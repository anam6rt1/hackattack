using System.Net;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>Students: at most one organisation, validated by its approved professors or its admin.</summary>
public sealed class EleviOrganizatieTests : TestOrganizatii
{
    [Fact]
    public async Task ElevulSeInscrieCuClasaLoculSiCererea()
    {
        var (c, _) = await ElevNou();
        var eu = await c.GetJson("/utilizatori/me");
        (eu.Str("clasa"), eu.Int("judetId"), eu.Int("localitateId")).ShouldBe(("XI", 40, 179141));
        eu.Lista("organizatii").Single().Str("stare").ShouldBe("in_asteptare");
    }

    [Fact]
    public async Task AdministratorulAflaDupaConfirmare()
    {
        await ElevNou();
        await Proceseaza();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "director@example.invalid" && m.Subiect.Contains("Un elev cere"));
    }

    [Fact]
    public async Task ElevulAreOSinguraOrganizatie()
    {
        var (c, _) = await ElevNou();
        var (_, alba) = await OrganizatieNoua(aprobata: true);
        (await c.Post("/utilizatori/me/organizatii", new { organizatieId = alba }).Mesaj(HttpStatusCode.Conflict)).ShouldContain("Ieși din ea");
    }

    [Fact]
    public async Task CelDinAfaraNuValideazaElevii()
    {
        var (_, id) = await ElevNou();
        var (ovidiu, _) = await OrganizatieNoua(aprobata: true);
        await ovidiu.Post($"/organizatii/{Liceu}/elevi/{id}/aprobare").Status(HttpStatusCode.Forbidden);
        await ovidiu.Get($"/organizatii/{Liceu}/elevi").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProfesorulAprobatValideazaElevul()
    {
        var (c, id) = await ElevNou();
        using var irina = await h.Login("irina");
        (await irina.GetJson($"/organizatii/{Liceu}/elevi?stare=in_asteptare")).Lista().Count.ShouldBe(1);
        await irina.Post($"/organizatii/{Liceu}/elevi/{id}/aprobare").Status(HttpStatusCode.NoContent);
        (await c.GetJson("/utilizatori/me")).Lista("organizatii").Single().Str("stare").ShouldBe("aprobat");
    }

    [Fact]
    public async Task AdministratorulValideazaElevul()
    {
        var (_, id) = await ElevNou();
        using var director = await h.Login("director");
        await director.Post($"/organizatii/{Liceu}/elevi/{id}/aprobare").Status(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ElevulNuValideazaAltiElevi()
    {
        var (_, id) = await ElevNou();
        var (alt, _) = await ElevNou("matei");
        await alt.Post($"/organizatii/{Liceu}/elevi/{id}/aprobare").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProfesorulNuDecidePrinRutaElevilorPentruProfesori()
    {
        var (_, paul) = await ProfesorNou();
        using var irina = await h.Login("irina");
        await irina.Post($"/organizatii/{Liceu}/elevi/{paul}/aprobare").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RespingereaElevuluiCuMotiv()
    {
        var (c, id) = await ElevNou();
        using var irina = await h.Login("irina");
        await irina.Post($"/organizatii/{Liceu}/elevi/{id}/respingere", new { motiv = "Nu ești la noi." }).Status(HttpStatusCode.NoContent);
        var m = (await c.GetJson("/utilizatori/me/organizatii")).Lista().Single();
        (m.Str("stare"), m.Str("motivRespingere")).ShouldBe(("respins", "Nu ești la noi."));
    }

    [Fact]
    public async Task DupaIesireElevulPoateCereAltaOrganizatie()
    {
        var (c, id) = await ElevNou();
        using var irina = await h.Login("irina");
        await irina.Post($"/organizatii/{Liceu}/elevi/{id}/aprobare");
        await c.Delete($"/utilizatori/me/organizatii/{Liceu}").Status(HttpStatusCode.OK);
        var (_, alba) = await OrganizatieNoua(aprobata: true);
        await c.Post("/utilizatori/me/organizatii", new { organizatieId = alba }).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DupaRespingereElevulPoateCereAltaOrganizatie()
    {
        var (c, id) = await ElevNou();
        using var director = await h.Login("director");
        await director.Post($"/organizatii/{Liceu}/elevi/{id}/respingere", new { motiv = "x" });
        var (_, alba) = await OrganizatieNoua(aprobata: true);
        await c.Post("/utilizatori/me/organizatii", new { organizatieId = alba }).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CerereaExpiraDupaSapteZileIarContulRamane()
    {
        var (_, id) = await ElevNou();
        h.Ceas.Advance(TimeSpan.FromDays(7.1));
        await h.Serviciu<MarkAi.Api.Servicii.CuratareConturi>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<MembruOrganizatie>().Where(m => m.UtilizatorId == id).Select(m => m.Stare).SingleAsync())).ShouldBe(MembruOrganizatie.Expirat);
        (await h.Db(db => db.Utilizatori.AnyAsync(u => u.Id == id))).ShouldBeTrue();
        await Proceseaza();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "sara@example.invalid" && m.Subiect.Contains("a expirat"));
    }

    [Fact]
    public async Task DupaExpirarePoateCereDinNou()
    {
        var (c, _) = await ElevNou();
        h.Ceas.Advance(TimeSpan.FromDays(7.1));
        await h.Serviciu<MarkAi.Api.Servicii.CuratareConturi>().RuleazaOdataAsync(Ct);
        await c.Post("/utilizatori/me/organizatii", new { organizatieId = Liceu }).Status(HttpStatusCode.OK);
        (await c.GetJson("/utilizatori/me/organizatii")).Lista().Single().Str("stare").ShouldBe("in_asteptare");
    }

    [Fact]
    public async Task IesireaDinOrganizatieInexistentaEste404()
    {
        var (c, _) = await ElevNou(organizatie: null);
        await c.Delete($"/utilizatori/me/organizatii/{Liceu}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListaElevilorSeFiltreazaPeStare()
    {
        var (_, a) = await ElevNou("ana");
        var (_, b) = await ElevNou("bogdan");
        using var director = await h.Login("director");
        await director.Post($"/organizatii/{Liceu}/elevi/{a}/aprobare");
        (await director.GetJson($"/organizatii/{Liceu}/elevi?stare=aprobat")).Lista().Select(x => x.Int("utilizatorId")).ShouldBe([a]);
        (await director.GetJson($"/organizatii/{Liceu}/elevi?stare=in_asteptare")).Lista().Select(x => x.Int("utilizatorId")).ShouldBe([b]);
    }
}
