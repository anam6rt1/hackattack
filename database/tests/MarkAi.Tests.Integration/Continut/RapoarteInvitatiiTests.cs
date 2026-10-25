using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Continut;
public class RapoarteInvitatiiTests : TestCuHarness
{
    private HttpClient director = null!, elev = null!, profesor = null!;
    private int org, uid;
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync(); director = await h.Login("director"); elev = await h.Login("george"); profesor = await h.Login("irina");
        org = Harness.IdLiceu; uid = (await elev.GetJson("/utilizatori/me")).Int("id");
    }
    private Task<JsonElement> Preview(string csv = "nume,email,clasa\nElev Invitat,nouinvitat@example.invalid,VIII") => director.Post($"/organizatii/{org}/invitatii/previzualizare", new { csv }).Json();
    private async Task<string> Invita(string csv = "nume,email,clasa\nElev Invitat,nouinvitat@example.invalid,VIII")
    {
        var p = await Preview(csv);
        await director.Post($"/organizatii/{org}/invitatii/trimite", new { previzualizare = p.Str("previzualizare") }).Status(HttpStatusCode.OK);
        var text = await h.Db(db => db.Set<MesajIesire>().Where(m => m.Tip == "invitatie_elev").OrderByDescending(m => m.Id).Select(m => m.Text!).FirstAsync());
        return text.Split('#').Last();
    }
    private async Task<string> CereParinte()
    {
        await elev.Put("/utilizatori/me/sumar-parinte", new { email = "parinte@example.invalid" }).Status(HttpStatusCode.NoContent);
        var text = await h.Db(db => db.Set<MesajIesire>().Where(m => m.Tip == "confirmare_sumar_parinte").Select(m => m.Text!).SingleAsync());
        return text.Split('#').Last();
    }
    [Fact]
    public async Task ImportulAreEroriPeRandSiNuCreeazaConturiInainteDeAcceptare()
    {
        var p = await Preview("nume,email,clasa\n\"Popescu, Ana\",ana@example.invalid,VIII\nAltul,gresit,VII\nDublu,ana@example.invalid,X");
        p.GetProperty("randuri").GetArrayLength().ShouldBe(3); p.GetProperty("previzualizare").ValueKind.ShouldBe(JsonValueKind.Null);
        p.GetProperty("randuri")[0].GetProperty("eroare").ValueKind.ShouldBe(JsonValueKind.Null);
        p.GetProperty("randuri")[1].Str("eroare").ShouldNotBeNullOrEmpty();
        var token = await Invita();
        (await h.Db(db => db.Utilizatori.AnyAsync(u => u.Email == "nouinvitat@example.invalid"))).ShouldBeFalse();
        var stored = await h.Db(db => db.Set<InvitatieElev>().Select(i => i.TokenHash).SingleAsync()); stored.ShouldNotBe(token);
    }
    [Fact]
    public async Task InvitatulIsiAlegeParolaSiAcceptaOData()
    {
        var token = await Invita(); var anonim = h.Anonim();
        await anonim.Post("/invitatii/accepta", new { token, username = "nouinvitat", parola = Harness.ParolaTest }).Status(HttpStatusCode.OK);
        await anonim.Post("/invitatii/accepta", new { token, username = "nouinvitat2", parola = Harness.ParolaTest }).Status(HttpStatusCode.NotFound);
        using var cont = await h.Login("nouinvitat"); (await cont.GetJson("/utilizatori/me")).Str("clasa").ShouldBe("VIII");
        (await h.Db(db => db.Set<MembruOrganizatie>().CountAsync(m => m.OrganizatieId == org && m.Stare == "aprobat" && m.Utilizator.Username == "nouinvitat"))).ShouldBe(1);
    }
    [Fact]
    public async Task ContulExistentNuPoateFiResetatPrinInvitatie()
    {
        var email = (await elev.GetJson("/utilizatori/me")).Str("email");
        var token = await Invita($"nume,email,clasa\nGeorge,{email},IX");
        await h.Anonim().Post("/invitatii/accepta", new { token, username = "atac", parola = "altaparola" }).Status(HttpStatusCode.Conflict);
        await elev.Post("/invitatii/accepta", new { token }).Status(HttpStatusCode.OK);
        using var reintra = await h.Login("george");
    }
    [Fact]
    public async Task ImportulEsteDoarPentruAdminSiPrevizualizareaNuPoateFiReutilizata()
    {
        await profesor.Post($"/organizatii/{org}/invitatii/previzualizare", new { csv = "nume,email,clasa\nA,a@example.invalid,IX" }).Status(HttpStatusCode.Forbidden);
        var p = await Preview(); var corp = new { previzualizare = p.Str("previzualizare") };
        await director.Post($"/organizatii/{org}/invitatii/trimite", corp).Status(HttpStatusCode.OK);
        await director.Post($"/organizatii/{org}/invitatii/trimite", corp).Status(HttpStatusCode.BadRequest);
        var p2 = await Preview(); var r = await director.Post($"/organizatii/{org}/invitatii/trimite", new { previzualizare = p2.Str("previzualizare") }).Json(); r.Int("omise").ShouldBe(1);
    }
    [Fact]
    public async Task InvitatiaExpirataEsteRefuzata()
    {
        var token = await Invita(); h.Ceas.Advance(TimeSpan.FromDays(8));
        await h.Anonim().Post("/invitatii/detalii", new { token }).Status(HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task RaportulCalculeazaInSqlSiSeReutilizeazaDinCache()
    {
        var now = h.Ceas.GetUtcNow().UtcDateTime;
        await h.Db(async db => {
            db.Add(new MembruOrganizatie { OrganizatieId = org, UtilizatorId = uid, Rol = "elev", Stare = "aprobat", CerutLa = now });
            var p = new Parcurs { MaterieId = 1, Titlu = "Raport lecție", Clasa = "Bac" }; db.Add(p);
            db.Add(new ProgresLectie { Parcurs = p, UtilizatorId = uid, PromovatLa = now, UltimaAccesare = now });
            db.Add(new Tema { OrganizatieId = org, Titlu = "Raport temă", CreatLa = now.AddDays(-2), TermenLa = now, ToataOrganizatia = true, Lectii = [new() { Parcurs = p }] });
            await db.SaveChangesAsync();
        });
        var luna = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest")).ToString("yyyy-MM");
        var r = await director.GetJson($"/organizatii/{org}/raport?luna={luna}"); r.Str("luna").ShouldBe(luna); r.Int("eleviActivi").ShouldBe(1); r.Int("lectiiTerminate").ShouldBe(1); r.GetProperty("teme").Int("predate").ShouldBe(1);
        await h.Db(db => db.Set<ProgresLectie>().Where(p => p.UtilizatorId == uid).ExecuteDeleteAsync());
        (await director.GetJson($"/organizatii/{org}/raport?luna={luna}")).Int("lectiiTerminate").ShouldBe(1);
        await profesor.GetAsync($"/organizatii/{org}/raport?luna={luna}", Ct).Status(HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task ParinteleConfirmaExplicitSiSumarulNuSeDublezaza()
    {
        var token = await CereParinte(); await h.Serviciu<SumareParinti>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(m => m.Tip == "sumar_parinte"))).ShouldBe(0);
        await h.Anonim().Post("/sumar-parinte/confirmare", new { token }).Status(HttpStatusCode.OK);
        await h.Anonim().Post("/sumar-parinte/confirmare", new { token }).Status(HttpStatusCode.BadRequest);
        h.Ceas.Advance(TimeSpan.FromDays(8)); await h.Serviciu<SumareParinti>().RuleazaOdataAsync(Ct); await h.Serviciu<SumareParinti>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(m => m.Tip == "sumar_parinte"))).ShouldBe(1);
        var text = await h.Db(db => db.Set<MesajIesire>().Where(m => m.Tip == "sumar_parinte").Select(m => m.Text!).SingleAsync());
        await h.Anonim().Post("/sumar-parinte/oprire", new { token = text.Split('#').Last() }).Status(HttpStatusCode.OK);
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<MesajIesire>().Where(m => m.Tip == "sumar_parinte").Select(m => m.Furnizor).SingleAsync())).ShouldBe("anulat");
    }
    [Fact]
    public async Task OprireaDeCatreElevAnuleazaConfirmareaInAsteptare()
    {
        var token = await CereParinte();
        await elev.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/utilizatori/me/sumar-parinte"), Ct).Status(HttpStatusCode.NoContent);
        await h.Anonim().Post("/sumar-parinte/confirmare", new { token }).Status(HttpStatusCode.BadRequest);
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<MesajIesire>().Where(m => m.Tip == "confirmare_sumar_parinte").Select(m => m.Furnizor).SingleAsync())).ShouldBe("anulat");
    }
    [Fact]
    public async Task StergereaContuluiEliminaAdresaParinteluiSiEmailurileSale()
    {
        await CereParinte();
        await elev.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/utilizatori/me/date") { Content = JsonContent.Create(new { parolaActuala = Harness.ParolaTest, confirmare = "STERGE" }) }, Ct).Status(HttpStatusCode.Accepted);
        (await h.Db(db => db.Set<SumarParinte>().CountAsync())).ShouldBe(0);
        (await h.Db(db => db.Set<MesajIesire>().CountAsync(m => m.Catre == "parinte@example.invalid"))).ShouldBe(0);
    }
    [Fact]
    public async Task StatisticaPastreazaCorectitudineaSiEsteVizibilaDoarEditorilor()
    {
        var ids = await h.Db(async db => {
            var p = new Parcurs { MaterieId = 1, Titlu = "Statistica test", Clasa = "Bac", Publicat = true }; db.Add(p);
            var q = new Intrebare { Parcurs = p, MaterieId = 1, Text = "Întrebare statistică", Variante = [new() { Text = "Da", Corecta = true }, new() { Text = "Nu" }] }; db.Add(q); await db.SaveChangesAsync();
            return (p.Id, Q: q.Id, V: q.Variante.Single(v => v.Corecta).Id);
        });
        var quiz = await elev.Post($"/parcursuri/{ids.Id}/quiz").Json();
        await elev.Post($"/parcursuri/{ids.Id}/quiz/{quiz.Int("id")}/raspuns", new { intrebareId = ids.Q, variantaId = ids.V }).Status(HttpStatusCode.OK);
        await h.Db(db => db.Set<VariantaIntrebare>().Where(v => v.Id == ids.V).ExecuteUpdateAsync(s => s.SetProperty(v => v.Corecta, false)));
        using var admin = await h.Login("admin");
        var stat = await admin.GetJson($"/parcursuri/{ids.Id}/statistici-intrebari"); var qstat = stat.GetProperty("elemente")[0]; qstat.Int("raspunsuri").ShouldBe(1); qstat.Int("corecte").ShouldBe(1);
        await profesor.GetAsync($"/parcursuri/{ids.Id}/statistici-intrebari", Ct).Status(HttpStatusCode.Forbidden);
        var ack = await elev.GetJson($"/parcursuri/{ids.Id}/quiz/{quiz.Int("id")}/stare"); ack.GetProperty("raspunsuri")[0].Int("variantaId").ShouldBe(ids.V);
    }
}

public class BugetInvitatiiTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?> { ["Email:MaxPeZi"] = "1" };
    [Fact]
    public async Task DepasireaBugetuluiNuTrimitePartialSiNuCreeazaInvitatii()
    {
        using var d = await h.Login("director");
        var p = await d.Post($"/organizatii/{Harness.IdLiceu}/invitatii/previzualizare", new { csv = "nume,email,clasa\nA,a@example.invalid,IX\nB,b@example.invalid,X" }).Json();
        await d.Post($"/organizatii/{Harness.IdLiceu}/invitatii/trimite", new { previzualizare = p.Str("previzualizare") }).Status(HttpStatusCode.Conflict);
        (await h.Db(db => db.Set<InvitatieElev>().CountAsync())).ShouldBe(0);
    }
}
