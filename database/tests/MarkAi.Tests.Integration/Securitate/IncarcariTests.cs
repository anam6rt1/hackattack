using System.Net;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Securitate;

/// <summary>Professor applications with a photo and a qualification document: content must match the extension; files stay private.</summary>
public sealed class IncarcariTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:InregistrariPeOra"] = "100", ["Limite:AutentificariPeMinut"] = "100", ["Limite:EmailuriPeOra"] = "100",
    };

    static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
    static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1];
    static readonly byte[] Webp = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P'];
    static readonly byte[] Pdf = "%PDF-1.4\n%fals\n"u8.ToArray();
    static readonly byte[] Exe = [(byte)'M', (byte)'Z', 0x90, 0, 3, 0, 0, 0, 4, 0, 0, 0, 0xFF, 0xFF, 0, 0];

    private static int telefon = 10000000;

    private Task<HttpResponseMessage> Aplica(HttpClient client, string username, (string Nume, byte[] Date)? poza, (string Nume, byte[] Date)? dovada,
        string organizatii = "1", string materii = "1", string? tel = null)
    {
        var f = new MultipartFormDataContent
        {
            { new StringContent("Profesor " + username), "Nume" }, { new StringContent(username), "Username" },
            { new StringContent(username + "@example.invalid"), "Email" }, { new StringContent(Harness.ParolaTest), "Parola" },
            { new StringContent(tel ?? "07" + Interlocked.Increment(ref telefon)), "Telefon" },
            { new StringContent("Predau limba română de mulți ani."), "Descriere" }, { new StringContent("40"), "JudetId" },
        };
        foreach (var m in materii.Split(',', StringSplitOptions.RemoveEmptyEntries)) f.Add(new StringContent(m), "MaterieIds");
        foreach (var o in organizatii.Split(',', StringSplitOptions.RemoveEmptyEntries)) f.Add(new StringContent(o), "OrganizatieIds");
        if (poza is var (pn, pd)) f.Add(Raspunsuri.Fisier(pd), "Poza", pn);
        if (dovada is var (dn, dd)) f.Add(Raspunsuri.Fisier(dd), "Dovada", dn);
        return client.PostAsync("/autentificare/inregistrare-profesor", f, Ct);
    }

    private async Task<(HttpClient Client, int Id)> Aplicant(string username = "prof")
    {
        var c = h.Anonim();
        await Aplica(c, username, ("poza.png", Png), ("dovada.pdf", Pdf)).Status(HttpStatusCode.Accepted);
        await c.Post("/autentificare/confirma-email", new { email = username + "@example.invalid", cod = h.Emailuri.UltimulCod(username + "@example.invalid") })
            .Status(HttpStatusCode.OK);
        return (c, (await c.GetJson("/utilizatori/me")).Int("id"));
    }

    [Fact]
    public async Task PozaSiPdfValideSuntAcceptate()
    {
        await Aplica(h.Anonim(), "prof", ("poza.png", Png), ("dovada.pdf", Pdf)).Status(HttpStatusCode.Accepted);
        var p = await h.Db(db => db.ProfiluriMentor.SingleAsync(x => x.Utilizator.Username == "prof"));
        p.StareVerificare.ShouldBe("in_asteptare");
        p.PozaUrl.ShouldStartWith("poze/");
        p.DovadaCalificareUrl.ShouldStartWith("dovezi/");
    }

    [Theory]
    [InlineData("poza.jpg")]
    [InlineData("poza.JPEG")]
    public async Task JpegEsteAcceptat(string nume)
    {
        await Aplica(h.Anonim(), "prof", (nume, Jpeg), ("dovada.pdf", Pdf)).Status(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task WebpEsteAcceptat()
    {
        await Aplica(h.Anonim(), "prof", ("poza.webp", Webp), ("dovada.pdf", Pdf)).Status(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task DovadaPoateFiOImagine()
    {
        await Aplica(h.Anonim(), "prof", ("poza.png", Png), ("diploma.jpg", Jpeg)).Status(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PngRedenumitInPdfEsteRefuzat()
    {
        (await Aplica(h.Anonim(), "prof", ("poza.png", Png), ("dovada.pdf", Png)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este un PDF valid");
    }

    [Fact]
    public async Task ExecutabilRedenumitInJpgEsteRefuzat()
    {
        (await Aplica(h.Anonim(), "prof", ("poza.jpg", Exe), ("dovada.pdf", Pdf)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este un JPG valid");
    }

    [Theory]
    [InlineData("poza.gif")]
    [InlineData("poza.svg")]
    [InlineData("poza")]
    public async Task ExtensieNepermisaEsteRefuzata(string nume)
    {
        (await Aplica(h.Anonim(), "prof", (nume, Png), ("dovada.pdf", Pdf)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Tip de fișier nepermis");
    }

    [Fact]
    public async Task PdfulNuEsteAcceptatCaPoza()
    {
        await Aplica(h.Anonim(), "prof", ("poza.pdf", Pdf), ("dovada.pdf", Pdf)).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FisierulGolEsteRefuzat()
    {
        (await Aplica(h.Anonim(), "prof", ("poza.png", []), ("dovada.pdf", Pdf)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("MB");
    }

    [Fact]
    public async Task PozaPeste5MbEsteRefuzata()
    {
        var mare = new byte[5 * 1024 * 1024 + 1];
        Png.CopyTo(mare, 0);
        (await Aplica(h.Anonim(), "prof", ("poza.png", mare), ("dovada.pdf", Pdf)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("5 MB");
    }

    [Fact]
    public async Task FaraPozaEsteRefuzat()
    {
        (await Aplica(h.Anonim(), "prof", null, ("dovada.pdf", Pdf)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("fotografie");
    }

    [Fact]
    public async Task FaraDovadaEsteRefuzat()
    {
        (await Aplica(h.Anonim(), "prof", ("poza.png", Png), null).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("dovada calificării");
    }

    [Fact]
    public async Task FaraOrganizatieEsteRefuzat()
    {
        (await Aplica(h.Anonim(), "prof", ("poza.png", Png), ("dovada.pdf", Pdf), organizatii: "").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("cel puțin o organizație");
    }

    [Fact]
    public async Task OrganizatieInexistentaEsteRefuzata()
    {
        await Aplica(h.Anonim(), "prof", ("poza.png", Png), ("dovada.pdf", Pdf), organizatii: "1,99").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FaraMaterieEsteRefuzat()
    {
        (await Aplica(h.Anonim(), "prof", ("poza.png", Png), ("dovada.pdf", Pdf), materii: "").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("materie");
    }

    [Fact]
    public async Task TelefonulFolositEsteRefuzat()
    {
        (await Aplica(h.Anonim(), "prof", ("poza.png", Png), ("dovada.pdf", Pdf), tel: "0722123456").Mesaj(HttpStatusCode.Conflict)).ShouldContain("telefon");
    }

    [Fact]
    public async Task RefuzulNuLasaFisiereInStocare()
    {
        await Aplica(h.Anonim(), "prof", ("poza.png", Png), ("dovada.pdf", Png));
        await Aplica(h.Anonim(), "prof2", ("poza.jpg", Exe), ("dovada.pdf", Pdf));
        Directory.EnumerateFiles(h.DirStocare, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task NiciunFisierPartialNuRamane()
    {
        await Aplicant();
        Directory.EnumerateFiles(h.DirStocare, "*.partial", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task FisiereleAuNumeAleatoriiNuNumeleTrimis()
    {
        await Aplica(h.Anonim(), "prof", ("../../etc/poza.png", Png), ("dovada.pdf", Pdf)).Status(HttpStatusCode.Accepted);
        var p = await h.Db(db => db.ProfiluriMentor.SingleAsync(x => x.Utilizator.Username == "prof"));
        p.PozaUrl!.ShouldMatch(@"^poze/[0-9a-f]{32}\.png$");
    }

    [Fact]
    public async Task PozaAplicantuluiNuEstePublica()
    {
        var (_, id) = await Aplicant();
        await h.Anonim().Get($"/profesori/{id}/poza").Status(HttpStatusCode.NotFound);
        using var george = await h.Login("george");
        await george.Get($"/profesori/{id}/poza").Status(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AplicantulIsiVedePoza()
    {
        var (c, id) = await Aplicant();
        var r = await c.Get($"/profesori/{id}/poza").Status(HttpStatusCode.OK);
        r.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
    }

    [Fact]
    public async Task AdminulVedePozaAplicantului()
    {
        var (_, id) = await Aplicant();
        using var admin = await h.Login("admin");
        await admin.Get($"/profesori/{id}/poza").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PozaProfesoruluiAprobatEstePublica()
    {
        var irina = await h.Db(db => db.Utilizatori.Where(u => u.Username == "irina").Select(u => u.Id).SingleAsync());
        File.WriteAllBytes(Path.Combine(h.DirStocare, "fixture-photo.png"), Png);
        await h.Anonim().Get($"/profesori/{irina}/poza").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DovadaSeDescarcaCaAtasament()
    {
        var (_, id) = await Aplicant();
        using var admin = await h.Login("admin");
        var r = await admin.Get($"/admin/profesori/{id}/dovada").Status(HttpStatusCode.OK);
        r.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        r.Content.Headers.ContentDisposition.FileName!.Trim('"').ShouldBe($"dovada-calificare-{id}.pdf");
    }

    [Fact]
    public async Task DovadaNuEsteVizibilaAltora()
    {
        var (aplicant, id) = await Aplicant();
        using var george = await h.Login("george");
        await george.Get($"/admin/profesori/{id}/dovada").Status(HttpStatusCode.Forbidden);
        await aplicant.Get($"/admin/profesori/{id}/dovada").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminulOrganizatieiVedeDovadaMembrului()
    {
        var (_, id) = await Aplicant();
        using var director = await h.Login("director");
        var r = await director.Get($"/organizatia-mea/membri/{id}/dovada").Status(HttpStatusCode.OK);
        r.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Fact]
    public async Task DovadaInlocuitaStergeFisierulVechi()
    {
        var (c, _) = await Aplicant();
        var veche = await h.Db(db => db.ProfiluriMentor.Where(p => p.Utilizator.Username == "prof").Select(p => p.DovadaCalificareUrl).SingleAsync());
        using var f = new MultipartFormDataContent();
        f.Add(Raspunsuri.Fisier(Pdf), "Dovada", "dovada.pdf");
        await c.PostAsync("/utilizatori/me/setari/documente", f, Ct).Status(HttpStatusCode.OK);
        File.Exists(Path.Combine(h.DirStocare, veche!)).ShouldBeFalse();
    }

    [Fact]
    public async Task DocumentulInvalidLaInlocuirePastreazaFisierulVechi()
    {
        var (c, _) = await Aplicant();
        var veche = await h.Db(db => db.ProfiluriMentor.Where(p => p.Utilizator.Username == "prof").Select(p => p.DovadaCalificareUrl).SingleAsync());
        using var f = new MultipartFormDataContent();
        f.Add(Raspunsuri.Fisier(Exe), "Dovada", "dovada.pdf");
        await c.PostAsync("/utilizatori/me/setari/documente", f, Ct).Status(HttpStatusCode.BadRequest);
        File.Exists(Path.Combine(h.DirStocare, veche!)).ShouldBeTrue();
    }

    [Fact]
    public async Task CererileMultipartGoaleLaInregistrareSuntRefuzate()
    {
        using var f = new MultipartFormDataContent { { new StringContent("x"), "Altceva" } };
        (await h.Anonim().PostAsync("/autentificare/inregistrare-profesor", f, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AplicantulApareCaInAsteptareLaOrganizatie()
    {
        var (_, id) = await Aplicant();
        using var director = await h.Login("director");
        var membri = (await director.GetJson("/organizatia-mea/membri?stare=in_asteptare")).Lista();
        membri.ShouldContain(m => m.Int("utilizatorId") == id);
    }
}
