using System.Net;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Conturi;

/// <summary>Student registration and email confirmation: no session until the emailed code is confirmed, nothing reveals which emails exist.</summary>
public sealed class InregistrareTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:EmailuriPeOra"] = "100", ["Limite:AutentificariPeMinut"] = "100", ["Limite:InregistrariPeOra"] = "100",
    };

    private const string Email = "ana@example.invalid";

    private Task<HttpResponseMessage> Inregistreaza(HttpClient c, string username = "ana", string email = Email, string parola = Harness.ParolaTest, object? extra = null) =>
        c.Post("/autentificare/inregistrare", extra ?? new { nume = "Ana Pop", username, email, parola });

    private Task<HttpResponseMessage> Confirma(HttpClient c, string cod, string email = Email) =>
        c.Post("/autentificare/confirma-email", new { email, cod });

    [Fact]
    public async Task InregistrareaCereCodulDinEmail()
    {
        var r = await Inregistreaza(h.Anonim()).Json(HttpStatusCode.Accepted);
        r.Str("mesaj").ShouldContain("cod de confirmare");
        r.Str("email").ShouldBe(Email);
    }

    [Fact]
    public async Task InregistrareaNuDeschideSesiunea()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        await anonim.Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CodulAjungeLaAdresaData()
    {
        await Inregistreaza(h.Anonim());
        var mesaj = h.Emailuri.Trimise.ShouldHaveSingleItem();
        mesaj.Catre.ShouldBe(Email);
        mesaj.Subiect.ShouldContain("Codul de confirmare");
        h.Emailuri.UltimulCod(Email).Length.ShouldBe(6);
    }

    [Fact]
    public async Task ContulNeconfirmatNuPoateIntra()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        var r = await anonim.Post("/autentificare", new { identificator = "ana", parola = Harness.ParolaTest }).Json(HttpStatusCode.Forbidden);
        r.Str("cod").ShouldBe("email_neconfirmat");
        r.Str("email").ShouldBe(Email);
    }

    [Fact]
    public async Task CuParolaGresitaNuSeAflaCaEsteNeconfirmat()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        var r = await anonim.Post("/autentificare", new { identificator = "ana", parola = "gresita-123" }).Json(HttpStatusCode.Unauthorized);
        r.TryGetProperty("cod", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task EmailExistentPrimesteAcelasiRaspuns()
    {
        var anonim = h.Anonim();
        var nou = await Inregistreaza(anonim).Json(HttpStatusCode.Accepted);
        var dublu = await Inregistreaza(anonim, "altcineva", "GEORGE@example.invalid").Json(HttpStatusCode.Accepted);
        dublu.Str("mesaj").Replace("GEORGE@example.invalid", "x").ShouldBe(nou.Str("mesaj").Replace(Email, "x"));
    }

    [Fact]
    public async Task EmailExistentNuCreeazaAlDoileaCont()
    {
        await Inregistreaza(h.Anonim(), "altcineva", "GEORGE@example.invalid");
        (await h.Db(db => db.Utilizatori.CountAsync(u => u.Username == "altcineva"))).ShouldBe(0);
    }

    [Fact]
    public async Task ProprietarulAflaCaCinevaAIncercat()
    {
        await Inregistreaza(h.Anonim(), "altcineva", "george@example.invalid");
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "george@example.invalid" && m.Subiect.Contains("Cineva a încercat"));
    }

    [Fact]
    public async Task ProprietarulEsteAnuntatCelMultODataPeOra()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim, "x1", "george@example.invalid");
        await Inregistreaza(anonim, "x2", "george@example.invalid");
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
        h.Emailuri.Trimise.Count(m => m.Catre == "george@example.invalid").ShouldBe(1);
        h.Ceas.Advance(TimeSpan.FromMinutes(61));
        await Inregistreaza(anonim, "x3", "george@example.invalid");
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
        h.Emailuri.Trimise.Count(m => m.Catre == "george@example.invalid").ShouldBe(2);
    }

    [Fact]
    public async Task UsernameFolositEsteRefuzat()
    {
        (await Inregistreaza(h.Anonim(), "GEORGE", "nou@example.invalid").Mesaj(HttpStatusCode.Conflict)).ShouldContain("deja folosit");
    }

    [Theory]
    [InlineData("ana pop")]
    [InlineData("ana/pop")]
    [InlineData("ana@pop")]
    public async Task UsernameCuCaractereNepermiseEsteRefuzat(string username)
    {
        (await Inregistreaza(h.Anonim(), username).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("doar litere, cifre");
    }

    [Theory]
    [InlineData("ana.pop")]
    [InlineData("ana_pop-2")]
    [InlineData("Ștefan")]
    public async Task UsernameCuPunctCratimaSauDiacriticeEsteAcceptat(string username)
    {
        await Inregistreaza(h.Anonim(), username).Status(HttpStatusCode.Accepted);
    }

    [Theory]
    [InlineData("fara-arond")]
    [InlineData("doi@@example.invalid")]
    public async Task EmailInvalidEsteRefuzat(string email)
    {
        (await Inregistreaza(h.Anonim(), email: email).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("email");
    }

    [Fact]
    public async Task ParolaScurtaEsteRefuzata()
    {
        (await Inregistreaza(h.Anonim(), parola: "1234567").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("8 caractere");
    }

    [Fact]
    public async Task NumeleEsteObligatoriu()
    {
        (await h.Anonim().Post("/autentificare/inregistrare", new { nume = "  ", username = "ana", email = Email, parola = Harness.ParolaTest })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Nume");
    }

    [Fact]
    public async Task UsernamePreaLungEsteRefuzat()
    {
        (await Inregistreaza(h.Anonim(), new string('a', 41)).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("cel mult 40");
    }

    [Fact]
    public async Task ClasaSiLoculSuntSalvate()
    {
        await Inregistreaza(h.Anonim(), extra: new { nume = "Ana Pop", username = "ana", email = Email, parola = Harness.ParolaTest, judetId = 1, localitateId = 1026, clasa = "xi" })
            .Status(HttpStatusCode.Accepted);
        var u = await h.Db(db => db.Utilizatori.SingleAsync(x => x.Username == "ana"));
        (u.JudetId, u.LocalitateId, u.Clasa).ShouldBe((1, 1026, "XI"));
    }

    [Fact]
    public async Task ClasaNevalidaEsteRefuzata()
    {
        (await Inregistreaza(h.Anonim(), extra: new { nume = "Ana", username = "ana", email = Email, parola = Harness.ParolaTest, clasa = "VII" })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("IX, X, XI sau XII");
    }

    [Fact]
    public async Task LocalitateaDinAltJudetEsteRefuzata()
    {
        await Inregistreaza(h.Anonim(), extra: new { nume = "Ana", username = "ana", email = Email, parola = Harness.ParolaTest, judetId = 40, localitateId = 1026 })
            .Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OrganizatieInexistentaEsteRefuzata()
    {
        (await Inregistreaza(h.Anonim(), extra: new { nume = "Ana", username = "ana", email = Email, parola = Harness.ParolaTest, organizatieId = 999 })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Organizația nu există");
    }

    [Fact]
    public async Task CuOrganizatieSeTrimiteCerereaDeIntrare()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim, extra: new { nume = "Ana", username = "ana", email = Email, parola = Harness.ParolaTest, organizatieId = Harness.IdLiceu });
        await Confirma(anonim, h.Emailuri.UltimulCod(Email)).Status(HttpStatusCode.OK);
        var m = await h.Db(db => db.Set<MembruOrganizatie>().SingleAsync(x => x.Utilizator.Username == "ana"));
        (m.OrganizatieId, m.Rol, m.Stare).ShouldBe((Harness.IdLiceu, MembruOrganizatie.RolElev, MembruOrganizatie.InAsteptare));
    }

    [Fact]
    public async Task CodulGresitEsteRefuzat()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        (await Confirma(anonim, "000000").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este corect");
    }

    [Fact]
    public async Task CodulBunConfirmaSiDeschideSesiunea()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        var eu = await Confirma(anonim, h.Emailuri.UltimulCod(Email)).Json();
        eu.Str("username").ShouldBe("ana");
        eu.Str("rol").ShouldBe("elev");
        (await anonim.GetJson("/utilizatori/me")).Str("email").ShouldBe(Email);
        (await h.Db(db => db.Utilizatori.SingleAsync(u => u.Username == "ana"))).EmailConfirmatLa.ShouldNotBeNull();
    }

    [Fact]
    public async Task CodulAcceptaSpatiiSiEmailCuMajuscule()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        await Confirma(anonim, " " + h.Emailuri.UltimulCod(Email) + " ", "ANA@Example.Invalid").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CodulRetrimisIlInlocuiestePeCelVechi()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        var primul = h.Emailuri.UltimulCod(Email);
        await anonim.Post("/autentificare/retrimite-confirmare", new { email = Email }).Status(HttpStatusCode.OK);
        var alDoilea = h.Emailuri.UltimulCod(Email);
        if (alDoilea == primul) return; // one in a million: the same 6 digits again
        await Confirma(anonim, primul).Status(HttpStatusCode.BadRequest);
        await Confirma(anonim, alDoilea).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DupaCinciIncercariGresiteCodulNuMaiMerge()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        var cod = h.Emailuri.UltimulCod(Email);
        var gresit = cod == "111111" ? "222222" : "111111";
        for (var i = 0; i < 5; i++) await Confirma(anonim, gresit).Status(HttpStatusCode.BadRequest);
        await Confirma(anonim, cod).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnCodNouMergeDupaBlocare()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        for (var i = 0; i < 5; i++) await Confirma(anonim, "abcdef");
        await anonim.Post("/autentificare/retrimite-confirmare", new { email = Email });
        await Confirma(anonim, h.Emailuri.UltimulCod(Email)).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CodulMergeODataSiContulConfirmatNuMaiPrimesteCoduri()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        var cod = h.Emailuri.UltimulCod(Email);
        await Confirma(anonim, cod).Status(HttpStatusCode.OK);
        await Confirma(h.Anonim(), cod).Status(HttpStatusCode.BadRequest);
        var trimise = h.Emailuri.Trimise.Count;
        await anonim.Post("/autentificare/retrimite-confirmare", new { email = Email }).Status(HttpStatusCode.OK);
        h.Emailuri.Trimise.Count.ShouldBe(trimise);
    }

    [Fact]
    public async Task RetrimitereaLaUnEmailNecunoscutRaspundeLaFel()
    {
        var anonim = h.Anonim();
        var necunoscut = await anonim.Post("/autentificare/retrimite-confirmare", new { email = "nimeni@example.invalid" }).Json();
        await Inregistreaza(anonim);
        var cunoscut = await anonim.Post("/autentificare/retrimite-confirmare", new { email = Email }).Json();
        necunoscut.Str("mesaj").ShouldBe(cunoscut.Str("mesaj"));
    }

    [Fact]
    public async Task CodulExpiraOdataCuContul()
    {
        var anonim = h.Anonim();
        await Inregistreaza(anonim);
        var cod = h.Emailuri.UltimulCod(Email);
        h.Ceas.Advance(TimeSpan.FromHours(24.1));
        await Confirma(anonim, cod).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmailulCareNuPleacaSpuneCumSeRetrimite()
    {
        h.Emailuri.Esueaza = true;
        var r = await Inregistreaza(h.Anonim()).Json(HttpStatusCode.Accepted);
        r.Str("mesaj").ShouldContain("Retrimite codul");
        (await h.Db(db => db.Utilizatori.AnyAsync(u => u.Username == "ana"))).ShouldBeTrue();
    }

    [Fact]
    public async Task CodulNuApareNiciodataInJurnal()
    {
        await Inregistreaza(h.Anonim());
        var cod = h.Emailuri.UltimulCod(Email);
        h.Jurnal.Linii.ShouldNotContain(l => l.Contains(cod));
    }

    [Fact]
    public async Task ParolaEsteSalvataDoarCaHash()
    {
        await Inregistreaza(h.Anonim());
        var hash = await h.Db(db => db.Utilizatori.Where(u => u.Username == "ana").Select(u => u.ParolaHash).SingleAsync());
        hash.ShouldNotContain(Harness.ParolaTest);
        hash.Length.ShouldBeGreaterThan(40);
    }

    [Fact]
    public async Task PreaMulteInregistrariDeLaAcelasiIpSuntOprite()
    {
        await using var strict = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:InregistrariPeOra"] = "2" });
        var anonim = strict.Anonim();
        for (var i = 0; i < 2; i++)
            await anonim.Post("/autentificare/inregistrare", new { nume = "A", username = $"a{i}", email = $"a{i}@example.invalid", parola = Harness.ParolaTest })
                .Status(HttpStatusCode.Accepted);
        var r = await anonim.Post("/autentificare/inregistrare", new { nume = "A", username = "a9", email = "a9@example.invalid", parola = Harness.ParolaTest });
        r.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        r.Headers.RetryAfter.ShouldNotBeNull();
    }
}
