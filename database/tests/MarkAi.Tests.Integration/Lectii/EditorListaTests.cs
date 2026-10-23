using System.Net;
using System.Text.Json;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Lectii;

/// <summary>
/// The editor's list of Lecții (GET /continut/parcursuri): 20 a page with the total, the subject filter in SQL, and every Lecție
/// on a page built the same as on its own (GET /continut/parcursuri/{id}) — also with several authors on one page, where the
/// per-author details (home organisations, the publishing switch) are loaded once for the whole page.
/// </summary>
public sealed class EditorListaTests : TestLectii
{
    private Task MulteLectii(int cate, int materieId = 1) => h.Db(async db =>
    {
        var admin = await db.Utilizatori.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync();
        for (var i = 0; i < cate; i++)
            db.Add(new Parcurs { MaterieId = materieId, Titlu = $"Lecția {materieId}-{i:00}", Clasa = "Bac", Vizibilitate = Parcurs.Public, AutorId = admin, CreatLa = DateTime.UtcNow, Ordine = i });
        await db.SaveChangesAsync();
    });

    private static IEnumerable<string> Titluri(JsonElement lista) => lista.Lista().Select(l => l.Str("titlu"));

    [Fact]
    public async Task ListaArePagini()
    {
        await MulteLectii(25);
        var p1 = await admin.GetJson("/continut/parcursuri");
        var total = p1.Int("total");
        total.ShouldBeGreaterThanOrEqualTo(25);
        p1.Lista().Count.ShouldBe(20);
        var p2 = await admin.GetJson("/continut/parcursuri?pagina=2");
        Titluri(p2).Intersect(Titluri(p1)).ShouldBeEmpty();
        var toate = new List<string>();
        for (var pag = 1; pag <= (total + 19) / 20; pag++) toate.AddRange(Titluri(await admin.GetJson($"/continut/parcursuri?pagina={pag}")));
        toate.Distinct().Count().ShouldBe(total);
        await admin.Get("/continut/parcursuri?pagina=0").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FiltrulDeMaterieEsteInSql()
    {
        await MulteLectii(3, materieId: 2);
        var r = await admin.GetJson("/continut/parcursuri?materieId=2");
        r.Int("total").ShouldBe(r.Lista().Count);
        r.Lista().ShouldAllBe(l => l.Int("materieId") == 2);
        Titluri(r).ShouldContain("Lecția 2-00");
    }

    [Fact]
    public async Task OPaginaCuMaiMultiAutoriEsteCaFiecareLectieSinguraTratata()
    {
        await director.Put($"/organizatia-mea/membri/{await Id("irina")}/publicare", new { poatePublicaPublic = false }).Status(HttpStatusCode.NoContent);
        var lectii = new[]
        {
            await Creeaza(irina, Lectie("A irinei, liceu", Liceu, vizibilitate: "organizatie")),
            await Creeaza(irina, Lectie("A irinei, centru", Centru)),
            await Creeaza(petra, Lectie("A petrei", Liceu)),
            await Creeaza(admin, Lectie("A platformei", null)),
        };
        var lista = (await admin.GetJson("/continut/parcursuri")).Lista().Where(l => lectii.Contains(l.Int("id"))).ToList();
        lista.Count.ShouldBe(4);
        lista.Single(l => l.Str("titlu") == "A platformei").Bool("poatePublicaPublic").ShouldBeTrue();
        foreach (var l in lista)
            l.GetRawText().ShouldBe((await admin.GetJson($"/continut/parcursuri/{l.Int("id")}")).GetRawText());
        lista.Single(l => l.Str("titlu") == "A irinei, liceu").Bool("poatePublicaPublic").ShouldBeFalse();
        lista.Single(l => l.Str("titlu") == "A irinei, centru").Bool("poatePublicaPublic").ShouldBeTrue();
        lista.Single(l => l.Str("titlu") == "A petrei").Bool("poatePublicaPublic").ShouldBeTrue();
    }

    [Fact]
    public async Task OrganizatiilePosibileSuntAleAutorului()
    {
        var l = await Creeaza(irina, Lectie("A irinei", Liceu));
        // For irina: her two organisations; for the platform admin looking at it: every approved organisation.
        var aIrinei = (await irina.GetJson("/continut/parcursuri")).Lista().Single(x => x.Int("id") == l);
        aIrinei.Lista("organizatiiPosibile").Select(o => o.Int("id")).Order().ShouldBe(new[] { Liceu, Centru }.Order());
        var laAdmin = (await admin.GetJson("/continut/parcursuri")).Lista().Single(x => x.Int("id") == l);
        laAdmin.Lista("organizatiiPosibile").Count.ShouldBeGreaterThanOrEqualTo(2);
        aIrinei.Bool("poateMuta").ShouldBeTrue();
        (await director.GetJson("/continut/parcursuri")).Lista().Single(x => x.Int("id") == l).Bool("poateMuta").ShouldBeFalse();
    }

    [Fact]
    public async Task UnProfesorVedeDoarCeEditeaza()
    {
        await MulteLectii(5);
        var aPetrei = await Creeaza(petra, Lectie("A petrei", Liceu));
        var r = await petra.GetJson("/continut/parcursuri");
        r.Lista().Select(l => l.Int("id")).ShouldBe([aPetrei]);
        r.Int("total").ShouldBe(1);
    }

    // ---- search and filters (in SQL)

    private async Task<List<string>> Editor(HttpClient c, string query) =>
        (await c.GetJson("/continut/parcursuri?marime=100&" + query)).Lista().Select(l => l.Str("titlu")).Order().ToList();

    private async Task LectiiIrinei()
    {
        await Creeaza(irina, Lectie("Luceafărul de Eminescu", Liceu, clasa: "XII"));
        await Creeaza(irina, Lectie("Plumb de Bacovia", Liceu, clasa: "XI", vizibilitate: "organizatie"));
        var ciorna = await Creeaza(irina, Lectie("Ion de Rebreanu", Liceu, clasa: "IX"));
        await irina.Put($"/parcursuri/{ciorna}", Lectie("Ion de Rebreanu", Liceu, clasa: "IX", publicat: false)).Status(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("q=luceafarul", new[] { "Luceafărul de Eminescu" })]
    [InlineData("q=EMINESCU", new[] { "Luceafărul de Eminescu" })]
    [InlineData("q=de", new[] { "Ion de Rebreanu", "Luceafărul de Eminescu", "Plumb de Bacovia" })]
    [InlineData("q=plumb%20bacovia", new[] { "Plumb de Bacovia" })]
    [InlineData("clasa=XI", new[] { "Plumb de Bacovia" })]
    [InlineData("vizibilitate=organizatie", new[] { "Plumb de Bacovia" })]
    [InlineData("vizibilitate=public", new[] { "Ion de Rebreanu", "Luceafărul de Eminescu" })]
    [InlineData("publicat=false", new[] { "Ion de Rebreanu" })]
    [InlineData("publicat=true&clasa=XII", new[] { "Luceafărul de Eminescu" })]
    [InlineData("q=zzz", new string[0])]
    public async Task ProfesorulCautaSiFiltreaza(string query, string[] gasite)
    {
        await LectiiIrinei();
        (await Editor(irina, query)).ShouldBe(gasite.Order());
    }

    [Fact]
    public async Task FiltreleGresiteSuntRefuzate()
    {
        await irina.Get("/continut/parcursuri?clasa=XIII").Status(HttpStatusCode.BadRequest);
        await irina.Get("/continut/parcursuri?vizibilitate=secret").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CautareaNuArataCeNuPoateEdita()
    {
        await LectiiIrinei();
        (await Editor(petra, "q=luceafarul")).ShouldBeEmpty();
    }
}

