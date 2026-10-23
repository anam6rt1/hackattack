using System.Net;
using System.Text.Json;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Cautare;

/// <summary>Where a professor teaches (own place or one of their organisations), and the professor filters built on it.</summary>
public sealed class LocProfesorTests : TestCautare
{
    [Fact]
    public async Task ImplicitLoculPropriuDintreDouaOptiuni()
    {
        var loc = await irina.GetJson("/profesori/me/loc");
        loc.Lista("optiuni").Count.ShouldBe(2);
        loc.GetProperty("organizatieId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task NuOOrganizatieDinCareNuFaceParte()
    {
        (await irina.Put("/profesori/me/loc", new { organizatieId = Centru }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("din care faci parte");
    }

    [Fact]
    public async Task ProfesorulAlegeOrganizatia()
    {
        (await irina.Put("/profesori/me/loc", new { organizatieId = Liceu }).Json()).Int("organizatieId").ShouldBe(Liceu);
    }

    [Fact]
    public async Task AproapeDeMineEsteDoarLocalitateaElevului()
    {
        // irina teaches at Liceul de Test (Sectorul 1); elena lives in Sectorul 2 of the same county: not near her.
        await CuSedinte("irina", "ovidiu");
        await irina.Put("/profesori/me/loc", new { organizatieId = Liceu });
        (await Profesori(elena, "?filtru=local")).ShouldBeEmpty();
        await h.Db(db => db.Utilizatori.Where(u => u.Username == "elena").ExecuteUpdateAsync(x => x.SetProperty(u => u.LocalitateId, Sector1)));
        var p = (await Profesori(elena, "?filtru=local")).ShouldHaveSingleItem();
        p.Id.ShouldBe(await Id("irina"));
        p.Loc.ShouldBe("Liceul de Test · Sectorul 1, București");
        p.InLocalitateaTa.ShouldBeTrue();
    }

    [Fact]
    public async Task InAlbaDoarCeiDinAlbaIulia()
    {
        // ovidiu teaches at Centrul Alba (Alba Iulia, radu's locality); bianca at her own place in Bărăbanț, same county.
        await CuSedinte("irina", "ovidiu", "bianca");
        await ovidiu.Put("/profesori/me/loc", new { organizatieId = Centru });
        var p = await Profesori(radu, "?filtru=local");
        p.Select(x => x.Id).ShouldBe([await Id("ovidiu")]);
        p.ShouldAllBe(x => x.InLocalitateaTa);
    }

    [Fact]
    public async Task AproapeDeMineAscundeProfesoriiFaraSedinte()
    {
        await ovidiu.Put("/profesori/me/loc", new { organizatieId = Centru });
        (await Profesori(radu, "?filtru=local")).ShouldBeEmpty();
        await CuSedinte("ovidiu");
        (await Profesori(radu, "?filtru=local")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ProfesoriiDinOrganizatiaMea()
    {
        await CuSedinte("irina", "ovidiu", "bianca");
        var (irinaId, ovidiuId) = (await Id("irina"), await Id("ovidiu"));
        var p = (await Profesori(elena, "?filtru=organizatia-mea")).Select(x => x.Id).ToList();
        p.ShouldBe([irinaId]);
        // radu is in Centrul Alba, which ovidiu runs.
        (await Profesori(radu, "?filtru=organizatia-mea")).Select(x => x.Id).ShouldBe([ovidiuId]);
    }

    [Fact]
    public async Task OrganizatiaMeaFaraOrganizatieEsteGoala()
    {
        await CuSedinte("irina", "ovidiu", "bianca");
        (await Profesori(george, "?filtru=organizatia-mea")).ShouldBeEmpty();
    }

    [Fact]
    public async Task OCerereInAsteptareNuEsteOrganizatiaMea()
    {
        await CuSedinte("irina");
        await h.Db(db => db.Set<MembruOrganizatie>().Where(m => m.OrganizatieId == Liceu && m.Utilizator.Username == "elena")
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.Stare, MembruOrganizatie.InAsteptare)));
        (await Profesori(elena, "?filtru=organizatia-mea")).ShouldBeEmpty();
    }

    [Fact]
    public async Task UnProfesorPlecatNuMaiEsteDinOrganizatiaMea()
    {
        await CuSedinte("irina");
        await h.Db(db => db.Set<MembruOrganizatie>().Where(m => m.OrganizatieId == Liceu && m.Utilizator.Username == "irina")
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.Stare, MembruOrganizatie.Expirat)));
        (await Profesori(elena, "?filtru=organizatia-mea")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ProfesoriiLocaliCerLocalitateaElevului()
    {
        (await george.Get("/profesori?filtru=local").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("localitatea");
        // A county without a locality is not enough either.
        await h.Db(db => db.Utilizatori.Where(u => u.Username == "elena").ExecuteUpdateAsync(x => x.SetProperty(u => u.LocalitateId, (int?)null)));
        (await elena.Get("/profesori?filtru=local").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Setări");
    }

    [Fact]
    public async Task DupaPlecareDinOrganizatieSeFolosesteLoculPropriu()
    {
        await CuSedinte("irina");
        var irinaId = await Id("irina");
        await irina.Put("/profesori/me/loc", new { organizatieId = Liceu });
        await h.Db(async db =>
        {
            var u = await db.Utilizatori.SingleAsync(x => x.Id == irinaId);
            (u.JudetId, u.LocalitateId) = (Bucuresti, Sector2);
            var m = await db.Set<MembruOrganizatie>().SingleAsync(x => x.OrganizatieId == Liceu && x.UtilizatorId == irinaId);
            m.Stare = MembruOrganizatie.Expirat;
            await db.SaveChangesAsync();
        });
        var dupa = (await Profesori(elena, "?filtru=local")).Single(p => p.Id == irinaId);
        dupa.Loc.ShouldBe("Sectorul 2, București");
        dupa.InLocalitateaTa.ShouldBeTrue();
        (await irina.GetJson("/profesori/me/loc")).GetProperty("organizatieId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ElevulNuAreLocDePredare()
    {
        await george.Get("/profesori/me/loc").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProfesorulNeaprobatNuApareInLista()
    {
        await CuSedinte("ovidiu");
        var ovidiuId = await Id("ovidiu");
        await h.Db(db => db.ProfiluriMentor.Where(p => p.UtilizatorId == ovidiuId).ExecuteUpdateAsync(s => s.SetProperty(p => p.StareVerificare, "in_asteptare")));
        (await Profesori(george)).ShouldNotContain(p => p.Id == ovidiuId);
        await george.Get($"/profesori/{ovidiuId}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProfilulSeDeschideSiFaraSedinte()
    {
        var irinaId = await Id("irina");
        (await Profesori(george)).ShouldNotContain(p => p.Id == irinaId);
        (await george.GetJson($"/profesori/{irinaId}")).Lista("sloturi").ShouldBeEmpty();
    }

    [Fact]
    public async Task ProfilulPublicAlProfesorului()
    {
        var irinaId = await Id("irina");
        var p = await george.GetJson($"/profesori/{irinaId}");
        p.Str("nume").ShouldBe("Irina Marin");
        p.ToString().ShouldNotContain("+40722123456");
    }
}
