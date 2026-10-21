using System.Net;
using System.Text.Json;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Lectii;

/// <summary>Who sees a Lecție, on every read path: lists, counts, details, chapters, quiz, progress.</summary>
public sealed class VizibilitateTests : TestLectii
{
    [Fact]
    public async Task CelDinAfaraPrimeste404PeLectie()
    {
        var (l, _, _) = await LectiePrivata();
        await george.Get($"/parcursuri/{l}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CelDinAfaraPrimeste404PeCapitol()
    {
        var (_, c, _) = await LectiePrivata();
        await george.Get($"/cursuri/{c}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CelDinAfaraNuPoateDaTestul()
    {
        var (l, _, _) = await LectiePrivata();
        await george.Post($"/parcursuri/{l}/quiz").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CelDinAfaraNuPoateMarcaProgres()
    {
        var (_, c, _) = await LectiePrivata();
        await george.Post($"/cursuri/{c}/acces").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListaCeluiDinAfaraAreDoarLectiiPublice()
    {
        var (l, _, _) = await LectiePrivata();
        var p = await LectiePlatforma();
        var lista = await Ids(george, "/materii/1/parcursuri");
        lista.ShouldContain(p);
        lista.ShouldNotContain(l);
    }

    [Fact]
    public async Task NumarareaUrmeazaVizibilitatea()
    {
        await LectiePrivata();
        await LectiePlatforma();
        (await NumarLectii(george, 1)).ShouldBe(1);
        (await NumarLectii(elena, 1)).ShouldBe(2);
        (await NumarLectii(h.Anonim(), 1)).ShouldBe(1);
    }

    [Fact]
    public async Task ElevulOrganizatieiVedeLectiaSiCapitolele()
    {
        var (l, c1, c2) = await LectiePrivata();
        await elena.Get($"/parcursuri/{l}").Status(HttpStatusCode.OK);
        await elena.Get($"/cursuri/{c1}").Status(HttpStatusCode.OK);
        await elena.Get($"/cursuri/{c2}").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ElevulAltreiOrganizatiiNuVede()
    {
        var (l, _, _) = await LectiePrivata();
        await radu.Get($"/parcursuri/{l}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ElevulCuCerereInAsteptareNuVede()
    {
        var (l, _, _) = await LectiePrivata();
        await george.Post("/utilizatori/me/organizatii", new { organizatieId = Liceu }).Status(HttpStatusCode.OK);
        await george.Get($"/parcursuri/{l}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ElevulAprobatIncepeSaVada()
    {
        var (l, _, _) = await LectiePrivata();
        await george.Post("/utilizatori/me/organizatii", new { organizatieId = Liceu });
        await director.Post($"/organizatii/{Liceu}/elevi/{await Id("george")}/aprobare").Status(HttpStatusCode.NoContent);
        await george.Get($"/parcursuri/{l}").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ElevulCareIeseNuMaiVede()
    {
        var (l, _, _) = await LectiePrivata();
        await elena.Delete($"/utilizatori/me/organizatii/{Liceu}").Status(HttpStatusCode.OK);
        await elena.Get($"/parcursuri/{l}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CapitoleleSeDeschidInOriceOrdine()
    {
        var (l, _, c2) = await LectiePrivata();
        await elena.Post($"/cursuri/{c2}/acces").Status(HttpStatusCode.NoContent);
        await elena.Post($"/parcursuri/{l}/quiz").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProgresulPeOLectieInvizibilaEsteAscuns()
    {
        var (l, c1, _) = await LectiePrivata();
        await LectiePlatforma();
        var georgeId = await Id("george");
        await h.Db(async db =>
        {
            db.Add(new ProgresLectie { UtilizatorId = georgeId, ParcursId = l, UltimulCursId = c1, PromovatLa = DateTime.UtcNow, UltimaAccesare = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        var progres = await george.GetJson("/progres");
        progres.Lista("lectiiCompletate").ShouldNotContain(c => c.Int("parcursId") == l);
        progres.GetProperty("ultimaLectie").ValueKind.ShouldBe(JsonValueKind.Null);
        progres.Lista("materii").Single(m => m.Int("materieId") == 1).Int("totalLectii").ShouldBe(1);
    }

    [Fact]
    public async Task LectiaNepublicataODoarEditorii()
    {
        var l = await Creeaza(irina, Lectie("Ciornă", Centru, clasa: "XII", publicat: false));
        await radu.Get($"/parcursuri/{l}").Status(HttpStatusCode.NotFound);
        await irina.Get($"/parcursuri/{l}").Status(HttpStatusCode.OK);
        await ovidiu.Get($"/parcursuri/{l}").Status(HttpStatusCode.OK);
        await admin.Get($"/parcursuri/{l}").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CapitolulNepublicatEsteAscunsElevilor()
    {
        var l = await LectiePlatforma();
        var c = (await admin.Post("/cursuri", new { parcursId = l, titlu = "Ascuns", publicat = false }).Json(HttpStatusCode.Created)).Int("id");
        await george.Get($"/cursuri/{c}").Status(HttpStatusCode.NotFound);
        (await george.GetJson($"/parcursuri/{l}")).Lista("capitole").ShouldNotContain(x => x.Int("id") == c);
        (await admin.GetJson($"/parcursuri/{l}")).Lista("capitole").ShouldContain(x => x.Int("id") == c);
    }

    [Fact]
    public async Task AnonimulVedeMateriileDarNuLectiile()
    {
        await LectiePlatforma();
        await h.Anonim().Get("/materii").Status(HttpStatusCode.OK);
        await h.Anonim().Get("/materii/1/parcursuri").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DetaliulSpuneDacaPoateEdita()
    {
        var (l, _, _) = await LectiePrivata();
        (await irina.GetJson($"/parcursuri/{l}")).Bool("poateEdita").ShouldBeTrue();
        (await elena.GetJson($"/parcursuri/{l}")).Bool("poateEdita").ShouldBeFalse();
        (await director.GetJson($"/parcursuri/{l}")).Bool("poateEdita").ShouldBeTrue();
    }

    [Fact]
    public async Task MateriaInexistentaEste404()
    {
        await george.Get("/materii/999").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OrganizatiaRespinsaNuDaAcces()
    {
        var (l, _, _) = await LectiePrivata();
        await h.Db(db => db.Set<Organizatie>().Where(o => o.Id == Liceu).ExecuteUpdateAsync(s => s.SetProperty(o => o.Stare, Organizatie.Respinsa)));
        await elena.Get($"/parcursuri/{l}").Status(HttpStatusCode.NotFound);
    }
}
