using System.Net;
using System.Net.Http.Headers;
using MarkAi.Tests.Suport;

namespace MarkAi.Tests.Integration.Conturi;

/// <summary>Signing in and out: the httpOnly session cookie, bearer tokens, revocation on every device, login rate limit.</summary>
public sealed class SesiuniTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?> { ["Limite:AutentificariPeMinut"] = "100" };

    private Task<HttpResponseMessage> Intra(HttpClient c, string identificator, string parola = Harness.ParolaTest) =>
        c.Post("/autentificare", new { identificator, parola });

    [Theory]
    [InlineData("george")]
    [InlineData("GEORGE")]
    [InlineData("george@example.invalid")]
    [InlineData(" George@Example.Invalid ")]
    public async Task IntrareCuUsernameSauEmail(string identificator)
    {
        var eu = await Intra(h.Anonim(), identificator).Json();
        eu.Str("username").ShouldBe("george");
    }

    [Fact]
    public async Task ParolaGresitaSiContulInexistentAuAcelasiMesaj()
    {
        var gresita = await Intra(h.Anonim(), "george", "gresita-123").Mesaj(HttpStatusCode.Unauthorized);
        var inexistent = await Intra(h.Anonim(), "nimeni").Mesaj(HttpStatusCode.Unauthorized);
        gresita.ShouldBe(inexistent);
    }

    [Fact]
    public async Task CookieulDeSesiuneEsteHttpOnlySiLax()
    {
        var r = await Intra(h.Anonim(), "george").Status(HttpStatusCode.OK);
        var cookie = r.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("markai_sesiune="));
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=lax", Case.Insensitive);
        cookie.ShouldContain("path=/", Case.Insensitive);
    }

    [Fact]
    public async Task CookieulSecureCandSiteulEPeHttps()
    {
        await using var https = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Autentificare:CookieSecure"] = "true" });
        var r = await https.Anonim().Post("/autentificare", new { identificator = "george", parola = Harness.ParolaTest });
        r.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("markai_sesiune=")).ShouldContain("secure", Case.Insensitive);
    }

    [Fact]
    public async Task RolulPrincipalEsteCelMaiInalt()
    {
        (await Intra(h.Anonim(), "director").Json()).Str("rol").ShouldBe("organizatie");
        (await Intra(h.Anonim(), "irina").Json()).Str("rol").ShouldBe("profesor");
        (await Intra(h.Anonim(), "admin").Json()).Str("rol").ShouldBe("admin");
    }

    [Fact]
    public async Task ProfesorulIsiVedeStareaSiOrganizatiile()
    {
        var eu = await Intra(h.Anonim(), "irina").Json();
        eu.Str("stareProfesor").ShouldBe("aprobat");
        eu.Lista("organizatii").ShouldHaveSingleItem().Str("nume").ShouldBe("Liceul de Test");
    }

    [Fact]
    public async Task AdministratorulOrganizatieiIsiVedeOrganizatia()
    {
        var eu = await Intra(h.Anonim(), "director").Json();
        eu.GetProperty("organizatieAdministrata").Int("id").ShouldBe(Harness.IdLiceu);
    }

    [Fact]
    public async Task ElevulNuAreStareDeProfesor()
    {
        (await Intra(h.Anonim(), "george").Json()).EsteNull("stareProfesor").ShouldBeTrue();
    }

    [Fact]
    public async Task IesireaStergeCookieul()
    {
        using var george = await h.Login("george");
        var r = await george.Post("/autentificare/iesire").Status(HttpStatusCode.NoContent);
        r.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("markai_sesiune=;"));
        await george.Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IesireaPesteTotInchideToateSesiunile()
    {
        using var unu = await h.Login("george");
        using var doi = await h.Login("george");
        await unu.Post("/autentificare/iesire-peste-tot").Status(HttpStatusCode.NoContent);
        await unu.Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
        await doi.Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IesireaPesteTotNuAtingeAlteConturi()
    {
        using var george = await h.Login("george");
        using var irina = await h.Login("irina");
        await george.Post("/autentificare/iesire-peste-tot");
        await irina.Get("/utilizatori/me").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DupaIesireaPesteTotSePoateIntraDinNou()
    {
        using var george = await h.Login("george");
        await george.Post("/autentificare/iesire-peste-tot");
        using var nou = await h.Login("george");
        await nou.Get("/utilizatori/me").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IesireaPesteTotCereCont()
    {
        await h.Anonim().Post("/autentificare/iesire-peste-tot").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenulMergeSiCaAntetBearer()
    {
        var r = await Intra(h.Anonim(), "george");
        var token = r.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("markai_sesiune=")).Split(';')[0]["markai_sesiune=".Length..];
        var api = h.Brut();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await api.GetJson("/utilizatori/me")).Str("username").ShouldBe("george");
    }

    [Fact]
    public async Task TokenulFalsificatEsteRefuzat()
    {
        var api = h.Brut();
        api.DefaultRequestHeaders.Add("Cookie", "markai_sesiune=eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.semnatura");
        await api.Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LimitaDeIntrariPeMinutPerIp()
    {
        await using var strict = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:AutentificariPeMinut"] = "3" });
        var anonim = strict.Anonim();
        for (var i = 0; i < 3; i++) await anonim.Post("/autentificare", new { identificator = "george", parola = "gresita-123" });
        var r = await anonim.Post("/autentificare", new { identificator = "george", parola = Harness.ParolaTest });
        r.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await r.Json()).Str("mesaj").ShouldContain("Prea multe încercări");
    }

    [Fact]
    public async Task ProfilulCereCont()
    {
        await h.Anonim().Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
    }
}
