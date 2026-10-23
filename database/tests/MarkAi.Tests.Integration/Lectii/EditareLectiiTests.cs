using System.Net;
using MarkAi.Api.Servicii;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MarkAi.Tests.Integration.Lectii;

/// <summary>Who edits a Lecție: its author, its home organisation's admin, platform admins. Shares never give edit rights.</summary>
public sealed class EditareLectiiTests : TestLectii
{
    private static object Editare => Lectie("Ion — organizație (editat)", Liceu, clasa: "XI", vizibilitate: "organizatie");

    [Fact]
    public async Task AltProfesorNuPoateEditaLectia()
    {
        var (l, _, _) = await LectiePrivata();
        await petra.Put($"/parcursuri/{l}", Editare).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AltProfesorNuPoateAdaugaCapitole()
    {
        var (l, _, _) = await LectiePrivata();
        await petra.Post("/cursuri", new { parcursId = l, titlu = "Intrus" }).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AltProfesorNuPoateEditaCapitolele()
    {
        var (l, c, _) = await LectiePrivata();
        await petra.Put($"/cursuri/{c}", new { parcursId = l, titlu = "Intrus" }).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AltProfesorNuPoateStergeCapitole()
    {
        var (_, c, _) = await LectiePrivata();
        await petra.Delete($"/cursuri/{c}").Status(HttpStatusCode.Forbidden);
        (await h.Db(db => db.Curs.AnyAsync(x => x.Id == c))).ShouldBeTrue();
    }

    [Fact]
    public async Task AltProfesorNuPoateDeschideLectiaInEditor()
    {
        var (l, _, _) = await LectiePrivata();
        await petra.Get($"/continut/parcursuri/{l}").Status(HttpStatusCode.Forbidden);
        await petra.Get($"/continut/cursuri?parcursId={l}").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CelCareNuOVedePrimeste404NuForbidden()
    {
        var (l, _, _) = await LectiePrivata();
        await ovidiu.Get($"/continut/parcursuri/{l}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AutorulEditeazaLectia()
    {
        var (l, _, _) = await LectiePrivata();
        (await irina.Put($"/parcursuri/{l}", Editare).Json()).Str("titlu").ShouldBe("Ion — organizație (editat)");
    }

    [Fact]
    public async Task AdministratorulOrganizatieiEditeazaLectia()
    {
        var (l, _, _) = await LectiePrivata();
        await director.Put($"/parcursuri/{l}", Editare).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminulPlatformeiEditeazaOriceLectie()
    {
        var (l, _, _) = await LectiePrivata();
        await admin.Put($"/parcursuri/{l}", Editare).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListaEditoruluiAreDoarCeEditeaza()
    {
        var (l, _, _) = await LectiePrivata();
        (await Ids(director, "/continut/parcursuri")).ShouldContain(l);
        (await Ids(petra, "/continut/parcursuri")).ShouldNotContain(l);
        (await Ids(ovidiu, "/continut/parcursuri")).ShouldNotContain(l);
        (await Ids(admin, "/continut/parcursuri")).ShouldContain(l);
    }

    [Fact]
    public async Task ListaEditoruluiSeFiltreazaPeMaterie()
    {
        var (l, _, _) = await LectiePrivata();
        (await Ids(admin, "/continut/parcursuri?materieId=2")).ShouldNotContain(l);
        (await Ids(admin, "/continut/parcursuri?materieId=1")).ShouldContain(l);
    }

    [Fact]
    public async Task DoarAutorulMutaLectiaInAltaOrganizatie()
    {
        var (l, _, _) = await LectiePrivata();
        (await director.Put($"/parcursuri/{l}", Lectie("Mutată", Centru, clasa: "XI")).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Doar autorul");
        await irina.Put($"/parcursuri/{l}", Lectie("Mutată", Centru, clasa: "XI")).Status(HttpStatusCode.OK);
        (await Editor(irina, l)).Int("organizatieId").ShouldBe(Centru);
    }

    [Fact]
    public async Task EditorulSpuneCineOPoateMuta()
    {
        var (l, _, _) = await LectiePrivata();
        (await Editor(irina, l)).Bool("poateMuta").ShouldBeTrue();
        (await Editor(director, l)).Bool("poateMuta").ShouldBeFalse();
    }

    [Fact]
    public async Task ProfesoriiNuEditeazaLectiiDePlatforma()
    {
        var p = await LectiePlatforma();
        await petra.Put($"/parcursuri/{p}", Lectie("x", Liceu)).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CapitolulNuSeMutaIntrOLectieNeeditabila()
    {
        var (l, _, _) = await LectiePrivata();
        var p = await LectiePlatforma();
        var c = await Capitol(irina, l, "Al treilea");
        await irina.Put($"/cursuri/{c}", new { parcursId = p, titlu = "Mutat" }).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OrdineaCereToateCapitolele()
    {
        var (l, c1, c2) = await LectiePrivata();
        var c3 = await Capitol(irina, l, "Ion: capitolul 3");
        await irina.Put($"/parcursuri/{l}/ordine", new { cursIds = new[] { c3, c1 } }).Status(HttpStatusCode.BadRequest);
        await irina.Put($"/parcursuri/{l}/ordine", new { cursIds = new[] { c3, c1, c1 } }).Status(HttpStatusCode.BadRequest);
        await irina.Put($"/parcursuri/{l}/ordine", new { cursIds = new[] { c3, c1, c2, 99999 } }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CapitoleleSeReordoneaza()
    {
        var (l, c1, c2) = await LectiePrivata();
        var c3 = await Capitol(irina, l, "Ion: capitolul 3");
        await irina.Put($"/parcursuri/{l}/ordine", new { cursIds = new[] { c3, c1, c2 } }).Status(HttpStatusCode.NoContent);
        (await elena.GetJson($"/parcursuri/{l}")).Lista("capitole").Select(c => c.Int("id")).ShouldBe([c3, c1, c2]);
    }

    [Fact]
    public async Task AltProfesorNuReordoneaza()
    {
        var (l, c1, c2) = await LectiePrivata();
        await petra.Put($"/parcursuri/{l}/ordine", new { cursIds = new[] { c2, c1 } }).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LectiaAutoruluiStersRamaneLaOrganizatie()
    {
        var aPetrei = await Creeaza(petra, Lectie("Lecția Petrei", Liceu));
        await using (var scope = h.App.Services.CreateAsyncScope())
            (await scope.ServiceProvider.GetRequiredService<StergereCont>().StergeAsync(await Id("petra"), null, Ct)).ShouldBeTrue();
        (await h.Db(db => db.Set<Parcurs>().AnyAsync(p => p.Id == aPetrei && p.AutorId == null))).ShouldBeTrue();
        await director.Put($"/parcursuri/{aPetrei}", Lectie("Lecția Petrei", Liceu)).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProfesorulScosDinOrganizatieRamaneAutorulLectieiSale()
    {
        // Plan 2: a professor removed from the organisation stays approved and remains the author; the Lecție stays with the
        // organisation, whose admin can still edit it.
        var aPetrei = await Creeaza(petra, Lectie("Lecția Petrei", Liceu));
        await director.Delete($"/organizatia-mea/membri/{await Id("petra")}").Status(HttpStatusCode.NoContent);
        (await petra.Put($"/parcursuri/{aPetrei}", Lectie("Lecția Petrei, revizuită", Liceu)).Json()).Int("organizatieId").ShouldBe(Liceu);
        await director.Put($"/parcursuri/{aPetrei}", Lectie("Lecția Petrei", Liceu)).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProfesorulScosNuFacePublicaOLectieDoarAOrganizatiei()
    {
        var aPetrei = await Creeaza(petra, Lectie("Doar pentru liceu", Liceu, vizibilitate: "organizatie"));
        await director.Delete($"/organizatia-mea/membri/{await Id("petra")}").Status(HttpStatusCode.NoContent);
        (await petra.Put($"/parcursuri/{aPetrei}", Lectie("Doar pentru liceu", Liceu, vizibilitate: "public")).Mesaj(HttpStatusCode.Conflict))
            .ShouldContain("Administratorul organizației o poate face publică");
        (await director.GetJson($"/continut/parcursuri/{aPetrei}")).Str("vizibilitate").ShouldBe("organizatie");
        // She can still edit it as organisation-only, and the organisation's admin may make it public.
        await petra.Put($"/parcursuri/{aPetrei}", Lectie("Doar pentru liceu (2)", Liceu, vizibilitate: "organizatie")).Status(HttpStatusCode.OK);
        (await director.Put($"/parcursuri/{aPetrei}", Lectie("Doar pentru liceu (2)", Liceu, vizibilitate: "public")).Json()).Str("vizibilitate").ShouldBe("public");
    }

    [Fact]
    public async Task UnMembruAlOrganizatieiFacePublicaOLectieDoarAOrganizatiei()
    {
        var aPetrei = await Creeaza(petra, Lectie("Doar pentru liceu", Liceu, vizibilitate: "organizatie"));
        (await petra.Put($"/parcursuri/{aPetrei}", Lectie("Doar pentru liceu", Liceu, vizibilitate: "public")).Json()).Str("vizibilitate").ShouldBe("public");
    }
}
