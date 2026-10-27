using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Continut;
public class SimulariTests : TestCuHarness
{
    private HttpClient elev = null!;
    private int lectie;
    private Dictionary<int, int> bune = [];
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync(); elev = await h.Login("george"); using var admin = await h.Login("admin");
        lectie = await Harness.LectiePlatforma(admin);
        await h.Db(async db => {
            for (var i = 0; i < 4; i++) db.Add(new Intrebare { ParcursId = lectie, MaterieId = 1, Text = $"Întrebarea {i}", Explicatie = "Soluția secretă", Variante = [new() { Text = "Bun", Corecta = true }, new() { Text = "Rău" }] });
            db.Add(new Intrebare { ParcursId = lectie, MaterieId = 1, Text = "Anul?", Tip = Intrebare.Estimare, RaspunsNumeric = 1883 });
            await db.SaveChangesAsync(); bune = await db.Set<VariantaIntrebare>().Where(v => v.Corecta).ToDictionaryAsync(v => v.IntrebareId, v => v.Id);
        });
    }
    private Task<JsonElement> Incepe(string tip = "bac", string clasa = "Bac") => elev.Post("/simulari", new { materieId = 1, tip, clasa, numarIntrebari = 5, durataMinute = 5 }).Json();
    [Fact]
    public async Task SolutiileSuntAscunsePanaLaFinalSiScorulIncludeIntrebariNumerice()
    {
        var s = await Incepe(); var id = s.Int("id"); s.GetProperty("corecte").ValueKind.ShouldBe(JsonValueKind.Null);
        foreach (var q in s.GetProperty("intrebari").EnumerateArray())
        {
            q.GetProperty("explicatie").ValueKind.ShouldBe(JsonValueKind.Null); q.GetProperty("raspunsNumeric").ValueKind.ShouldBe(JsonValueKind.Null);
            var r = await elev.Post($"/simulari/{id}/raspuns", new { intrebareId = q.Int("id"), variantaId = bune.TryGetValue(q.Int("id"), out var bun) ? (int?)bun : null, valoare = q.Str("tip") == "estimare" ? (int?)1883 : null }).Json();
            r.GetProperty("corecte").ValueKind.ShouldBe(JsonValueKind.Null);
        }
        var final = await elev.Post($"/simulari/{id}/finalizare").Json(); final.Int("corecte").ShouldBe(5);
        final.GetProperty("notaOrientativa").GetDecimal().ShouldBe(10); final.GetProperty("deRecapitulat").GetArrayLength().ShouldBe(0);
        (await h.Db(db => db.EvenimenteRecompensa.CountAsync())).ShouldBe(0);
        (await h.Db(db => db.Set<ProgresLectie>().CountAsync())).ShouldBe(0);
    }
    [Fact]
    public async Task CronometrulEsteImpusDeServerIarNeraspunseleSuntGresite()
    {
        var s = await Incepe(); h.Ceas.Advance(TimeSpan.FromMinutes(6));
        var final = await elev.GetJson($"/simulari/{s.Int("id")}"); final.Int("corecte").ShouldBe(0);
        final.GetProperty("deRecapitulat").GetArrayLength().ShouldBe(1);
        await elev.Post($"/simulari/{s.Int("id")}/raspuns", new { intrebareId = 1, variantaId = 1 }).Status(HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task RaspunsurilePersistaSiSimulareaNuPoateFiCititaDeAltElev()
    {
        var s = await Incepe(); var q = s.GetProperty("intrebari").EnumerateArray().First(q => q.Str("tip") == "grila");
        await elev.Post($"/simulari/{s.Int("id")}/raspuns", new { intrebareId = q.Int("id"), variantaId = bune[q.Int("id")] }).Status(HttpStatusCode.OK);
        (await elev.GetJson($"/simulari/{s.Int("id")}")).GetProperty("intrebari").EnumerateArray().Count(x => x.Bool("raspunsa")).ShouldBe(1);
        using var altul = await h.InregistreazaElev("altulexamen");
        await altul.GetAsync($"/simulari/{s.Int("id")}", Ct).Status(HttpStatusCode.NotFound);
        await elev.Post("/simulari", new { materieId = 1, tip = "bac", clasa = "Bac", numarIntrebari = 5, durataMinute = 5 }).Status(HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task EvaluareaNationalaFolosesteClasaVIII()
    {
        await h.Db(async db => { var p = await db.Set<Parcurs>().FindAsync(lectie); p!.Clasa = "VIII"; await db.SaveChangesAsync(); });
        (await Incepe("evaluare_nationala", "VIII")).Str("tip").ShouldBe("evaluare_nationala");
        (await elev.GetJson("/simulari/optiuni")).GetArrayLength().ShouldBe(1);
    }
    [Fact]
    public async Task OptiunileTinuteMinutulNuAmestecaOrganizatiile()
    {
        // The options are kept for a minute per group of organisations: an outsider must never get a member's cached answer.
        await h.Db(async db => {
            var liceu = await db.Set<Organizatie>().Where(o => o.Nume == "Liceul de Test").Select(o => o.Id).SingleAsync();
            var p = new Parcurs { MaterieId = 2, Titlu = "Doar pentru liceu", Clasa = "Bac", Vizibilitate = Parcurs.DoarOrganizatie, OrganizatieId = liceu, CreatLa = DateTime.UtcNow };
            db.Add(p); await db.SaveChangesAsync();
            for (var i = 0; i < 2; i++) db.Add(new Intrebare { ParcursId = p.Id, MaterieId = 2, Text = $"Internă {i}", Variante = [new() { Text = "Da", Corecta = true }, new() { Text = "Nu" }] });
            var membru = new Utilizator { Nume = "Membru Liceu", Username = "membruliceu", Email = "membruliceu@example.invalid", ParolaHash = (await db.Utilizatori.SingleAsync(u => u.Username == "george")).ParolaHash, EmailConfirmatLa = DateTime.UtcNow, CreatLa = DateTime.UtcNow, Clasa = "XII" };
            db.Add(membru); await db.SaveChangesAsync();
            db.Add(new UtilizatorRol { UtilizatorId = membru.Id, RolId = await db.Set<Rol>().Where(r => r.Nume == "elev").Select(r => r.Id).SingleAsync(), AtribuitLa = DateTime.UtcNow });
            db.Add(new MembruOrganizatie { OrganizatieId = liceu, UtilizatorId = membru.Id, Rol = "elev", Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        static bool AreMateria2(JsonElement o) => o.EnumerateArray().Any(x => x.Int("materieId") == 2);
        AreMateria2(await elev.GetJson("/simulari/optiuni")).ShouldBeFalse();
        using var membru = await h.Login("membruliceu");
        AreMateria2(await membru.GetJson("/simulari/optiuni")).ShouldBeTrue();
        AreMateria2(await elev.GetJson("/simulari/optiuni")).ShouldBeFalse();
    }
    [Fact]
    public async Task AccesulRetrasLaMaterialeAscundeSimularea()
    {
        var s = await Incepe(); await h.Db(async db => { var p = await db.Set<Parcurs>().FindAsync(lectie); p!.Publicat = false; await db.SaveChangesAsync(); });
        await elev.GetAsync($"/simulari/{s.Int("id")}", Ct).Status(HttpStatusCode.NotFound);
        await elev.Post($"/simulari/{s.Int("id")}/finalizare").Status(HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task ExportulNuDezvaluieSolutiileUnuiExamenDeschisIarStergereaIlElimina()
    {
        await Incepe();
        var export = await elev.Post("/utilizatori/me/date/export", new { parolaActuala = Harness.ParolaTest }).Json();
        export.GetProperty("simulari").GetArrayLength().ShouldBe(1); export.GetRawText().ShouldNotContain("Soluția secretă");
        await elev.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/utilizatori/me/date") { Content = JsonContent.Create(new { parolaActuala = Harness.ParolaTest, confirmare = "STERGE" }) }, Ct).Status(HttpStatusCode.Accepted);
        (await h.Db(db => db.Set<Simulare>().CountAsync())).ShouldBe(0);
    }
    [Fact]
    public async Task ScorulPartialSiInstantaneulRamanCorecteDupaEditareaBancii()
    {
        var s = await Incepe(); var q = s.GetProperty("intrebari").EnumerateArray().First(q => q.Str("tip") == "grila");
        var qid = q.Int("id");
        await elev.Post($"/simulari/{s.Int("id")}/raspuns", new { intrebareId = qid, variantaId = bune[qid] }).Status(HttpStatusCode.OK);
        await h.Db(async db => { var intrebare = await db.Set<Intrebare>().FindAsync(qid); intrebare!.Text = "Întrebare editată"; await db.SaveChangesAsync(); });
        var r = await elev.Post($"/simulari/{s.Int("id")}/finalizare").Json(); r.Int("corecte").ShouldBe(1);
        r.GetProperty("notaOrientativa").GetDecimal().ShouldBe(2.8m);
        r.GetProperty("intrebari").EnumerateArray().Single(x => x.Int("id") == qid).Str("text").ShouldBe(q.Str("text"));
    }
}
