using System.Net;
using System.Net.Http.Json;
using System.Text;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Securitate;

/// <summary>Request-level protections: error hygiene, security headers, the CSRF header, body size, forwarded client IP, rate limits.</summary>
public sealed class SecuritateTests : TestCuHarness
{
    [Fact]
    public async Task EroareaNeasteptataAreUnMesajGeneric()
    {
        var r = await h.Anonim().Get(Harness.RutaEroareInterna).Status(HttpStatusCode.InternalServerError);
        var text = await r.Content.ReadAsStringAsync(Ct);
        text.ShouldNotContain("Sequence");
        text.ShouldNotContain("intern");
        text.ShouldNotContain("   at ");
        (await r.Json()).Str("mesaj").ShouldBe("A apărut o eroare. Încearcă din nou.");
    }

    [Fact]
    public async Task EroareaInternaEsteInJurnal()
    {
        await h.Anonim().Get(Harness.RutaEroareInterna);
        h.Jurnal.Linii.ShouldContain(l => l.Contains(Harness.MesajIntern));
    }

    [Theory]
    [InlineData("/materii")]
    [InlineData("/judete")]
    [InlineData("/utilizatori/me")]
    [InlineData("/nu-exista")]
    public async Task AntetedeSecuritatePeOriceRaspuns(string url)
    {
        var r = await h.Anonim().Get(url);
        r.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
        r.Headers.GetValues("X-Frame-Options").Single().ShouldBe("DENY");
        r.Headers.GetValues("Referrer-Policy").Single().ShouldBe("no-referrer");
        var csp = r.Headers.GetValues("Content-Security-Policy").Single();
        csp.ShouldContain("default-src 'none'");
        csp.ShouldContain("frame-ancestors 'none'");
        csp.ShouldContain("sandbox");
    }

    [Fact]
    public async Task SwaggerNuPrimesteCspStrict()
    {
        var r = await h.Brut().Get("/swagger/index.html");
        r.Headers.Contains("Content-Security-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task PostFaraAntetulCsrfEsteRefuzatChiarSiLaIntrare()
    {
        var r = await h.Brut().PostAsJsonAsync("/autentificare", new { identificator = "george", parola = Harness.ParolaTest }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await r.Json()).Str("mesaj").ShouldContain("Reîncarcă pagina");
    }

    [Fact]
    public async Task AntetulCsrfTrebuieSaFie1()
    {
        var c = h.Brut();
        c.DefaultRequestHeaders.Add("X-MarkAi", "da");
        await c.Post("/autentificare", new { identificator = "george", parola = Harness.ParolaTest }).Status(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task OriceModificareCereAntetulCsrf(string metoda)
    {
        var r = await h.Brut().SendAsync(new HttpRequestMessage(new HttpMethod(metoda), "/utilizatori/me"), Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetNuCereAntetulCsrf()
    {
        (await h.Brut().Get("/materii")).StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        (await h.Brut().Get("/judete")).IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task WebhookurileTwilioNuCerAntetulCsrf()
    {
        // Refused for the signature (403 with a different body), not for CSRF.
        var r = await h.Brut().PostAsync("/twilio/voce/stare", new FormUrlEncodedContent(new Dictionary<string, string> { ["CallSid"] = "CA1" }), Ct);
        (await r.Content.ReadAsStringAsync(Ct)).ShouldNotContain("Reîncarcă pagina");
    }

    [Fact]
    public async Task CorpulJsonPeste2MbEsteRefuzat()
    {
        using var george = await h.Login("george");
        var mare = new StringContent("{\"nume\":\"" + new string('a', 3 * 1024 * 1024) + "\"}", Encoding.UTF8, "application/json");
        (await george.PatchAsync("/utilizatori/me", mare, Ct)).StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task CorpulSub2MbTrece()
    {
        using var george = await h.Login("george");
        var mic = new StringContent("{\"nume\":\"George\",\"umplutura\":\"" + new string('a', 1024 * 1024) + "\"}", Encoding.UTF8, "application/json");
        (await george.PatchAsync("/utilizatori/me", mic, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchPeProfilIgnoraTelefonul()
    {
        using var george = await h.Login("george");
        await george.Patch("/utilizatori/me", new { telefon = "0722123456" });
        (await h.Db(db => db.Utilizatori.Where(u => u.Username == "george").Select(u => u.Telefon).SingleAsync())).ShouldBeNull();
    }

    [Fact]
    public async Task IpulDinXForwardedForSeparaLimitele()
    {
        await using var strict = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:AutentificariPeMinut"] = "2" });
        HttpClient Din(string ip)
        {
            var c = strict.Anonim();
            c.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
            return c;
        }
        var a = Din("203.0.113.1");
        for (var i = 0; i < 2; i++) await a.Post("/autentificare", new { identificator = "x", parola = "y" });
        await a.Post("/autentificare", new { identificator = "x", parola = "y" }).Status(HttpStatusCode.TooManyRequests);
        await Din("203.0.113.2").Post("/autentificare", new { identificator = "x", parola = "y" }).Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DoarUltimaAdresaDinXForwardedForConteaza()
    {
        await using var strict = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:AutentificariPeMinut"] = "2" });
        // A client cannot escape the limit by prepending invented addresses: only the last hop (cloudflared's) counts.
        for (var i = 0; i < 3; i++)
        {
            var c = strict.Anonim();
            c.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.0.0.{i}, 203.0.113.9");
            var r = await c.Post("/autentificare", new { identificator = "x", parola = "y" });
            if (i == 2) r.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        }
    }

    [Fact]
    public async Task LimitaGlobalaPeUtilizator()
    {
        await using var strict = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:CereriPeMinut"] = "5" });
        using var george = await strict.Login("george");
        var coduri = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++) coduri.Add((await george.Get("/utilizatori/me")).StatusCode);
        coduri.ShouldContain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RaspunsulLa429AreRetryAfter()
    {
        await using var strict = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:AutentificariPeMinut"] = "1" });
        var c = strict.Anonim();
        await c.Post("/autentificare", new { identificator = "x", parola = "y" });
        var r = await c.Post("/autentificare", new { identificator = "x", parola = "y" });
        r.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        int.Parse(r.Headers.GetValues("Retry-After").Single()).ShouldBeInRange(1, 60);
    }

    [Fact]
    public async Task UnsprezeceIncercariDeIntrarePeMinutSuntOprite()
    {
        var anonim = h.Anonim();
        HttpStatusCode ultim = 0;
        for (var i = 0; i < 11; i++) ultim = (await anonim.Post("/autentificare", new { identificator = "nimeni", parola = "gresita" })).StatusCode;
        ultim.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task EroareaDeUnicitateDevine409()
    {
        // Two subjects with the same code: the database's unique index answers, mapped to a clear 409.
        using var admin = await h.Login("admin");
        await admin.Post("/materii", new { denumire = "Chimie", cod = "chimie" }).Status(HttpStatusCode.Created);
        (await admin.Post("/materii", new { denumire = "Chimie 2", cod = "chimie" }).Mesaj(HttpStatusCode.Conflict)).ShouldContain("Există deja");
    }

    [Fact]
    public async Task ResursaInexistentaEste404CuMesaj()
    {
        using var george = await h.Login("george");
        (await george.Get("/parcursuri/999999")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorsPermiteDoarOriginileConfigurate()
    {
        var c = h.Brut();
        var cerere = new HttpRequestMessage(HttpMethod.Options, "/autentificare");
        cerere.Headers.Add("Origin", "https://atacator.example");
        cerere.Headers.Add("Access-Control-Request-Method", "POST");
        var r = await c.SendAsync(cerere, Ct);
        r.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();

        cerere = new HttpRequestMessage(HttpMethod.Options, "/autentificare");
        cerere.Headers.Add("Origin", "http://localhost:3000");
        cerere.Headers.Add("Access-Control-Request-Method", "POST");
        r = await c.SendAsync(cerere, Ct);
        r.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe("http://localhost:3000");
    }

    [Fact]
    public async Task UrlurileWebhookurilorNuAjungInJurnal()
    {
        await h.Brut().PostAsync("/twilio/voce?s=1&k=cheie-secreta-de-test", new FormUrlEncodedContent([]), Ct);
        h.Jurnal.Linii.ShouldNotContain(l => l.Contains("cheie-secreta-de-test"));
    }
}
