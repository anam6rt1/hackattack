using MarkAi.Data.Entities;
using System.Net;

namespace MarkAi.Tests.Integration.Lectii;

/// <summary>View-only shares to the author's other organisations, and the organisation's switch for public publishing.</summary>
public sealed class PartajareSiPublicareTests : TestLectii
{
    private async Task<int> Partajata()
    {
        var (l, _, _) = await LectiePrivata();
        await irina.Put($"/parcursuri/{l}", Lectie("Ion — organizație", Liceu, clasa: "XI", vizibilitate: "organizatie", partajari: [Centru])).Status(HttpStatusCode.OK);
        return l;
    }

    private async Task OpresteIrina() =>
        await director.Put($"/organizatia-mea/membri/{await Id("irina")}/publicare", new { poatePublicaPublic = false }).Status(HttpStatusCode.NoContent);

    [Fact]
    public async Task EleviiOrganizatieiPartajateVadLectia()
    {
        var l = await Partajata();
        await radu.Get($"/parcursuri/{l}").Status(HttpStatusCode.OK);
        var c = (await radu.GetJson($"/parcursuri/{l}")).Lista("capitole")[0].Int("id");
        await radu.Get($"/cursuri/{c}").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PartajareaApareInEditor()
    {
        var l = await Partajata();
        (await Editor(irina, l)).Lista("partajari").Select(x => x.GetInt32()).ShouldBe([Centru]);
    }

    [Fact]
    public async Task AdministratorulOrganizatieiPartajateNuEditeaza()
    {
        var l = await Partajata();
        await ovidiu.Put($"/parcursuri/{l}", Lectie("x", Liceu, clasa: "XI", vizibilitate: "organizatie")).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PartajareDoarInOrganizatiileAutorului()
    {
        var aPetrei = await Creeaza(petra, Lectie("Lecția Petrei", Liceu));
        (await petra.Put($"/parcursuri/{aPetrei}", Lectie("Lecția Petrei", Liceu, partajari: [Centru])).Mesaj(HttpStatusCode.BadRequest))
            .ShouldContain("celelalte organizații ale autorului");
    }

    [Fact]
    public async Task CelMultPatruPartajari()
    {
        // Only possible for an author who joined more organisations before the limit of four existed.
        var irinaId = await Id("irina");
        var alte = await h.Db(async db =>
        {
            var org = Enumerable.Range(1, 5).Select(i => new Organizatie
            {
                Nume = $"Școala {i}", Adresa = "Str. 1", JudetId = 1, Stare = Organizatie.Aprobata, CreatLa = DateTime.UtcNow,
                Admin = new Utilizator { Nume = $"adm{i}", Username = $"adm-part{i}", Email = $"adm-part{i}@example.invalid", ParolaHash = "x" },
                Membri = { new MembruOrganizatie { UtilizatorId = irinaId, Rol = MembruOrganizatie.RolProfesor, Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow } },
            }).ToList();
            db.AddRange(org);
            await db.SaveChangesAsync();
            return org.Select(o => o.Id).ToArray();
        });
        var (l, _, _) = await LectiePrivata();
        (await irina.Put($"/parcursuri/{l}", Lectie("Ion", Liceu, clasa: "XI", partajari: alte)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("cel mult 4");
        await irina.Put($"/parcursuri/{l}", Lectie("Ion", Liceu, clasa: "XI", partajari: alte[..4])).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PartajareaScoasaInchideAccesul()
    {
        var l = await Partajata();
        await irina.Put($"/parcursuri/{l}", Lectie("Ion — organizație", Liceu, clasa: "XI", vizibilitate: "organizatie", partajari: [])).Status(HttpStatusCode.OK);
        await radu.Get($"/parcursuri/{l}").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PartajareaCuOrganizatiaDeBazaEsteIgnorata()
    {
        var (l, _, _) = await LectiePrivata();
        await irina.Put($"/parcursuri/{l}", Lectie("Ion", Liceu, clasa: "XI", vizibilitate: "organizatie", partajari: [Liceu])).Status(HttpStatusCode.OK);
        (await Editor(irina, l)).Lista("partajari").ShouldBeEmpty();
    }

    [Fact]
    public async Task PartajareaVecheRamanePermisaDupaCeAutorulPleaca()
    {
        var l = await Partajata();
        // Irina leaves Centrul Alba; the director (who edits the Lecție) may still keep the existing share.
        await irina.Delete($"/utilizatori/me/organizatii/{Centru}").Status(HttpStatusCode.OK);
        await director.Put($"/parcursuri/{l}", Lectie("Ion", Liceu, clasa: "XI", vizibilitate: "organizatie", partajari: [Centru])).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LectiaPublicaEsteDeschisaTuturor()
    {
        var l = await Creeaza(irina, Lectie("Luceafărul pentru toți", Liceu, clasa: "XII"));
        var c = await Capitol(irina, l, "Luceafărul: capitolul 1");
        await george.Get($"/cursuri/{c}").Status(HttpStatusCode.OK);
        await radu.Get($"/cursuri/{c}").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ComutatorulOpritFaceLectiileDoarPentruOrganizatie()
    {
        var l = await Creeaza(irina, Lectie("Luceafărul pentru toți", Liceu, clasa: "XII"));
        var c = await Capitol(irina, l, "Luceafărul: capitolul 1");
        await OpresteIrina();
        await george.Get($"/cursuri/{c}").Status(HttpStatusCode.NotFound);
        (await Editor(irina, l)).Str("vizibilitate").ShouldBe("organizatie");
    }

    [Fact]
    public async Task DupaComutatorLectiaNuMaiDevinePublica()
    {
        var l = await Creeaza(irina, Lectie("Luceafărul pentru toți", Liceu, clasa: "XII"));
        await OpresteIrina();
        (await irina.Put($"/parcursuri/{l}", Lectie("Luceafărul pentru toți", Liceu, clasa: "XII")).Mesaj(HttpStatusCode.Conflict)).ShouldContain("nu permite publicarea");
    }

    [Fact]
    public async Task DupaComutatorNicioLectieNouaPublica()
    {
        await OpresteIrina();
        await irina.Post("/parcursuri", Lectie("Nouă publică", Liceu)).Status(HttpStatusCode.Conflict);
        await Creeaza(irina, Lectie("Nouă privată", Liceu, vizibilitate: "organizatie"));
    }

    [Fact]
    public async Task EditorulSpuneDeCePublicEsteGri()
    {
        var l = await Creeaza(irina, Lectie("x", Liceu));
        (await Editor(irina, l)).Bool("poatePublicaPublic").ShouldBeTrue();
        await OpresteIrina();
        (await Editor(irina, l)).Bool("poatePublicaPublic").ShouldBeFalse();
        (await irina.GetJson("/continut/organizatii")).Lista().Single(o => o.Int("id") == Liceu).Bool("poatePublicaPublic").ShouldBeFalse();
    }

    [Fact]
    public async Task ComutatorulNuAtingeCealaltaOrganizatieAAutorului()
    {
        var inCentru = await Creeaza(irina, Lectie("Lecție în Centrul Alba", Centru, clasa: "XII"));
        await OpresteIrina();
        (await Editor(irina, inCentru)).Str("vizibilitate").ShouldBe("public");
        await Creeaza(irina, Lectie("Altă publică în Centru", Centru));
    }

    [Fact]
    public async Task ComutatorulNuAtingeAltiProfesori()
    {
        var aPetrei = await Creeaza(petra, Lectie("Lecția Petrei", Liceu));
        await OpresteIrina();
        (await Editor(petra, aPetrei)).Str("vizibilitate").ShouldBe("public");
    }

    [Fact]
    public async Task ComutatorulRepornitNuFaceLectiilePubliceSingur()
    {
        var l = await Creeaza(irina, Lectie("x", Liceu));
        await OpresteIrina();
        await director.Put($"/organizatia-mea/membri/{await Id("irina")}/publicare", new { poatePublicaPublic = true }).Status(HttpStatusCode.NoContent);
        (await Editor(irina, l)).Str("vizibilitate").ShouldBe("organizatie");
        await irina.Put($"/parcursuri/{l}", Lectie("x", Liceu)).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ComutatorulSeAplicaDoarProfesorilor()
    {
        (await director.Put($"/organizatia-mea/membri/{await Id("elena")}/publicare", new { poatePublicaPublic = false }).Mesaj(HttpStatusCode.BadRequest))
            .ShouldContain("doar profesorilor");
    }

    [Fact]
    public async Task DoarAdministratorulOrganizatieiFolosesteComutatorul()
    {
        (await petra.Put($"/organizatia-mea/membri/{await Id("irina")}/publicare", new { poatePublicaPublic = false })).StatusCode.ShouldNotBe(HttpStatusCode.NoContent);
    }
}
