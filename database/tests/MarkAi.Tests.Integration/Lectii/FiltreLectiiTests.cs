using System.Net;
using MarkAi.Api.Servicii.Triviador;
using MarkAi.Api.Infrastructura;

namespace MarkAi.Tests.Integration.Lectii;

/// <summary>The Lecții list filters (grade, organisation, public) and the Triviador grade filter.</summary>
public sealed class FiltreLectiiTests : TestLectii
{
    private int privata, platforma, publica;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        (privata, _, _) = await LectiePrivata();
        platforma = await LectiePlatforma();
        publica = await Creeaza(irina, Lectie("Luceafărul pentru toți", Liceu, clasa: "XII"));
        await Capitol(irina, publica, "Luceafărul: capitolul 1");
    }

    [Fact]
    public async Task FiltrulPeOClasa()
    {
        var xi = await Ids(elena, "/materii/1/parcursuri?clasa=XI");
        xi.ShouldBe([privata]);
    }

    [Fact]
    public async Task FiltrulPeMaiMulteClase()
    {
        var lista = await Ids(elena, "/materii/1/parcursuri?clasa=XII,Bac");
        lista.ShouldContain(platforma);
        lista.ShouldContain(publica);
        lista.ShouldNotContain(privata);
    }

    [Fact]
    public async Task FiltrulAcceptaSpatii()
    {
        (await Ids(elena, "/materii/1/parcursuri?clasa=XII%2C%20Bac")).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ClasaNecunoscutaEsteRefuzata()
    {
        await elena.Get("/materii/1/parcursuri?clasa=XIII").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FiltrulOrganizatiaMea()
    {
        var lista = await Ids(elena, "/materii/1/parcursuri?filtru=organizatia-mea");
        lista.ShouldContain(privata);
        lista.ShouldContain(publica);
        lista.ShouldNotContain(platforma);
    }

    [Fact]
    public async Task OrganizatiaMeaIncludePartajarile()
    {
        await irina.Put($"/parcursuri/{privata}", Lectie("Ion", Liceu, clasa: "XI", vizibilitate: "organizatie", partajari: [Centru]));
        (await Ids(radu, "/materii/1/parcursuri?filtru=organizatia-mea")).ShouldContain(privata);
    }

    [Fact]
    public async Task FiltrulPublic()
    {
        var lista = await Ids(elena, "/materii/1/parcursuri?filtru=public");
        lista.ShouldNotContain(privata);
        lista.ShouldContain(platforma);
    }

    [Fact]
    public async Task FaraOrganizatieOrganizatiaMeaEsteGoala()
    {
        (await Ids(george, "/materii/1/parcursuri?filtru=organizatia-mea")).ShouldBeEmpty();
    }

    [Fact]
    public async Task FiltrulNecunoscutEsteRefuzat()
    {
        (await elena.Get("/materii/1/parcursuri?filtru=altceva").Mesaj(HttpStatusCode.BadRequest)).ShouldBe(FiltreLectii.Mesaj);
    }

    [Fact]
    public async Task FiltreleSeCombina()
    {
        (await Ids(elena, "/materii/1/parcursuri?filtru=organizatia-mea&clasa=XII")).ShouldBe([publica]);
    }

    [Fact]
    public async Task RezumatulArataCapitoleleSiTerminarea()
    {
        var r = (await elena.GetJson("/materii/1/parcursuri")).Lista().Single(x => x.Int("id") == privata);
        r.Int("numarCapitole").ShouldBe(2);
        r.Bool("completata").ShouldBeFalse();
    }

    [Fact]
    public async Task TriviadorCuClasaXiFolosesteDoarLectiiDeXi()
    {
        var motor = h.Serviciu<MotorTriviador>();
        var cod = await motor.CreeazaAsync(await Id("george"), new SetariCamera([1], 9, 2, "XI"), Ct);
        var camera = motor.Descriere(cod)!.Value;
        camera.Insule.ShouldContain("Ion — organizație");
        camera.Insule.ShouldNotContain("Platformă Bac");
        camera.Insule.ShouldNotContain("Luceafărul pentru toți");
        camera.Materie.ShouldEndWith("clasa a XI-a");
    }

    [Fact]
    public async Task TriviadorRefuzaOClasaNecunoscuta()
    {
        await Should.ThrowAsync<CerereInvalida>(() => h.Serviciu<MotorTriviador>().CreeazaAsync(1, new SetariCamera([1], 9, 2, "XIII"), Ct));
    }
}
