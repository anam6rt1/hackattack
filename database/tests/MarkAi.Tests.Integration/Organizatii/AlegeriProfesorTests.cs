using System.Net;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>
/// A professor picks at most four subjects and four organisations (sign-up, Setări, later join requests), and the pickers
/// search on the server: organisations by name, county or locality, subjects by name.
/// </summary>
public sealed class AlegeriProfesorTests : TestOrganizatii
{
    private async Task<int[]> OrganizatiiAprobate(int cate)
    {
        var id = new List<int>();
        for (var i = 0; i < cate; i++) id.Add((await OrganizatieNoua($"org{i}", $"Școala Alba {i}", aprobata: true)).Organizatie);
        return [.. id];
    }

    [Fact]
    public async Task InregistrareaCuCinciOrganizatiiEsteRefuzata()
    {
        var org = await OrganizatiiAprobate(4);
        (await h.Anonim().PostAsync("/autentificare/inregistrare-profesor", FormularProfesor("paul", [Liceu, .. org]), Ct).Mesaj(HttpStatusCode.BadRequest))
            .ShouldContain("cel mult 4 organizații");
    }

    [Fact]
    public async Task InregistrareaCuPatruOrganizatiiEstePrimita()
    {
        var org = await OrganizatiiAprobate(3);
        var (paul, _) = await ProfesorNou("paul", [Liceu, .. org]);
        (await paul.GetJson("/utilizatori/me/organizatii")).Lista().Count.ShouldBe(4);
    }

    [Fact]
    public async Task InregistrareaCuCinciMateriiEsteRefuzata()
    {
        var f = FormularProfesor("paul", Liceu);
        foreach (var m in new[] { 2, 3, 4, 5 }) f.Add(new StringContent(m.ToString()), "MaterieIds");
        (await h.Anonim().PostAsync("/autentificare/inregistrare-profesor", f, Ct).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("cel mult 4 materii");
    }

    [Fact]
    public async Task SetarileRefuzaCinciMaterii()
    {
        using var irina = await h.Login("irina");
        (await irina.Patch("/utilizatori/me/setari", new { nume = "Irina Marin", username = "irina", telefon = "0722123456", descriere = "x", materieIds = new[] { 1, 2, 3, 4, 5 } })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("cel mult 4 materii");
    }

    [Fact]
    public async Task ACinceaCerereDeProfesorEsteRefuzataPanaIeseDintrUna()
    {
        var org = await OrganizatiiAprobate(4);
        var (paul, _) = await ProfesorNou("paul", [Liceu, .. org[..3]]);
        (await paul.Post("/utilizatori/me/organizatii", new { organizatieId = org[3] }).Mesaj(HttpStatusCode.Conflict)).ShouldContain("cel mult 4 organizații");
        await paul.Delete($"/utilizatori/me/organizatii/{org[0]}").Status(HttpStatusCode.OK);
        await paul.Post("/utilizatori/me/organizatii", new { organizatieId = org[3] }).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CerereaRespinsaNuOcupaLoc()
    {
        var org = await OrganizatiiAprobate(4);
        var (paul, paulId) = await ProfesorNou("paul", [Liceu, .. org[..3]]);
        using var director = await h.Login("director");
        await director.Post($"/organizatia-mea/membri/{paulId}/respingere", new { motiv = "Nu predă aici." }).Status(HttpStatusCode.NoContent);
        await paul.Post("/utilizatori/me/organizatii", new { organizatieId = org[3] }).Status(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("sectorul", new[] { "Liceul de Test" })]             // the locality
    [InlineData("bucuresti", new[] { "Liceul de Test" })]            // the county
    [InlineData("alba iulia", new[] { "Școala Alba 0", "Școala Alba 1" })]
    [InlineData("scoala alba 1", new[] { "Școala Alba 1" })]          // the name, without diacritics
    [InlineData("zzz", new string[0])]
    public async Task OrganizatiilePublicePotFiCautateDupaNumeJudetSauLocalitate(string q, string[] gasite)
    {
        await OrganizatiiAprobate(2);
        var r = await h.Anonim().GetJson("/organizatii?q=" + Uri.EscapeDataString(q));
        r.Lista().Select(o => o.Str("nume")).ShouldBe(gasite);
        r.Int("total").ShouldBe(gasite.Length);
    }

    [Fact]
    public async Task CautareaPublicaTineContDeJudet()
    {
        await OrganizatiiAprobate(1);
        (await h.Anonim().GetJson("/organizatii?judetId=40&q=alba")).Lista().ShouldBeEmpty();
        (await h.Anonim().GetJson("/organizatii?judetId=1&q=alba")).Lista().Select(o => o.Str("nume")).ShouldBe(["Școala Alba 0"]);
    }

    [Fact]
    public async Task OrganizatiileNeaprobateNuApar()
    {
        await OrganizatieNoua("ovidiu", "Centrul Neaprobat");
        (await h.Anonim().GetJson("/organizatii?q=neaprobat")).Int("total").ShouldBe(0);
    }

    [Theory]
    [InlineData("mate", new[] { "Matematică" })]
    [InlineData("ROMANA", new[] { "Limba și literatura română" })]
    [InlineData("a", new[] { "Limba și literatura română", "Matematică", "Biologie" })] // one letter: no search
    public async Task MateriileSePotCautaDupaDenumire(string q, string[] gasite) =>
        (await h.Anonim().GetJson("/materii?q=" + q)).Lista().Select(m => m.Str("denumire")).ShouldBe(gasite, ignoreOrder: true);
}
