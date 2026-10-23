using System.Net;
using System.Text.Json;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MarkAi.Tests.Integration.Continut;

/// <summary>
/// The student's progress page (level from XP, per-subject points and Lecții, the last Lecție, completed ones) and the content
/// detail reads (subject, chapter, the editor's chapter list). Lecții in three subjects and four grades.
/// </summary>
public sealed class ProgresTests : TestCuHarness
{
    private HttpClient admin = null!, george = null!;
    private int roRomantism, roInterbelic, mateFunctii, bioCelula, cap1, cap2;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await h.Db(async db =>
        {
            foreach (var (nivel, xp, titlu) in new[] { (0, 0, "Începător"), (1, 100, "Ucenic"), (2, 300, "Cititor"), (3, 600, "Cunoscător") })
                db.Add(new NivelXp { Nivel = nivel, XpNecesar = xp, Titlu = titlu });
            await db.SaveChangesAsync();
        });
        admin = await h.Login("admin");
        george = await h.Login("george");
        roRomantism = await Harness.LectiePlatforma(admin, 1, "Romantismul românesc", "XI");
        roInterbelic = await Harness.LectiePlatforma(admin, 1, "Romanul interbelic", "XII");
        mateFunctii = await Harness.LectiePlatforma(admin, 2, "Funcții de gradul al II-lea", "IX");
        bioCelula = await Harness.LectiePlatforma(admin, 3, "Celula — structură și funcții", "Bac");
        cap1 = await Harness.Capitol(admin, roRomantism, "Luceafărul");
        cap2 = await Harness.Capitol(admin, roRomantism, "Scrisoarea I", "„Scrisoarea I” a apărut în 1881.");
        await Harness.Capitol(admin, mateFunctii, "Vârful parabolei", "x = −b / 2a");
    }

    private async Task Promoveaza(int parcursId, string username = "george")
    {
        var uid = await h.Db(db => db.Utilizatori.Where(u => u.Username == username).Select(u => u.Id).SingleAsync());
        await using var scope = h.App.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProgresService>().PromoveazaAsync(uid, parcursId, Ct);
    }

    private Task<JsonElement> Progres() => george.GetJson("/progres");

    [Fact]
    public async Task ElevulNouEsteIncepator()
    {
        var n = (await Progres()).GetProperty("nivel");
        (n.Int("nivel"), n.Str("titlu"), n.Int("xpTotal"), n.Int("xpUrmatorNivel"), n.Str("titluUrmator")).ShouldBe((0, "Începător", 0, 100, "Ucenic"));
    }

    [Fact]
    public async Task OLectiePromovataDaNivelul1()
    {
        await Promoveaza(roRomantism);
        var n = (await Progres()).GetProperty("nivel");
        (n.Int("nivel"), n.Str("titlu"), n.Int("xpTotal")).ShouldBe((1, "Ucenic", 100));
    }

    [Fact]
    public async Task TreiLectiiDauNivelul2()
    {
        foreach (var l in new[] { roRomantism, mateFunctii, bioCelula }) await Promoveaza(l);
        var n = (await Progres()).GetProperty("nivel");
        (n.Int("nivel"), n.Str("titlu"), n.Int("xpTotal"), n.Int("xpUrmatorNivel")).ShouldBe((2, "Cititor", 300, 600));
    }

    [Fact]
    public async Task XpulDinTriviadorSeAduna()
    {
        var uid = await h.Db(db => db.Utilizatori.Where(u => u.Username == "george").Select(u => u.Id).SingleAsync());
        await h.Db(async db =>
        {
            db.Add(new EvenimentRecompensa { UtilizatorId = uid, XpAcordat = 650, PuncteMaterie = 0, TipEveniment = "triviador", CheieUnica = "triviador:test" });
            await db.SaveChangesAsync();
        });
        var n = (await Progres()).GetProperty("nivel");
        (n.Int("nivel"), n.Str("titlu")).ShouldBe((3, "Cunoscător"));
        n.EsteNull("xpUrmatorNivel").ShouldBeTrue();
        n.EsteNull("titluUrmator").ShouldBeTrue();
    }

    [Fact]
    public async Task MateriileAratLectiileTerminateDinTotal()
    {
        await Promoveaza(roRomantism);
        var r = await george.GetJson("/progres/materii");
        // Only the subjects where george opened a Lecție: Mathematics and Biology stay hidden.
        r.Int("total").ShouldBe(1);
        var ro = r.Lista().ShouldHaveSingleItem();
        ro.Str("denumire").ShouldBe("Limba și literatura română");
        (ro.Int("lectiiCompletate"), ro.Int("totalLectii"), ro.Int("puncte")).ShouldBe((1, 2, 10));
    }

    [Fact]
    public async Task OLectieDeschisaDarNeterminataAduceMateria()
    {
        await h.Db(async db =>
        {
            var georgeId = await db.Utilizatori.Where(u => u.Username == "george").Select(u => u.Id).SingleAsync();
            db.Add(new ProgresLectie { UtilizatorId = georgeId, ParcursId = mateFunctii, UltimaAccesare = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        var m = (await george.GetJson("/progres/materii")).Lista().ShouldHaveSingleItem();
        (m.Str("denumire"), m.Int("lectiiCompletate"), m.Int("totalLectii"), m.Int("puncte")).ShouldBe(("Matematică", 0, 1, 0));
    }

    [Fact]
    public async Task FaraLectiiDeschiseListaEsteGoala()
    {
        var r = await george.GetJson("/progres/materii");
        (r.Int("total"), r.Lista().Count).ShouldBe((0, 0));
    }

    [Fact]
    public async Task ProgresulPeMateriiArePagini()
    {
        await Promoveaza(roRomantism);
        await Promoveaza(bioCelula);
        var p1 = await george.GetJson("/progres/materii?marime=1");
        (p1.Int("total"), p1.Lista().Count).ShouldBe((2, 1));
        var p2 = await george.GetJson("/progres/materii?marime=1&pagina=2");
        p2.Lista().Single().Int("materieId").ShouldNotBe(p1.Lista().Single().Int("materieId"));
        await george.Get("/progres/materii?marime=101").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LectiileTerminateSuntCeleMaiNoiIntai()
    {
        await Promoveaza(bioCelula);
        await Task.Delay(20, Ct);
        await Promoveaza(mateFunctii);
        var l = (await Progres()).Lista("lectiiCompletate");
        l.Select(x => x.Int("parcursId")).ShouldBe([mateFunctii, bioCelula]);
        (l[0].Str("titlu"), l[0].Str("clasa"), l[0].Str("materie")).ShouldBe(("Funcții de gradul al II-lea", "IX", "Matematică"));
    }

    [Fact]
    public async Task UltimaLectieEsteCeaAccesataUltima()
    {
        await george.Post($"/cursuri/{cap1}/acces").Status(HttpStatusCode.NoContent);
        await Task.Delay(20, Ct);
        await george.Post($"/cursuri/{cap2}/acces").Status(HttpStatusCode.NoContent);
        var u = (await Progres()).GetProperty("ultimaLectie");
        (u.Int("parcursId"), u.Int("cursId"), u.Str("capitol"), u.Bool("completata")).ShouldBe((roRomantism, cap2, "Scrisoarea I", false));
    }

    [Fact]
    public async Task FaraActivitateNuEsteUltimaLectie() => (await Progres()).GetProperty("ultimaLectie").ValueKind.ShouldBe(JsonValueKind.Null);

    [Fact]
    public async Task ProgresulEsteAlFiecaruiElev()
    {
        using var ana = await h.InregistreazaElev("ana");
        await Promoveaza(roRomantism, "ana");
        (await Progres()).Lista("lectiiCompletate").ShouldBeEmpty();
        (await ana.GetJson("/progres")).Lista("lectiiCompletate").Single().Int("parcursId").ShouldBe(roRomantism);
    }

    [Fact]
    public async Task DoarEleviiAuPaginaDeProgres()
    {
        using var irina = await h.Login("irina");
        await irina.Get("/progres").Status(HttpStatusCode.Forbidden);
        await h.Anonim().Get("/progres").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LectiaNepublicataNuConteazaLaTotal()
    {
        await Promoveaza(roRomantism);
        await h.Db(db => db.Set<Parcurs>().Where(p => p.Id == roInterbelic).ExecuteUpdateAsync(s => s.SetProperty(p => p.Publicat, false)));
        (await george.GetJson("/progres/materii")).Lista().Single(m => m.Int("materieId") == 1).Int("totalLectii").ShouldBe(1);
    }

    [Fact]
    public async Task MateriaArataNumarulDeLectiiSiTerminate()
    {
        await Promoveaza(roInterbelic);
        var m = await george.GetJson("/materii/1");
        (m.Str("denumire"), m.Int("numarLectii"), m.Int("lectiiCompletate")).ShouldBe(("Limba și literatura română", 2, 1));
        await george.Get("/materii/999").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CapitolulAreMaterialeleSiLectia()
    {
        var c = await george.GetJson($"/cursuri/{cap2}");
        (c.Str("titlu"), c.Int("parcursId"), c.Str("parcurs"), c.Str("clasa"), c.Str("materie")).ShouldBe(("Scrisoarea I", roRomantism, "Romantismul românesc", "XI", "Limba și literatura română"));
        c.Lista("materiale").Single().Str("text").ShouldContain("1881");
        await george.Get("/cursuri/999999").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CapitoleleInEditorSuntInOrdine()
    {
        var c = (await admin.GetJson($"/continut/cursuri?parcursId={roRomantism}")).Lista();
        c.Select(x => x.Str("titlu")).ShouldBe(["Luceafărul", "Scrisoarea I"]);
        c.Select(x => x.Int("ordine")).ShouldBe([1, 2]);
        using var irina = await h.Login("irina");
        await irina.Get($"/continut/cursuri?parcursId={roRomantism}").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListaLectiilorMateriei()
    {
        var l = (await george.GetJson("/materii/1/parcursuri")).Lista();
        l.Select(x => x.Str("titlu")).ShouldBe(["Romantismul românesc", "Romanul interbelic"]);
        l[0].Int("numarCapitole").ShouldBe(2);
        (await george.GetJson("/materii/3/parcursuri")).Lista().Single().Str("clasa").ShouldBe("Bac");
    }

    [Fact]
    public async Task MateriileSuntPePaginiDoarCandSeCere()
    {
        await Promoveaza(roRomantism);
        var toate = await george.GetJson("/materii");
        toate.Int("total").ShouldBe(toate.Lista().Count);
        var p1 = await george.GetJson("/materii?marime=2");
        (p1.Int("total"), p1.Lista().Count).ShouldBe((toate.Int("total"), 2));
        var p2 = await george.GetJson("/materii?marime=2&pagina=2");
        p2.Lista().Select(m => m.Int("id")).Intersect(p1.Lista().Select(m => m.Int("id"))).ShouldBeEmpty();
        // The counts are the same paged or not.
        var ro = toate.Lista().Single(m => m.Int("id") == 1);
        var roPagina = p1.Lista().Concat(p2.Lista()).Single(m => m.Int("id") == 1);
        (roPagina.Int("numarLectii"), roPagina.Int("lectiiCompletate")).ShouldBe((ro.Int("numarLectii"), ro.Int("lectiiCompletate")));
        (ro.Int("numarLectii"), ro.Int("lectiiCompletate")).ShouldBe((2, 1));
        await george.Get("/materii?marime=0").Status(HttpStatusCode.BadRequest);
    }
}

