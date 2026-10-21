using System.Net;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>The organisation admin's dashboard: details, members, counts, removing members.</summary>
public sealed class PanouOrganizatieTests : TestOrganizatii
{
    private HttpClient director = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        director = await h.Login("director");
    }

    [Fact]
    public async Task PanoulArataDetaliileSiNumerele()
    {
        await ElevNou();
        var o = await director.GetJson("/organizatia-mea");
        (o.Str("nume"), o.Str("judet"), o.Str("localitate"), o.Str("stare")).ShouldBe(("Liceul de Test", "București", "Sectorul 1", "aprobata"));
        (o.Int("profesori"), o.Int("elevi"), o.Int("cereri")).ShouldBe((2, 0, 1));
    }

    [Fact]
    public async Task DetaliileSePotSchimba()
    {
        var o = await director.Put("/organizatia-mea", new { nume = "Liceul Nou", adresa = "Str. Nouă 5", judetId = 1, localitateId = 1035 }).Json();
        (o.Str("nume"), o.Str("adresa"), o.Str("localitate")).ShouldBe(("Liceul Nou", "Str. Nouă 5", "Bărăbanț"));
    }

    [Fact]
    public async Task DetaliileGresiteSuntRefuzate()
    {
        await director.Put("/organizatia-mea", new { nume = "", adresa = "x", judetId = 40 }).Status(HttpStatusCode.BadRequest);
        await director.Put("/organizatia-mea", new { nume = "x", adresa = "x", judetId = 40, localitateId = 1026 }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CelCareNuConduceNuArePanou()
    {
        using var irina = await h.Login("irina");
        await irina.Get("/organizatia-mea").Status(HttpStatusCode.Forbidden);
        await irina.Put("/organizatia-mea", new { nume = "x", adresa = "x", judetId = 40 }).Status(HttpStatusCode.Forbidden);
        await irina.Get("/organizatia-mea/membri").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MembriiSeFiltreazaPeRol()
    {
        await ElevNou();
        (await director.GetJson("/organizatia-mea/membri?rol=elev")).Lista().ShouldAllBe(m => m.Str("rol") == "elev");
        (await director.GetJson("/organizatia-mea/membri?rol=profesor")).Lista().Count.ShouldBe(2);
    }

    [Fact]
    public async Task CererileInAsteptareSuntPrimele()
    {
        await ElevNou();
        var membri = (await director.GetJson("/organizatia-mea/membri")).Lista();
        membri[0].Str("stare").ShouldBe("in_asteptare");
    }

    [Fact]
    public async Task ElevulScosNuMaiEsteMembru()
    {
        var (c, id) = await ElevNou();
        await director.Post($"/organizatii/{Liceu}/elevi/{id}/aprobare");
        await director.Delete($"/organizatia-mea/membri/{id}").Status(HttpStatusCode.NoContent);
        (await c.GetJson("/utilizatori/me/organizatii")).Lista().ShouldBeEmpty();
    }

    [Fact]
    public async Task ScoatereaUnuiNemembruEste404()
    {
        await director.Delete("/organizatia-mea/membri/99999").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OrganizatiilePredateAleProfesorului()
    {
        using var irina = await h.Login("irina");
        (await irina.GetJson("/utilizatori/me/organizatii-predate")).Lista().Single().Int("id").ShouldBe(Liceu);
        using var george = await h.Login("george");
        (await george.GetJson("/utilizatori/me/organizatii-predate")).Lista().ShouldBeEmpty();
    }

    [Fact]
    public async Task DovadaUnuiElevNuExista()
    {
        var (_, id) = await ElevNou();
        await director.Get($"/organizatia-mea/membri/{id}/dovada").Status(HttpStatusCode.NotFound);
    }
}
