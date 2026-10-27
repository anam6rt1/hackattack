using System.Net;
using System.Net.Http.Json;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Continut;

public class ContactTests : TestCuHarness
{
    // Every anonymous client here comes from the same address; the per-address limit has its own test below.
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?> { ["Limite:ContactPeOra"] = "100" };

    [Fact]
    public async Task ContulScrieFaraDateCopiateIarAdministratorulIlVedeSiIlRezolva()
    {
        using var elev = await h.Login("george"); using var prof = await h.Login("irina"); using var admin = await h.Login("admin");
        var eu = await elev.GetJson("/utilizatori/me");
        // A name and address sent by a signed-in sender are ignored: the account's own are shown, and only the path of the page is kept.
        await elev.Post("/contact", new { subiect = "problema", text = "Nu se vede lecția", pagina = "/materiale?token=secret#x", nume = "Altcineva", email = "alt@exemplu.ro" })
            .Status(HttpStatusCode.NoContent);
        await prof.GetAsync("/admin/contact", Ct).Status(HttpStatusCode.Forbidden);
        var f = (await admin.GetJson("/admin/contact")).GetProperty("elemente")[0];
        f.Str("pagina").ShouldBe("/materiale"); f.Str("subiect").ShouldBe("problema");
        f.Str("nume").ShouldBe(eu.Str("nume")); f.Str("email").ShouldBe(eu.Str("email")); f.GetProperty("cont").GetBoolean().ShouldBeTrue(); f.Str("rol").ShouldBe("elev");
        (await h.Db(db => db.Set<MesajContact>().SingleAsync())).Email.ShouldBeNull();

        await admin.Post($"/admin/contact/{f.Int("id")}/rezolvat").Status(HttpStatusCode.NoContent);
        (await admin.GetJson("/admin/contact")).Int("total").ShouldBe(0);
        (await admin.GetJson("/admin/contact?rezolvate=true")).Int("total").ShouldBe(1);
        await admin.Post($"/admin/contact/{f.Int("id")}/redeschis").Status(HttpStatusCode.NoContent);
        (await admin.GetJson("/admin/contact")).Int("total").ShouldBe(1);
        await admin.Post("/admin/contact/999999/rezolvat").Status(HttpStatusCode.NotFound);

        for (var i = 0; i < 4; i++) await elev.Post("/contact", new { subiect = "sugestie", text = "Mesaj" }).Status(HttpStatusCode.NoContent);
        await elev.Post("/contact", new { subiect = "sugestie", text = "În plus" }).Status(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task VizitatorulFaraContLasaNumeleSiEmailulValid()
    {
        var anonim = h.Anonim();
        await anonim.Post("/contact", new { subiect = "organizatii", text = "Vrem școala pe platformă.", email = "dir@liceu.ro" }).Status(HttpStatusCode.BadRequest);
        await anonim.Post("/contact", new { subiect = "organizatii", text = "Vrem școala pe platformă.", nume = "Ana", email = "nu-e-email" }).Status(HttpStatusCode.BadRequest);
        await anonim.Post("/contact", new { subiect = "inexistent", text = "Text", nume = "Ana", email = "dir@liceu.ro" }).Status(HttpStatusCode.BadRequest);
        await anonim.Post("/contact", new { subiect = "organizatii", text = " ", nume = "Ana", email = "dir@liceu.ro" }).Status(HttpStatusCode.BadRequest);
        for (var i = 0; i < 3; i++)
            await anonim.Post("/contact", new { subiect = "organizatii", text = "Vrem școala pe platformă.", nume = "Ana Pop", email = "Dir@Liceu.ro" }).Status(HttpStatusCode.NoContent);
        await anonim.Post("/contact", new { subiect = "organizatii", text = "Încă unul", nume = "Ana Pop", email = "dir@liceu.ro" }).Status(HttpStatusCode.Conflict);
        var salvat = await h.Db(db => db.Set<MesajContact>().FirstAsync());
        salvat.Email.ShouldBe("dir@liceu.ro"); salvat.Nume.ShouldBe("Ana Pop"); salvat.UtilizatorId.ShouldBeNull();

        using var admin = await h.Login("admin");
        var f = (await admin.GetJson("/admin/contact")).GetProperty("elemente")[0];
        f.Str("email").ShouldBe("dir@liceu.ro"); f.GetProperty("cont").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task CampulAscunsCompletatDeRoboțiNuSalveazaNimic()
    {
        await h.Anonim().Post("/contact", new { subiect = "altceva", text = "Reclamă", nume = "Bot", email = "bot@spam.ro", site = "https://spam.example" })
            .Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.Set<MesajContact>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task ExportulSiStergereaContuluiCuprindMesajeleTrimiseInainteDeCont()
    {
        using var elev = await h.Login("george");
        var email = (await elev.GetJson("/utilizatori/me")).Str("email")!;
        await h.Anonim().Post("/contact", new { subiect = "cont", text = "Nu primesc codul.", nume = "George", email = email.ToUpperInvariant() }).Status(HttpStatusCode.NoContent);
        await elev.Post("/contact", new { subiect = "problema", text = "Acum am cont." }).Status(HttpStatusCode.NoContent);
        var export = await elev.Post("/utilizatori/me/date/export", new { parolaActuala = Harness.ParolaTest }).Json();
        export.GetProperty("mesajeContact").GetArrayLength().ShouldBe(2);
        await elev.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/utilizatori/me/date") { Content = JsonContent.Create(new { parolaActuala = Harness.ParolaTest, confirmare = "STERGE" }) }, Ct)
            .Status(HttpStatusCode.Accepted);
        (await h.Db(db => db.Set<MesajContact>().CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task MesajeleRezolvateDemultSeStergDoarCandRetentiaEsteSetata()
    {
        await using var cuRetentie = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Confidentialitate:MesajeContactZile"] = "30" });
        await cuRetentie.Db(async db =>
        {
            var acum = DateTime.UtcNow;
            db.AddRange(new MesajContact { Text = "vechi rezolvat", CreatLa = acum.AddDays(-60), RezolvatLa = acum.AddDays(-40) },
                new MesajContact { Text = "vechi nerezolvat", CreatLa = acum.AddDays(-60) },
                new MesajContact { Text = "rezolvat recent", CreatLa = acum.AddDays(-20), RezolvatLa = acum.AddDays(-10) });
            await db.SaveChangesAsync();
        });
        await cuRetentie.Serviciu<MarkAi.Api.Servicii.CuratareDatePersonale>().RuleazaOdataAsync(Ct);
        (await cuRetentie.Db(db => db.Set<MesajContact>().OrderBy(m => m.Text).Select(m => m.Text).ToListAsync()))
            .ShouldBe(["rezolvat recent", "vechi nerezolvat"]);
    }

    [Fact]
    public async Task LimitaPeAdresaOpresteTrimiterileRepetate()
    {
        await using var strict = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:ContactPeOra"] = "2" });
        var anonim = strict.Anonim(); var coduri = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            coduri.Add((await anonim.Post("/contact", new { subiect = "altceva", text = "Salut", nume = "Ana", email = $"ana{i}@liceu.ro" })).StatusCode);
        coduri.ShouldBe([HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.TooManyRequests]);
    }
}

public class CacheOrganizatiiTests : TestCuHarness
{
    [Fact]
    public async Task CacheOrganizatiiSeInvalideazaLaEditareFaraPartajareaDatelorPrivate()
    {
        var anonim = h.Anonim(); using var director = await h.Login("director");
        (await anonim.GetJson("/organizatii")).GetRawText().ShouldContain("Liceul de Test");
        var o = await director.GetJson("/organizatia-mea");
        await director.Put("/organizatia-mea", new { nume = "Liceul Actualizat", adresa = o.Str("adresa"), judetId = o.Int("judetId"), localitateId = (int?)null }).Status(HttpStatusCode.OK);
        var updated = await anonim.GetJson("/organizatii"); updated.GetRawText().ShouldContain("Liceul Actualizat"); updated.GetRawText().ShouldNotContain("adminEmail");
        (await anonim.GetJson("/judete")).GetRawText().ShouldBe((await anonim.GetJson("/judete")).GetRawText());
    }
}
