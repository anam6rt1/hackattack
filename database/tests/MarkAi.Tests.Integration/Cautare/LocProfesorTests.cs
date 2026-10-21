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
    public async Task ProfesoriiLocaliDinBucuresti()
    {
        await irina.Put("/profesori/me/loc", new { organizatieId = Liceu });
        await ovidiu.Put("/profesori/me/loc", new { organizatieId = Centru });
        var (irinaId, ovidiuId) = (await Id("irina"), await Id("ovidiu"));
        var p = await Profesori(elena, "?filtru=local");
        p.Single(x => x.Id == irinaId).Loc.ShouldBe("Liceul de Test · Sectorul 1, București");
        p.ShouldNotContain(x => x.Id == ovidiuId);
    }

    [Fact]
    public async Task ProfesoriiLocaliDinAlbaSuntInLocalitateaElevului()
    {
        await ovidiu.Put("/profesori/me/loc", new { organizatieId = Centru });
        var (irinaId, ovidiuId) = (await Id("irina"), await Id("ovidiu"));
        var p = await Profesori(radu, "?filtru=local");
        p.Single(x => x.Id == ovidiuId).InLocalitateaTa.ShouldBeTrue();
        p.ShouldNotContain(x => x.Id == irinaId);
    }

    [Fact]
    public async Task ProfesoriiDinOrganizatiaMea()
    {
        var (irinaId, ovidiuId) = (await Id("irina"), await Id("ovidiu"));
        var p = (await Profesori(elena, "?filtru=organizatia-mea")).Select(x => x.Id).ToList();
        p.ShouldContain(irinaId);
        p.ShouldNotContain(ovidiuId);
    }

    [Fact]
    public async Task ProfesoriiLocaliCerJudetulElevului()
    {
        await george.Get("/profesori?filtru=local").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DupaPlecareDinOrganizatieSeFolosesteLoculPropriu()
    {
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
        var ovidiuId = await Id("ovidiu");
        await h.Db(db => db.ProfiluriMentor.Where(p => p.UtilizatorId == ovidiuId).ExecuteUpdateAsync(s => s.SetProperty(p => p.StareVerificare, "in_asteptare")));
        (await Profesori(george)).ShouldNotContain(p => p.Id == ovidiuId);
        await george.Get($"/profesori/{ovidiuId}").Status(HttpStatusCode.NotFound);
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
