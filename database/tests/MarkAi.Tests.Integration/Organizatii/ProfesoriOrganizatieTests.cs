using System.Net;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>Professors are approved by the admin of each organisation they ask to join; approval anywhere makes them approved.</summary>
public sealed class ProfesoriOrganizatieTests : TestOrganizatii
{
    private HttpClient director = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        director = await h.Login("director");
    }

    [Fact]
    public async Task ProfesorulTrebuieSaAlegaOOrganizatie()
    {
        await h.Anonim().PostAsync("/autentificare/inregistrare-profesor", FormularProfesor("paul2"), Ct).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OrganizatiaNeaprobataNuPoateFiAleasa()
    {
        var (_, alba) = await OrganizatieNoua();
        await h.Anonim().PostAsync("/autentificare/inregistrare-profesor", FormularProfesor("paul2", alba), Ct).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdministratorulAflaDeCerere()
    {
        await ProfesorNou();
        await Proceseaza();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "director@example.invalid" && m.Subiect.Contains("profesor"));
    }

    [Fact]
    public async Task ProfesorulInAsteptareNuPreda()
    {
        var (c, _) = await ProfesorNou();
        await c.Get("/profesori/me").Status(HttpStatusCode.Forbidden);
        (await c.GetJson("/utilizatori/me")).Str("stareProfesor").ShouldBe("in_asteptare");
    }

    [Fact]
    public async Task AdministratorulVedeCerereaCuDovada()
    {
        var (_, id) = await ProfesorNou();
        var m = (await director.GetJson("/organizatia-mea/membri?stare=in_asteptare")).Lista().Single(x => x.Int("utilizatorId") == id);
        m.Bool("areDovada").ShouldBeTrue();
        m.Lista("materii").Select(x => x.GetString()).ShouldBe(["Limba și literatura română"]);
        m.Str("descriere").ShouldBe("Predau de mulți ani.");
    }

    [Fact]
    public async Task AdministratorulAltreiOrganizatiiNuDecide()
    {
        var (_, id) = await ProfesorNou();
        var (ovidiu, _) = await OrganizatieNoua(aprobata: true);
        await ovidiu.Post($"/organizatia-mea/membri/{id}/aprobare").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnProfesorAprobatNuDecideProfesori()
    {
        var (_, id) = await ProfesorNou();
        using var irina = await h.Login("irina");
        await irina.Post($"/organizatia-mea/membri/{id}/aprobare").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AprobareaIlLasaSaPredea()
    {
        var (c, id) = await ProfesorNou();
        await director.Post($"/organizatia-mea/membri/{id}/aprobare").Status(HttpStatusCode.NoContent);
        await c.Get("/profesori/me").Status(HttpStatusCode.OK);
        await Proceseaza();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "paul@example.invalid" && m.Subiect.Contains("a fost aprobată"));
    }

    [Fact]
    public async Task AdminiiPlatformeiNuMaiAprobaProfesori()
    {
        var (_, id) = await ProfesorNou();
        using var admin = await h.Login("admin");
        (await admin.Post($"/admin/profesori/{id}/aprobare", new { onorariuNegociat = 50 })).StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task RespingereaCuMotivIlFaceRespins()
    {
        var (c, id) = await ProfesorNou();
        await director.Post($"/organizatia-mea/membri/{id}/respingere", new { motiv = "Nu predăm această materie." }).Status(HttpStatusCode.NoContent);
        var eu = await c.GetJson("/utilizatori/me");
        eu.Str("stareProfesor").ShouldBe("respins");
        eu.Str("motivRespingere").ShouldBe("Nu predăm această materie.");
    }

    [Fact]
    public async Task RespingereaFaraMotivAreUnMotivImplicit()
    {
        var (c, id) = await ProfesorNou();
        await director.Post($"/organizatia-mea/membri/{id}/respingere", new { motiv = (string?)null });
        (await c.GetJson("/utilizatori/me/organizatii")).Lista().Single().Str("motivRespingere").ShouldBe("Cererea nu a fost aprobată.");
    }

    [Fact]
    public async Task ProfesorulCereSiInAltaOrganizatie()
    {
        var (c, id) = await ProfesorNou();
        var (_, alba) = await OrganizatieNoua(aprobata: true);
        await c.Post("/utilizatori/me/organizatii", new { organizatieId = alba }).Status(HttpStatusCode.OK);
        (await c.GetJson("/utilizatori/me/organizatii")).Lista().Count.ShouldBe(2);
    }

    [Fact]
    public async Task NiciOCerereDubla()
    {
        var (c, _) = await ProfesorNou();
        (await c.Post("/utilizatori/me/organizatii", new { organizatieId = Liceu }).Mesaj(HttpStatusCode.Conflict)).ShouldContain("deja o cerere");
    }

    [Fact]
    public async Task RefuzulIntrOOrganizatiePastreazaAprobareaDinAlta()
    {
        var (c, id) = await ProfesorNou();
        var (ovidiu, alba) = await OrganizatieNoua(aprobata: true);
        await director.Post($"/organizatia-mea/membri/{id}/aprobare");
        await c.Post("/utilizatori/me/organizatii", new { organizatieId = alba });
        await ovidiu.Post($"/organizatia-mea/membri/{id}/respingere", new { motiv = "Nu." });
        (await c.GetJson("/utilizatori/me")).Str("stareProfesor").ShouldBe("aprobat");
    }

    [Fact]
    public async Task ProfesorulScosDinUnicaOrganizatieRamaneAprobat()
    {
        // Plan 2: an approval stays after leaving or being removed; only the platform admin withdraws it (RetragereProfesorTests).
        var (c, id) = await ProfesorNou();
        await director.Post($"/organizatia-mea/membri/{id}/aprobare");
        await director.Delete($"/organizatia-mea/membri/{id}").Status(HttpStatusCode.NoContent);
        (await c.GetJson("/utilizatori/me")).Str("stareProfesor").ShouldBe("aprobat");
        await c.Get("/profesori/me").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProfesorulCareIeseDinUnicaOrganizatieRamaneAprobat()
    {
        var (c, id) = await ProfesorNou();
        await director.Post($"/organizatia-mea/membri/{id}/aprobare");
        await c.Delete($"/utilizatori/me/organizatii/{Liceu}").Status(HttpStatusCode.OK);
        (await c.GetJson("/utilizatori/me")).Str("stareProfesor").ShouldBe("aprobat");
        (await c.GetJson("/utilizatori/me/organizatii")).Lista().ShouldBeEmpty();
    }

    [Fact]
    public async Task OCerereRespinsaDupaAprobareNuRetrageAprobarea()
    {
        var (c, id) = await ProfesorNou();
        var (ovidiu, alba) = await OrganizatieNoua(aprobata: true);
        await director.Post($"/organizatia-mea/membri/{id}/aprobare");
        await c.Post("/utilizatori/me/organizatii", new { organizatieId = alba });
        await ovidiu.Post($"/organizatia-mea/membri/{id}/respingere", new { motiv = "Nu avem nevoie acum." });
        (await c.GetJson("/utilizatori/me")).Str("stareProfesor").ShouldBe("aprobat");
    }

    [Fact]
    public async Task ProfesorulCareIeseDinUnaRamaneAprobatInCealalta()
    {
        var (c, id) = await ProfesorNou();
        var (ovidiu, alba) = await OrganizatieNoua(aprobata: true);
        await director.Post($"/organizatia-mea/membri/{id}/aprobare");
        await c.Post("/utilizatori/me/organizatii", new { organizatieId = alba });
        await ovidiu.Post($"/organizatia-mea/membri/{id}/aprobare");
        await c.Delete($"/utilizatori/me/organizatii/{Liceu}").Status(HttpStatusCode.OK);
        (await c.GetJson("/utilizatori/me")).Str("stareProfesor").ShouldBe("aprobat");
    }

    [Fact]
    public async Task CerereaRetrasaDispare()
    {
        var (c, id) = await ProfesorNou();
        await c.Delete($"/utilizatori/me/organizatii/{Liceu}").Status(HttpStatusCode.OK);
        (await h.Db(db => db.Set<MembruOrganizatie>().AnyAsync(m => m.UtilizatorId == id))).ShouldBeFalse();
    }

    [Fact]
    public async Task DupaRefuzPoateCereDinNou()
    {
        var (c, id) = await ProfesorNou();
        await director.Post($"/organizatia-mea/membri/{id}/respingere", new { motiv = "Încă nu." });
        await c.Post("/utilizatori/me/organizatii", new { organizatieId = Liceu }).Status(HttpStatusCode.OK);
        (await c.GetJson("/utilizatori/me")).Str("stareProfesor").ShouldBe("in_asteptare");
    }

    [Fact]
    public async Task ADouaDecizieIdenticaNuSchimbaNimic()
    {
        var (_, id) = await ProfesorNou();
        await director.Post($"/organizatia-mea/membri/{id}/aprobare");
        await Proceseaza();
        var trimise = h.Emailuri.Trimise.Count;
        await director.Post($"/organizatia-mea/membri/{id}/aprobare").Status(HttpStatusCode.NoContent);
        await Proceseaza();
        h.Emailuri.Trimise.Count.ShouldBe(trimise);
    }

    [Fact]
    public async Task OrganizatiaNeaprobataNuPrimesteCereri()
    {
        var (c, _) = await ProfesorNou();
        var (_, alba) = await OrganizatieNoua();
        (await c.Post("/utilizatori/me/organizatii", new { organizatieId = alba }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este încă aprobată");
    }

    [Fact]
    public async Task AdminulPlatformeiNuFaceParteDinOrganizatii()
    {
        using var admin = await h.Login("admin");
        (await admin.Post("/utilizatori/me/organizatii", new { organizatieId = Liceu }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu poate face parte");
    }

    [Fact]
    public async Task AdministratorulNuIeseDinPropriaOrganizatie()
    {
        (await director.Delete($"/utilizatori/me/organizatii/{Liceu}").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Administratorul");
        (await director.Delete($"/organizatia-mea/membri/{await h.Db(db => db.Utilizatori.Where(u => u.Username == "director").Select(u => u.Id).SingleAsync())}")
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("pe tine");
    }
}
