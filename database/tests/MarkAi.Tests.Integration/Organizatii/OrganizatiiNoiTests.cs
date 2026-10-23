using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>A new organisation: its admin applies, confirms the email, and a platform admin approves or rejects it.</summary>
public sealed class OrganizatiiNoiTests : TestOrganizatii
{
    [Fact]
    public async Task AdministratorulAplicaSiAsteapta()
    {
        var (c, _) = await OrganizatieNoua();
        var eu = await c.GetJson("/utilizatori/me");
        eu.Str("rol").ShouldBe("organizatie");
        eu.Str("stareProfesor").ShouldBe("in_asteptare");
        eu.GetProperty("organizatieAdministrata").Str("stare").ShouldBe("in_asteptare");
    }

    [Fact]
    public async Task LocalitateaTrebuieSaFieInJudet()
    {
        await h.Anonim().Post("/autentificare/inregistrare-organizatie", new { nume = "X", username = "x1", email = "x1@example.invalid",
            parola = Harness.ParolaTest, telefon = "0744000222", numeOrganizatie = "Y", adresa = "Z", judetId = 40, localitateId = 1026 }).Status(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("", "Str. 1", "0744000222")]
    [InlineData("Școala", "", "0744000222")]
    [InlineData("Școala", "Str. 1", "")]
    public async Task CampurileObligatorii(string numeOrganizatie, string adresa, string telefon)
    {
        await h.Anonim().Post("/autentificare/inregistrare-organizatie", new { nume = "X", username = "x1", email = "x1@example.invalid",
            parola = Harness.ParolaTest, telefon, numeOrganizatie, adresa, judetId = 40 }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminiiPlatformeiSuntAnuntatiDupaConfirmare()
    {
        await OrganizatieNoua();
        await Proceseaza();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "admin@example.invalid" && m.Subiect.Contains("Centrul Alba"));
    }

    [Fact]
    public async Task InainteDeConfirmareAdminiiNuSuntAnuntati()
    {
        await h.Anonim().Post("/autentificare/inregistrare-organizatie", new { nume = "Ovidiu", username = "ovidiu", email = "ovidiu@example.invalid",
            parola = Harness.ParolaTest, telefon = "0744000111", numeOrganizatie = "Centrul Alba", adresa = "Str. 2", judetId = 1 });
        await Proceseaza();
        h.Emailuri.Trimise.ShouldNotContain(m => m.Catre == "admin@example.invalid");
    }

    [Fact]
    public async Task OrganizatiaNeaprobataNuArePanou()
    {
        var (c, _) = await OrganizatieNoua();
        await c.Get("/organizatia-mea").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminulVedeCererileInAsteptare()
    {
        var (_, id) = await OrganizatieNoua();
        using var admin = await h.Login("admin");
        var o = (await admin.GetJson("/admin/organizatii?stare=in_asteptare")).Lista().Single(x => x.Int("id") == id);
        (o.Str("nume"), o.Str("adminEmail"), o.Bool("adminEmailConfirmat")).ShouldBe(("Centrul Alba", "ovidiu@example.invalid", true));
    }

    [Fact]
    public async Task DoarAdminiiPlatformeiAproba()
    {
        var (_, id) = await OrganizatieNoua();
        using var director = await h.Login("director");
        await director.Post($"/admin/organizatii/{id}/aprobare").Status(HttpStatusCode.Forbidden);
        await director.Get("/admin/organizatii").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AprobareaIlFaceProfesorAprobatCuPanou()
    {
        var (c, _) = await OrganizatieNoua(aprobata: true);
        (await c.GetJson("/utilizatori/me")).Str("stareProfesor").ShouldBe("aprobat");
        await c.Get("/organizatia-mea").Status(HttpStatusCode.OK);
        await c.Get("/profesori/me").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdministratorulPredaInOrganizatie()
    {
        var (c, id) = await OrganizatieNoua(aprobata: true);
        var m = (await c.GetJson("/organizatia-mea/membri?rol=profesor")).Lista().ShouldHaveSingleItem();
        m.Str("stare").ShouldBe("aprobat");
        (await c.GetJson("/utilizatori/me/organizatii-predate")).Lista().Single().Int("id").ShouldBe(id);
    }

    [Fact]
    public async Task SolicitantulEsteAnuntatDeAprobare()
    {
        await OrganizatieNoua(aprobata: true);
        await Proceseaza();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "ovidiu@example.invalid" && m.Subiect.Contains("a fost aprobată"));
    }

    [Fact]
    public async Task FaraEmailConfirmatNuSeAproba()
    {
        await h.Anonim().Post("/autentificare/inregistrare-organizatie", new { nume = "Ovidiu", username = "ovidiu", email = "ovidiu@example.invalid",
            parola = Harness.ParolaTest, telefon = "0744000111", numeOrganizatie = "Centrul Alba", adresa = "Str. 2", judetId = 1 });
        var id = await h.Db(db => db.Set<Organizatie>().Where(o => o.Nume == "Centrul Alba").Select(o => o.Id).SingleAsync());
        using var admin = await h.Login("admin");
        (await admin.Post($"/admin/organizatii/{id}/aprobare").Mesaj(HttpStatusCode.Conflict)).ShouldContain("nu și-a confirmat");
    }

    [Fact]
    public async Task RespingereaCuMotiv()
    {
        var (c, id) = await OrganizatieNoua();
        using var admin = await h.Login("admin");
        await admin.Post($"/admin/organizatii/{id}/respingere", new { motiv = "Date incomplete." }).Status(HttpStatusCode.NoContent);
        var eu = await c.GetJson("/utilizatori/me");
        eu.GetProperty("organizatieAdministrata").Str("stare").ShouldBe("respinsa");
        eu.GetProperty("organizatieAdministrata").Str("motivRespingere").ShouldBe("Date incomplete.");
        eu.Str("stareProfesor").ShouldBe("respins");
        await Proceseaza();
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "ovidiu@example.invalid" && m.Text.Contains("Date incomplete."));
    }

    [Fact]
    public async Task OrganizatiaAprobataNuMaiPoateFiRespinsa()
    {
        var (_, id) = await OrganizatieNoua(aprobata: true);
        using var admin = await h.Login("admin");
        await admin.Post($"/admin/organizatii/{id}/respingere", new { motiv = "x" }).Status(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OrganizatiaInexistentaEste404()
    {
        using var admin = await h.Login("admin");
        await admin.Post("/admin/organizatii/999/aprobare").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OrganizatiaRespinsaSeStergeDupaSapteZile()
    {
        var (_, id) = await OrganizatieNoua();
        using var admin = await h.Login("admin");
        await admin.Post($"/admin/organizatii/{id}/respingere", new { motiv = "x" });
        h.Ceas.Advance(TimeSpan.FromDays(7.1));
        await h.Serviciu<MarkAi.Api.Servicii.CuratareConturi>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<Organizatie>().AnyAsync(o => o.Id == id))).ShouldBeFalse();
        (await h.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "ovidiu"))).ShouldBeFalse();
    }

    [Fact]
    public async Task OrganizatiaAprobataRamane()
    {
        var (_, id) = await OrganizatieNoua(aprobata: true);
        h.Ceas.Advance(TimeSpan.FromDays(30));
        await h.Serviciu<MarkAi.Api.Servicii.CuratareConturi>().RuleazaOdataAsync(Ct);
        (await h.Db(db => db.Set<Organizatie>().AnyAsync(o => o.Id == id))).ShouldBeTrue();
    }

    [Fact]
    public async Task UnAdministratorConduceOSinguraOrganizatie()
    {
        // The unique index on admin_id: a second organisation for the same admin is refused by the database.
        var adminId = await h.Db(db => db.Utilizatori.Where(u => u.Username == "director").Select(u => u.Id).SingleAsync());
        await Should.ThrowAsync<DbUpdateException>(() => h.Db(async db =>
        {
            db.Add(new Organizatie { Nume = "A doua", Adresa = "x", JudetId = 40, AdminId = adminId, CreatLa = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task ListaAdminuluiArePagini()
    {
        await h.Db(async db =>
        {
            // One organisation per admin account (the database allows no more).
            for (var i = 0; i < 23; i++)
            {
                var u = new Utilizator { Nume = $"Admin {i:00}", Username = $"admin{i:00}", Email = $"admin{i:00}@example.invalid", ParolaHash = "x" };
                db.Add(new Organizatie { Nume = $"Școala {i:00}", Adresa = "Str. 1", JudetId = 40, Stare = Organizatie.InAsteptare, Admin = u,
                    CreatLa = DateTime.UtcNow.AddMinutes(-i) });
            }
            await db.SaveChangesAsync();
        });
        using var admin = await h.Login("admin");
        var p1 = await admin.GetJson("/admin/organizatii?stare=in_asteptare");
        (p1.Int("total"), p1.Lista().Count).ShouldBe((23, 20));
        p1.Lista().First().Str("nume").ShouldBe("Școala 00"); // newest first
        var p2 = await admin.GetJson("/admin/organizatii?stare=in_asteptare&pagina=2");
        p2.Lista().Select(o => o.Str("nume")).ShouldBe(["Școala 20", "Școala 21", "Școala 22"]);
        (await admin.GetJson("/admin/organizatii")).Int("total").ShouldBe(24);
        await admin.Get("/admin/organizatii?pagina=0").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OrganizatiaDinListaEsteCaCeaSingura()
    {
        using var admin = await h.Login("admin");
        var dinLista = (await admin.GetJson("/admin/organizatii")).Lista().Single(o => o.Int("id") == Liceu);
        dinLista.Int("profesori").ShouldBeGreaterThan(0);
        dinLista.Str("nume").ShouldBe("Liceul de Test");
    }
}

