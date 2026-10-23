using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Cautare;

/// <summary>
/// Tutoring sessions: online, in person (at an organisation or the professor's own place) or hybrid; the place is public, the room
/// note only for booked students; the type filter; booking, how a student attends a hybrid session, capacity, cancelling, paid sessions.
/// </summary>
public sealed class SedinteTests : TestCautare
{
    private async Task<int> SalaLaLiceu(string nota = "Sala 12, etajul 2") =>
        await CreeazaSlot(irina, Slot("La liceu", Maine, tip: "fata_in_fata", organizatieId: Liceu, notaLoc: nota));

    private async Task<int> Hibrid() =>
        await CreeazaSlot(irina, Slot("Hibrid", Maine.AddHours(3), tip: "hibrid", organizatieId: Liceu, link: "https://meet.example.invalid/hibrid", notaLoc: "Sala 3"));

    private Task<string> Tip(int id) => h.Db(db => db.CamereTutoring.Where(s => s.Id == id).Select(s => s.Tip).SingleAsync());

    [Fact]
    public async Task ImplicitSedintaEsteOnline()
    {
        (await Tip(await CreeazaSlot(irina, Slot("Online", Maine)))).ShouldBe("online");
    }

    [Fact]
    public async Task SedintaOnlineNuAreNotaDeSala()
    {
        await irina.Post("/profesori/me/sloturi", Slot("Online cu notă", Maine, notaLoc: "Sala 1")).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SedintaFataInFataNuAreLink()
    {
        await irina.Post("/profesori/me/sloturi", Slot("Sală cu link", Maine, tip: "fata_in_fata", organizatieId: Liceu, link: "https://meet.example.invalid/x"))
            .Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcasaCereJudetulProfesorului()
    {
        (await irina.Post("/profesori/me/sloturi", Slot("Acasă", Maine, tip: "fata_in_fata")).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Setări");
    }

    [Fact]
    public async Task NuLaOOrganizatieStraina()
    {
        await irina.Post("/profesori/me/sloturi", Slot("Alba", Maine, tip: "fata_in_fata", organizatieId: Centru)).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TipulNecunoscutEsteRefuzat()
    {
        await irina.Post("/profesori/me/sloturi", Slot("Zbor", Maine, tip: "zbor")).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcasaCopiazaLocalitateaProfesorului()
    {
        var irinaId = await Id("irina");
        await h.Db(async db => { var u = await db.Utilizatori.SingleAsync(x => x.Id == irinaId); (u.JudetId, u.LocalitateId) = (Bucuresti, Sector2); await db.SaveChangesAsync(); });
        var acasa = await CreeazaSlot(irina, Slot("Acasă", Maine, tip: "fata_in_fata"));
        var s = await h.Db(db => db.CamereTutoring.SingleAsync(x => x.Id == acasa));
        (s.LocJudetId, s.LocLocalitateId, s.LocOrganizatieId).ShouldBe((Bucuresti, Sector2, (int?)null));
    }

    [Fact]
    public async Task LaOrganizatieCopiazaLoculEi()
    {
        var sala = await SalaLaLiceu();
        var s = await h.Db(db => db.CamereTutoring.SingleAsync(x => x.Id == sala));
        (s.LocOrganizatieId, s.LocJudetId, s.LocLocalitateId, s.NotaLoc).ShouldBe(((int?)Liceu, (int?)Bucuresti, (int?)Sector1, "Sala 12, etajul 2"));
    }

    [Fact]
    public async Task ProgramareaCuMaiPutinDeTreizeciDeMinuteEsteRefuzata()
    {
        (await irina.Post("/profesori/me/sloturi", Slot("Acum", DateTime.UtcNow.AddMinutes(10))).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("30 de minute");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public async Task LocurileSuntIntre1Si30(int capacitate)
    {
        await irina.Post("/profesori/me/sloturi", Slot("x", Maine, capacitate: capacitate)).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DoarLaMateriilePredate()
    {
        await irina.Post("/profesori/me/sloturi", new { materieId = 2, titlu = "Mate", inceputLa = Maine, capacitate = 5, proBono = true })
            .Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FaraTarifConfiguratSedintaPlatitaEsteRefuzata()
    {
        (await irina.Post("/profesori/me/sloturi", Slot("Plătită", Maine, proBono: false)).Mesaj(HttpStatusCode.Conflict)).ShouldContain("tarif");
    }

    [Fact]
    public async Task FiltrulFataInFataIncludeHibrideleSiLasaDeoparteOnline()
    {
        await CreeazaSlot(irina, Slot("Online", Maine));
        var sala = await SalaLaLiceu();
        var hibrid = await Hibrid();
        await CreeazaSlot(ovidiu, Slot("Doar online", Maine));
        var (irinaId, ovidiuId) = (await Id("irina"), await Id("ovidiu"));
        var p = await Profesori(george, "?tip=fata_in_fata");
        p.Single(x => x.Id == irinaId).Sloturi.Select(s => s.Id).Order().ShouldBe(new[] { sala, hibrid }.Order());
        p.ShouldNotContain(x => x.Id == ovidiuId);
    }

    [Fact]
    public async Task FiltrulOnlineIncludeHibridele()
    {
        var online = await CreeazaSlot(irina, Slot("Online", Maine));
        await SalaLaLiceu();
        var hibrid = await Hibrid();
        var irinaId = await Id("irina");
        (await Profesori(george, "?tip=online")).Single(x => x.Id == irinaId).Sloturi.Select(s => s.Id).Order().ShouldBe(new[] { online, hibrid }.Order());
    }

    [Fact]
    public async Task FiltrulDeTipNecunoscutEsteRefuzat()
    {
        await george.Get("/profesori?tip=zbor").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TotiVadLoculDarNuNotaDeSala()
    {
        var sala = await SalaLaLiceu();
        var s = (await Profesori(george, "?tip=fata_in_fata")).SelectMany(p => p.Sloturi).Single(x => x.Id == sala);
        s.Loc.ShouldBe("Liceul de Test · Sectorul 1, București");
        s.NotaLoc.ShouldBeNull();
    }

    [Fact]
    public async Task RezervareaLaSalaEsteLaSala()
    {
        var sala = await SalaLaLiceu();
        (await elena.Post("/rezervari", new { slotId = sala }).Json(HttpStatusCode.Created)).Str("participare").ShouldBe("la_sala");
    }

    [Fact]
    public async Task ElevulRezervatVedeNotaDeSala()
    {
        var sala = await SalaLaLiceu();
        await elena.Post("/rezervari", new { slotId = sala });
        (await elena.GetJson("/rezervari")).Lista().Single().GetProperty("slot").Str("notaLoc").ShouldBe("Sala 12, etajul 2");
    }

    [Fact]
    public async Task HibridulIntreabaCumParticipi()
    {
        var hibrid = await Hibrid();
        (await radu.Post("/rezervari", new { slotId = hibrid }).Mesaj(HttpStatusCode.Conflict)).ShouldContain("online sau la sală");
        await radu.Post("/rezervari", new { slotId = hibrid, participare = "teleportare" }).Status(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task HibridulRezervatOnlinePrimesteLinkulSiNota()
    {
        var hibrid = await Hibrid();
        await radu.Post("/rezervari", new { slotId = hibrid, participare = "online" }).Status(HttpStatusCode.Created);
        var s = (await radu.GetJson("/rezervari")).Lista().Single().GetProperty("slot");
        s.Str("linkIntalnire").ShouldBe("https://meet.example.invalid/hibrid");
        s.Str("notaLoc").ShouldBe("Sala 3");
    }

    [Fact]
    public async Task ProfesorulVedeCumParticipaFiecare()
    {
        var hibrid = await Hibrid();
        await radu.Post("/rezervari", new { slotId = hibrid, participare = "online" });
        await elena.Post("/rezervari", new { slotId = hibrid, participare = "la_sala" });
        var elevi = (await irina.GetJson("/profesori/me")).Lista("sloturi").Single(s => s.GetProperty("slot").Int("id") == hibrid).Lista("elevi")
            .ToDictionary(e => e.Str("nume"), e => e.Str("participare"));
        elevi["Radu Elev"].ShouldBe("online");
        elevi["Elena Elev"].ShouldBe("la_sala");
    }

    [Fact]
    public async Task SedintaFataInFataNuPrimesteLinkMaiTarziu()
    {
        var sala = await SalaLaLiceu();
        await irina.Put($"/profesori/me/sloturi/{sala}/link", new { linkIntalnire = "https://meet.example.invalid/y" }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SedintaOnlineNuPrimesteNotaMaiTarziu()
    {
        var online = await CreeazaSlot(irina, Slot("Online", Maine));
        await irina.Put($"/profesori/me/sloturi/{online}/nota", new { notaLoc = "Sala" }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NotaDeSalaSePoateSchimba()
    {
        var sala = await SalaLaLiceu();
        await irina.Put($"/profesori/me/sloturi/{sala}/nota", new { notaLoc = "Sala 14" }).Status(HttpStatusCode.OK);
        (await h.Db(db => db.CamereTutoring.Where(s => s.Id == sala).Select(s => s.NotaLoc).SingleAsync())).ShouldBe("Sala 14");
    }

    [Fact]
    public async Task LinkulOnlineTrebuieSaFieHttps()
    {
        await irina.Post("/profesori/me/sloturi", Slot("Online", Maine, link: "http://meet.example.invalid/x")).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AltProfesorNuSchimbaSedinta()
    {
        var sala = await SalaLaLiceu();
        await ovidiu.Put($"/profesori/me/sloturi/{sala}/nota", new { notaLoc = "Furat" }).Status(HttpStatusCode.NotFound);
        await ovidiu.Delete($"/profesori/me/sloturi/{sala}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SedintaFaraTipEsteOnline()
    {
        var irinaId = await Id("irina");
        await h.Db(db => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO mark_ai.camera_tutoring (mentor_id, materie_id, titlu, capacitate, pro_bono, status) VALUES ({0}, 1, 'Veche', 5, true, 'deschisa')", irinaId));
        (await h.Db(db => db.CamereTutoring.Where(s => s.Titlu == "Veche").Select(s => s.Tip).SingleAsync())).ShouldBe("online");
    }

    [Fact]
    public async Task OSedintaNuSeRezervaDeDouaOri()
    {
        var sala = await SalaLaLiceu();
        await elena.Post("/rezervari", new { slotId = sala });
        (await elena.Post("/rezervari", new { slotId = sala }).Mesaj(HttpStatusCode.Conflict)).ShouldContain("deja");
    }

    [Fact]
    public async Task LocurileSeTermina()
    {
        var unLoc = await CreeazaSlot(irina, Slot("Un loc", Maine, capacitate: 1));
        await elena.Post("/rezervari", new { slotId = unLoc }).Status(HttpStatusCode.Created);
        (await radu.Post("/rezervari", new { slotId = unLoc }).Mesaj(HttpStatusCode.Conflict)).ShouldContain("Nu mai sunt locuri");
    }

    [Fact]
    public async Task RezervarileSimultaneNuDepasescLocurile()
    {
        var doua = await CreeazaSlot(irina, Slot("Două locuri", Maine, capacitate: 2));
        var elevi = new List<HttpClient>();
        for (var i = 0; i < 6; i++) elevi.Add(await h.InregistreazaElev($"rapid{i}"));
        var raspunsuri = await Task.WhenAll(elevi.Select(e => e.Post("/rezervari", new { slotId = doua })));
        raspunsuri.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(2);
        (await h.Db(db => db.Rezervari.CountAsync(r => r.CameraId == doua && r.Status == RezervareService.RezervareConfirmata))).ShouldBe(2);
    }

    [Fact]
    public async Task AnulareaElibereazaLocul()
    {
        var unLoc = await CreeazaSlot(irina, Slot("Un loc", Maine, capacitate: 1));
        var id = (await elena.Post("/rezervari", new { slotId = unLoc }).Json(HttpStatusCode.Created)).Int("id");
        await elena.Delete($"/rezervari/{id}").Status(HttpStatusCode.NoContent);
        await radu.Post("/rezervari", new { slotId = unLoc }).Status(HttpStatusCode.Created);
    }

    [Fact]
    public async Task NuSeAnuleazaRezervareaAltcuiva()
    {
        var sala = await SalaLaLiceu();
        var id = (await elena.Post("/rezervari", new { slotId = sala }).Json(HttpStatusCode.Created)).Int("id");
        await radu.Delete($"/rezervari/{id}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SedintaAnulataDeProfesorAnuleazaRezervarile()
    {
        var sala = await SalaLaLiceu();
        await elena.Post("/rezervari", new { slotId = sala });
        await irina.Delete($"/profesori/me/sloturi/{sala}").Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.Rezervari.Where(r => r.CameraId == sala).Select(r => r.Status).SingleAsync())).ShouldBe(RezervareService.RezervareAnulata);
        await radu.Post("/rezervari", new { slotId = sala }).Status(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DoarEleviiRezerva()
    {
        var sala = await SalaLaLiceu();
        await ovidiu.Post("/rezervari", new { slotId = sala }).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SedintaInexistentaEste404()
    {
        await elena.Post("/rezervari", new { slotId = 999999 }).Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SedintaPlatitaInregistreazaPlataSiComisionul()
    {
        await h.Db(async db => { db.Add(new TarifFix { Suma = 50, Moneda = "RON", DurataMinute = 50, Activ = true }); await db.SaveChangesAsync(); });
        await h.Db(db => db.ProfiluriMentor.Where(p => p.Utilizator.Username == "irina").ExecuteUpdateAsync(s => s.SetProperty(p => p.OnorariuNegociat, 40)));
        var platita = await CreeazaSlot(irina, Slot("Plătită", Maine, proBono: false));
        var r = await elena.Post("/rezervari", new { slotId = platita }).Json(HttpStatusCode.Created);
        var rez = await h.Db(db => db.Rezervari.Include(x => x.Plati).Include(x => x.ComisionMentor).SingleAsync(x => x.Id == r.Int("id")));
        rez.TarifAchitat.ShouldBe(50m);
        rez.Plati.ShouldHaveSingleItem().Status.ShouldBe("simulata");
        rez.ComisionMentor!.SumaComision.ShouldBe(40m);
        rez.ComisionMentor.ProcentSnapshot.ShouldBe(80m);
    }

    [Fact]
    public async Task AnulareaSedinteiPlatiteRamburseazaSimulat()
    {
        await h.Db(async db => { db.Add(new TarifFix { Suma = 50, Moneda = "RON", DurataMinute = 50, Activ = true }); await db.SaveChangesAsync(); });
        var platita = await CreeazaSlot(irina, Slot("Plătită", Maine, proBono: false));
        var id = (await elena.Post("/rezervari", new { slotId = platita }).Json(HttpStatusCode.Created)).Int("id");
        await elena.Delete($"/rezervari/{id}").Status(HttpStatusCode.NoContent);
        var rez = await h.Db(db => db.Rezervari.Include(x => x.Plati).Include(x => x.ComisionMentor).SingleAsync(x => x.Id == id));
        rez.Plati.ShouldAllBe(p => p.Status == "rambursata");
        rez.ComisionMentor!.Status.ShouldBe("anulat");
    }
}
