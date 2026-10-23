using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>
/// The platform admin withdraws a professor's approval (POST /admin/profesori/{id}/retragere): refused with the reason, upcoming
/// sessions cancelled with their bookings, the professor and the booked students told. Memberships the professor already had
/// don't approve them again; an organisation's approval given afterwards does ("Aprobă din nou", or a new request approved), and
/// so does the platform admin. irina teaches in Liceul de Test, run by director; george books.
/// </summary>
public sealed class RetragereProfesorTests : TestOrganizatii
{
    private HttpClient admin = null!, director = null!, irina = null!, george = null!;
    private int irinaId;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        (admin, director, irina, george) = (await h.Login("admin"), await h.Login("director"), await h.Login("irina"), await h.Login("george"));
        irinaId = (await irina.GetJson("/utilizatori/me")).Int("id");
    }

    /// <summary>Withdraws the approval; the fake clock then moves on, as real time would before anyone decides again.</summary>
    private async Task<HttpResponseMessage> Retrage(int id, string? motiv = "Plângeri repetate de la elevi.")
    {
        var r = await admin.Post($"/admin/profesori/{id}/retragere", new { motiv });
        h.Ceas.Advance(TimeSpan.FromMinutes(1));
        return r;
    }

    private async Task<string> Stare(HttpClient c) => (await c.GetJson("/utilizatori/me")).Str("stareProfesor");

    private static async Task<int> Sedinta(HttpClient profesor, string titlu, int zile = 2)
    {
        var panou = await profesor.Post("/profesori/me/sloturi", new { materieId = 1, titlu, inceputLa = DateTime.UtcNow.AddDays(zile), capacitate = 3, proBono = true,
            linkIntalnire = "https://meet.example.invalid/x" }).Json(HttpStatusCode.Created);
        return panou.Lista("sloturi").Select(s => s.GetProperty("slot")).Single(s => s.Str("titlu") == titlu).Int("id");
    }

    [Fact]
    public async Task AdminulRetrageAprobareaCuMotiv()
    {
        (await Retrage(irinaId).Json()).Int("sedinteAnulate").ShouldBe(0);
        var eu = await irina.GetJson("/utilizatori/me");
        eu.Str("stareProfesor").ShouldBe("respins");
        eu.Str("motivRespingere").ShouldBe("Plângeri repetate de la elevi.");
        await irina.Get("/profesori/me").Status(HttpStatusCode.Forbidden);
        await irina.Post("/profesori/me/sloturi", new { materieId = 1, titlu = "x", inceputLa = DateTime.UtcNow.AddDays(2), capacitate = 1, proBono = true })
            .Status(HttpStatusCode.Forbidden);
        await george.Get($"/profesori/{irinaId}").Status(HttpStatusCode.NotFound);
        (await admin.GetJson("/admin/profesori")).Lista().Single(p => p.Int("id") == irinaId).EsteNull("retrasLa").ShouldBeFalse();
    }

    [Fact]
    public async Task RetragereaAnuleazaSedinteleViitoareSiRezervarile()
    {
        var (s1, s2) = (await Sedinta(irina, "Prima"), await Sedinta(irina, "A doua", 5));
        await george.Post("/rezervari", new { slotId = s1 }).Status(HttpStatusCode.Created);
        // A session already held is history: it stays as it was.
        var trecuta = await h.Db(async db =>
        {
            var c = new CameraTutoring { MentorId = irinaId, MaterieId = 1, Titlu = "Ieri", Capacitate = 2, ProBono = true, Status = RezervareService.SlotDeschis,
                InceputLa = DateTime.UtcNow.AddDays(-1), FinalizatLa = DateTime.UtcNow.AddDays(-1).AddHours(1) };
            db.Add(c);
            await db.SaveChangesAsync();
            return c.Id;
        });
        (await Retrage(irinaId).Json()).Int("sedinteAnulate").ShouldBe(2);
        var stari = await h.Db(db => db.CamereTutoring.Where(c => c.MentorId == irinaId).ToDictionaryAsync(c => c.Id, c => c.Status));
        (stari[s1], stari[s2], stari[trecuta]).ShouldBe(("anulata", "anulata", RezervareService.SlotDeschis));
        (await george.GetJson("/rezervari")).Lista().Single(r => r.GetProperty("slot").Int("id") == s1).Str("status").ShouldBe("anulata");
    }

    [Fact]
    public async Task ProfesorulSiEleviiRezervatiAflaPrinEmail()
    {
        var s = await Sedinta(irina, "Recapitulare");
        await george.Post("/rezervari", new { slotId = s }).Status(HttpStatusCode.Created);
        await Retrage(irinaId);
        await Proceseaza();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "george@example.invalid" && m.Subiect.Contains("O ședință a fost anulată") && m.Text.Contains("Recapitulare"));
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "irina@example.invalid" && m.Subiect.Contains("retrasă") && m.Text.Contains("Plângeri repetate"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RetragereaCereUnMotiv(string? motiv)
    {
        await Retrage(irinaId, motiv).Status(HttpStatusCode.BadRequest);
        (await Stare(irina)).ShouldBe("aprobat");
    }

    [Fact]
    public async Task DoarAdminulPlatformeiRetrage()
    {
        await director.Post($"/admin/profesori/{irinaId}/retragere", new { motiv = "x" }).Status(HttpStatusCode.Forbidden);
        await george.Post($"/admin/profesori/{irinaId}/retragere", new { motiv = "x" }).Status(HttpStatusCode.Forbidden);
        await h.Anonim().Post($"/admin/profesori/{irinaId}/retragere", new { motiv = "x" }).Status(HttpStatusCode.Unauthorized);
        (await Stare(irina)).ShouldBe("aprobat");
    }

    [Fact]
    public async Task NuSeRetrageUnProfesorNeaprobatSauInexistent()
    {
        var (_, paul) = await ProfesorNou();
        await Retrage(paul).Status(HttpStatusCode.Conflict);
        await Retrage(irinaId);
        await Retrage(irinaId).Status(HttpStatusCode.Conflict);
        await Retrage(999_999).Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OApartenentaVecheNuReaproba()
    {
        await Retrage(irinaId);
        // irina is still an approved member of Liceul de Test; a new request elsewhere makes the professor's state recalculate.
        var (_, alba) = await OrganizatieNoua(aprobata: true);
        await irina.Post("/utilizatori/me/organizatii", new { organizatieId = alba });
        var eu = await irina.GetJson("/utilizatori/me");
        eu.Str("stareProfesor").ShouldBe("respins");
        eu.Str("motivRespingere").ShouldBe("Plângeri repetate de la elevi.");
    }

    [Fact]
    public async Task OrganizatiaIlAprobaDinNou()
    {
        await Retrage(irinaId);
        (await director.GetJson("/organizatia-mea/membri?rol=profesor")).Lista().Single(m => m.Int("utilizatorId") == irinaId).Bool("profesorRetras").ShouldBeTrue();
        await director.Post($"/organizatia-mea/membri/{irinaId}/reaprobare").Status(HttpStatusCode.NoContent);
        (await Stare(irina)).ShouldBe("aprobat");
        await irina.Get("/profesori/me").Status(HttpStatusCode.OK);
        (await director.GetJson("/organizatia-mea/membri?rol=profesor")).Lista().Single(m => m.Int("utilizatorId") == irinaId).Bool("profesorRetras").ShouldBeFalse();
        await Proceseaza();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "irina@example.invalid" && m.Subiect.Contains("din nou profesor aprobat"));
    }

    [Fact]
    public async Task OCerereNouaAprobataDupaRetragereIlAprobaDinNou()
    {
        await Retrage(irinaId);
        var (ovidiu, alba) = await OrganizatieNoua(aprobata: true);
        await irina.Post("/utilizatori/me/organizatii", new { organizatieId = alba });
        await ovidiu.Post($"/organizatia-mea/membri/{irinaId}/aprobare").Status(HttpStatusCode.NoContent);
        (await Stare(irina)).ShouldBe("aprobat");
    }

    [Fact]
    public async Task AdminulOrganizatieiNuSeAprobaDinNouSingur()
    {
        var directorId = (await director.GetJson("/utilizatori/me")).Int("id");
        await Retrage(directorId);
        (await director.Post($"/organizatia-mea/membri/{directorId}/reaprobare").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("singur");
        (await Stare(director)).ShouldBe("respins");
    }

    [Fact]
    public async Task OrganizatiaNuReaprobaUnProfesorNeretrasSauUnElev()
    {
        await director.Post($"/organizatia-mea/membri/{irinaId}/reaprobare").Status(HttpStatusCode.Conflict);
        var (_, sara) = await ElevNou();
        await director.Post($"/organizatia-mea/membri/{sara}/reaprobare").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OAltaOrganizatieNuReaprobaUnProfesorCareNuEsteAlEi()
    {
        await Retrage(irinaId);
        var (ovidiu, _) = await OrganizatieNoua(aprobata: true);
        await ovidiu.Post($"/organizatia-mea/membri/{irinaId}/reaprobare").Status(HttpStatusCode.NotFound);
        (await Stare(irina)).ShouldBe("respins");
    }

    [Fact]
    public async Task AdminulPlatformeiIlAprobaDinNou()
    {
        await Retrage(irinaId);
        await admin.Post($"/admin/profesori/{irinaId}/reaprobare").Status(HttpStatusCode.NoContent);
        (await Stare(irina)).ShouldBe("aprobat");
        (await admin.GetJson("/admin/profesori")).Lista().Single(p => p.Int("id") == irinaId).EsteNull("retrasLa").ShouldBeTrue();
        await admin.Post($"/admin/profesori/{irinaId}/reaprobare").Status(HttpStatusCode.Conflict);
        await director.Post($"/admin/profesori/{irinaId}/reaprobare").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DupaReaprobareRetragereaSePoateRepeta()
    {
        await Retrage(irinaId);
        await director.Post($"/organizatia-mea/membri/{irinaId}/reaprobare");
        await Retrage(irinaId, "A doua oară.").Status(HttpStatusCode.OK);
        (await irina.GetJson("/utilizatori/me")).Str("motivRespingere").ShouldBe("A doua oară.");
    }

    [Fact]
    public async Task OSedintaPlatitaAnulataAnuleazaSiComisionul()
    {
        await h.Db(async db => { db.Add(new TarifFix { Suma = 80, Moneda = "RON", DurataMinute = 60, Activ = true }); await db.SaveChangesAsync(); });
        var panou = await irina.Post("/profesori/me/sloturi", new { materieId = 1, titlu = "Plătită", inceputLa = DateTime.UtcNow.AddDays(2), capacitate = 2,
            proBono = false, linkIntalnire = "https://meet.example.invalid/x" }).Json(HttpStatusCode.Created);
        var s = panou.Lista("sloturi").Select(x => x.GetProperty("slot")).Single(x => x.Str("titlu") == "Plătită").Int("id");
        await george.Post("/rezervari", new { slotId = s }).Status(HttpStatusCode.Created);
        await Retrage(irinaId);
        var r = await h.Db(db => db.Rezervari.Include(x => x.ComisionMentor).Include(x => x.Plati).SingleAsync(x => x.CameraId == s));
        (r.Status, r.ComisionMentor!.Status).ShouldBe(("anulata", "anulat"));
        r.Plati.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task FiecareElevRezervatPrimesteUnEmailPeSedinta()
    {
        var (s1, s2) = (await Sedinta(irina, "Prima"), await Sedinta(irina, "A doua", 4));
        var (sara, _) = await ElevNou();
        foreach (var (c, slot) in new[] { (george, s1), (george, s2), (sara, s1) })
            await c.Post("/rezervari", new { slotId = slot }).Status(HttpStatusCode.Created);
        await Retrage(irinaId);
        await Proceseaza();
        h.Emailuri.Trimise.Count(m => m.Subiect.Contains("O ședință a fost anulată")).ShouldBe(3);
        h.Emailuri.Trimise.Count(m => m.Catre == "george@example.invalid" && m.Subiect.Contains("O ședință a fost anulată")).ShouldBe(2);
    }

    [Fact]
    public async Task OSedintaAnulataDeJaNuMaiEsteNumarata()
    {
        var s = await Sedinta(irina, "Anulată de ea");
        await irina.Delete($"/profesori/me/sloturi/{s}").Status(HttpStatusCode.NoContent);
        (await Retrage(irinaId).Json()).Int("sedinteAnulate").ShouldBe(0);
    }

    [Fact]
    public async Task AdminulOrganizatieiRetrasIsiConduceInContinuareOrganizatiaPrinApi()
    {
        // The withdrawal is of the teaching approval: director still runs Liceul de Test through the API (the site's /organizatie
        // page sends an unapproved professor to /in-asteptare — open question in plan 2's bugs.log), but cannot teach.
        var directorId = (await director.GetJson("/utilizatori/me")).Int("id");
        await Retrage(directorId);
        (await director.GetJson("/organizatia-mea")).Str("nume").ShouldBe("Liceul de Test");
        var (_, sara) = await ElevNou();
        await director.Post($"/organizatia-mea/membri/{sara}/aprobare").Status(HttpStatusCode.NoContent);
        await director.Get("/profesori/me").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnAdminDeOrganizatieRetrasEsteReaprobatCandONouaOrganizatieAluiEsteAprobata()
    {
        // ovidiu applied for an organisation; withdrawn before it is approved; the platform approving it afterwards counts.
        var (ovidiu, alba) = await OrganizatieNoua();
        var ovidiuId = (await ovidiu.GetJson("/utilizatori/me")).Int("id");
        await h.Db(db => db.ProfiluriMentor.Where(p => p.UtilizatorId == ovidiuId).ExecuteUpdateAsync(x => x.SetProperty(p => p.StareVerificare, "aprobat")));
        await Retrage(ovidiuId);
        await admin.Post($"/admin/organizatii/{alba}/aprobare").Status(HttpStatusCode.NoContent);
        (await Stare(ovidiu)).ShouldBe("aprobat");
    }

    [Fact]
    public async Task ProfesorulRetrasNuMaiPoateFiRezervat()
    {
        var s = await Sedinta(irina, "Înainte");
        await Retrage(irinaId);
        await george.Post("/rezervari", new { slotId = s }).Status(HttpStatusCode.Conflict);
        (await george.GetJson("/profesori")).Lista().ShouldNotContain(p => p.Int("id") == irinaId);
    }
}

