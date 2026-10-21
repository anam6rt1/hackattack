using System.Net;

namespace MarkAi.Tests.Integration.Cautare;

/// <summary>"Near me" for Lecții: the home organisation's county; the user's own locality first; platform Lecții have no place.</summary>
public sealed class FiltruLocalTests : TestCautare
{
    private int platforma, liceu, dinAlbaIulia, dinBarabant;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        platforma = await Creeaza(admin, Lectie("Lecție de platformă", null));
        liceu = await Creeaza(irina, Lectie("Lecție din liceu", Liceu));
        dinAlbaIulia = await Creeaza(ovidiu, Lectie("Lecție din Alba Iulia", Centru));
        dinBarabant = await Creeaza(ovidiu, Lectie("Lecție din Bărăbanț", ScoalaBarabant));
    }

    [Fact]
    public async Task InBucurestiDoarLectiileLiceului()
    {
        (await Lectii(elena, "/materii/1/parcursuri?filtru=local")).Select(l => l.Id).ShouldBe([liceu]);
    }

    [Fact]
    public async Task InAlbaDoarLectiileDinAlba()
    {
        (await Lectii(radu, "/materii/1/parcursuri?filtru=local")).Select(l => l.Id).Order().ShouldBe(new[] { dinAlbaIulia, dinBarabant }.Order());
    }

    [Fact]
    public async Task LocalitateaProprieEstePrimaSiMarcata()
    {
        var l = await Lectii(radu, "/materii/1/parcursuri?filtru=local");
        l[0].Id.ShouldBe(dinAlbaIulia);
        l[0].InLocalitateaTa.ShouldBeTrue();
        l[1].InLocalitateaTa.ShouldBeFalse();
    }

    [Fact]
    public async Task LectiaArataLoculOrganizatiei()
    {
        (await Lectii(radu, "/materii/1/parcursuri")).Single(l => l.Id == dinAlbaIulia).Loc.ShouldBe("Alba Iulia, Alba");
    }

    [Fact]
    public async Task LectiaDePlatformaNuAreLoc()
    {
        (await Lectii(radu, "/materii/1/parcursuri")).Single(l => l.Id == platforma).Loc.ShouldBeNull();
    }

    [Fact]
    public async Task FaraJudetLocalCereSetarile()
    {
        (await george.Get("/materii/1/parcursuri?filtru=local").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Setări");
    }

    [Fact]
    public async Task CautareaAreSiFiltrulLocal()
    {
        (await Cauta(radu, "lectie din", "&filtru=local")).Lectii.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CautareaLocalaPuneLocalitateaProprieIntai()
    {
        (await Cauta(radu, "lectie din", "&filtru=local")).Lectii[0].ShouldBe(dinAlbaIulia);
    }

    [Fact]
    public async Task SchimbareaLoculuiSchimbaRezultatele()
    {
        await george.Put("/utilizatori/me/setari/loc", new { judetId = Alba, localitateId = Barabant, clasa = "XI" }).Status(HttpStatusCode.OK);
        var l = await Lectii(george, "/materii/1/parcursuri?filtru=local");
        l[0].Id.ShouldBe(dinBarabant);
        l[0].InLocalitateaTa.ShouldBeTrue();
    }

    [Fact]
    public async Task DoarJudetulFaraLocalitateMergeFaraMarcaj()
    {
        await george.Put("/utilizatori/me/setari/loc", new { judetId = Alba, localitateId = (int?)null, clasa = (string?)null });
        (await Lectii(george, "/materii/1/parcursuri?filtru=local")).ShouldAllBe(l => !l.InLocalitateaTa);
    }

    [Fact]
    public async Task LocalSePoateCombinaCuClasa()
    {
        var a = await Creeaza(ovidiu, Lectie("A noua din Alba", Centru, clasa: "IX"));
        (await Lectii(radu, "/materii/1/parcursuri?filtru=local&clasa=IX")).Select(l => l.Id).ShouldBe([a]);
    }
}
