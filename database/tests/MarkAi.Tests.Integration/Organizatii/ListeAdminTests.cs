using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Organizatii;

/// <summary>
/// The platform admin's lists: organisations filtered by county and locality and searched by name, county or locality
/// (without diacritics), and professors a page at a time; the page size each list allows (marime, up to its maximum).
/// Organisations: Liceul de Test (Sectorul 1, București), and here Centrul Alba (Alba Iulia), Școala Bărăbanț (Bărăbanț, Alba).
/// </summary>
public sealed class ListeAdminTests : TestOrganizatii
{
    private HttpClient admin = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await h.Db(async db =>
        {
            foreach (var (nume, judet, localitate, user) in new[] { ("Centrul Alba", 1, 1026, "adm-centru"), ("Școala Bărăbanț", 1, 1035, "adm-barabant") })
                db.Add(new Organizatie { Nume = nume, Adresa = "Str. 1", JudetId = judet, LocalitateId = localitate, Stare = Organizatie.Aprobata, CreatLa = DateTime.UtcNow,
                    Admin = new Utilizator { Nume = user, Username = user, Email = user + "@example.invalid", ParolaHash = "x" } });
            await db.SaveChangesAsync();
        });
        admin = await h.Login("admin");
    }

    private async Task<List<string>> Organizatii(string query) =>
        (await admin.GetJson("/admin/organizatii?stare=aprobata" + query)).Lista().Select(o => o.Str("nume")).Order().ToList();

    [Fact]
    public async Task FiltrulDeJudet() => (await Organizatii("&judetId=1")).ShouldBe(["Centrul Alba", "Școala Bărăbanț"]);

    [Fact]
    public async Task FiltrulDeLocalitate() => (await Organizatii("&judetId=1&localitateId=1035")).ShouldBe(["Școala Bărăbanț"]);

    [Theory]
    [InlineData("barabant", new[] { "Școala Bărăbanț" })]          // the locality (and the name)
    [InlineData("alba", new[] { "Centrul Alba", "Școala Bărăbanț" })] // the county
    [InlineData("alba iulia", new[] { "Centrul Alba" })]           // the locality
    [InlineData("bucuresti", new[] { "Liceul de Test" })]          // the county
    [InlineData("sectorul", new[] { "Liceul de Test" })]           // the locality
    [InlineData("scoala", new[] { "Școala Bărăbanț" })]            // the name, without diacritics
    [InlineData("centrul alba", new[] { "Centrul Alba" })]         // words found in different fields
    [InlineData("zzz", new string[0])]
    public async Task CautareaDupaNumeJudetSauLocalitate(string q, string[] gasite) =>
        (await Organizatii("&q=" + Uri.EscapeDataString(q))).ShouldBe(gasite.Order());

    [Fact]
    public async Task CautareaSeCombinaCuFiltrele()
    {
        (await Organizatii("&judetId=40&q=alba")).ShouldBeEmpty();
        (await Organizatii("&judetId=1&q=centrul")).ShouldBe(["Centrul Alba"]);
    }

    [Fact]
    public async Task TotalulUrmeazaFiltrele()
    {
        var r = await admin.GetJson("/admin/organizatii?stare=aprobata&judetId=1&marime=1");
        (r.Int("total"), r.Lista().Count).ShouldBe((2, 1));
        (await admin.GetJson("/admin/organizatii?stare=aprobata&judetId=1&marime=1&pagina=2")).Lista().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("/admin/organizatii?marime=0")]
    [InlineData("/admin/organizatii?marime=101")]
    [InlineData("/admin/profesori?marime=101")]
    [InlineData("/continut/parcursuri?marime=0")]
    [InlineData("/materii/1/parcursuri?marime=101")]
    [InlineData("/profesori?marime=101")]
    [InlineData("/organizatia-mea/membri?marime=101")]
    public async Task MarimeaPaginiiAreLimite(string url)
    {
        var c = url.StartsWith("/organizatia-mea") ? await h.Login("director") : admin;
        (await c.Get(url).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("între 1 și 100");
    }

    [Fact]
    public async Task MarimeaLaLimitaEstePrimita()
    {
        (await admin.GetJson("/admin/organizatii?marime=100")).Lista().Count.ShouldBeLessThanOrEqualTo(100);
        (await admin.GetJson("/profesori?marime=1")).Lista().Count.ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task ProfesoriiAdminuluiPePagini()
    {
        await h.Db(async db =>
        {
            for (var i = 0; i < 7; i++)
            {
                var u = new Utilizator { Nume = $"Candidat {i}", Username = $"candidat{i}", Email = $"candidat{i}@example.invalid", ParolaHash = "x", CreatLa = DateTime.UtcNow.AddMinutes(i) };
                u.ProfilMentor = new ProfilMentor { StareVerificare = "in_asteptare" };
                db.Add(u);
            }
            await db.SaveChangesAsync();
        });
        var p1 = await admin.GetJson("/admin/profesori?stare=in_asteptare&marime=5");
        (p1.Int("total"), p1.Lista().Count).ShouldBe((7, 5));
        var p2 = await admin.GetJson("/admin/profesori?stare=in_asteptare&marime=5&pagina=2");
        p2.Lista().Select(p => p.Str("nume")).ShouldBe(["Candidat 5", "Candidat 6"]); // oldest application first
        (await admin.GetJson("/admin/profesori?stare=aprobat")).Lista().ShouldAllBe(p => p.Str("stare") == RezervareService.MentorAprobat);
    }

    [Theory]
    [InlineData("irina", "Irina Marin")]
    [InlineData("MARIN", "Irina Marin")]
    [InlineData("irina@example", "Irina Marin")]
    [InlineData("dana director", "Dana Director")]
    public async Task AdminulCautaProfesoriDupaNumeUtilizatorSauEmail(string q, string gasit) =>
        (await admin.GetJson($"/admin/profesori?stare=aprobat&q={Uri.EscapeDataString(q)}")).Lista().Select(p => p.Str("nume")).ShouldBe([gasit]);

    [Fact]
    public async Task CautareaProfesorilorTineContDeTab()
    {
        (await admin.GetJson("/admin/profesori?stare=in_asteptare&q=irina")).Lista().ShouldBeEmpty();
        (await admin.GetJson("/admin/profesori?stare=aprobat&q=zzz")).Int("total").ShouldBe(0);
    }
}

