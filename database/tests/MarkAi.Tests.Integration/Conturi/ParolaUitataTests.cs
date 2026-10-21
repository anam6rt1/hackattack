using System.Net;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Tests.Suport;

namespace MarkAi.Tests.Integration.Conturi;

/// <summary>Forgotten password: a code by email or SMS, the same answer for unknown accounts, sessions closed after the change.</summary>
public sealed class ParolaUitataTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:AutentificariPeMinut"] = "100", ["Limite:EmailuriPeOra"] = "100", ["Limite:InregistrariPeOra"] = "100",
    };

    private const string Noua = "Parola-noua-2026!";

    private async Task<string> CerePrinEmail(string identificator = "george")
    {
        await h.Anonim().Post("/autentificare/parola-uitata", new { identificator, canal = "email" }).Status(HttpStatusCode.OK);
        return h.Emailuri.UltimulCod("george@example.invalid");
    }

    private Task<HttpResponseMessage> Schimba(string cod, string parola = Noua, string identificator = "george") =>
        h.Anonim().Post("/autentificare/parola-noua", new { identificator, cod, parola });

    private Task<HttpResponseMessage> Intra(string parola, string identificator = "george") =>
        h.Anonim().Post("/autentificare", new { identificator, parola });

    [Fact]
    public async Task RaspunsulEsteAcelasiPentruUnContInexistent()
    {
        var necunoscut = await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "nimeni", canal = "email" }).Json();
        var cunoscut = await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "george", canal = "email" }).Json();
        necunoscut.Str("mesaj").ShouldBe(cunoscut.Str("mesaj"));
    }

    [Fact]
    public async Task ContulInexistentNuPrimesteNimic()
    {
        await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "nimeni@example.invalid", canal = "email" });
        h.Emailuri.Trimise.ShouldBeEmpty();
        h.Sms.Trimise.ShouldBeEmpty();
    }

    [Fact]
    public async Task CodulSeTrimitePeEmail()
    {
        await CerePrinEmail();
        var m = h.Emailuri.Trimise.ShouldHaveSingleItem();
        m.Catre.ShouldBe("george@example.invalid");
        m.Subiect.ShouldContain("parolă nouă");
    }

    [Theory]
    [InlineData("george")]
    [InlineData("GEORGE@example.invalid")]
    [InlineData("  george  ")]
    public async Task ContulSeGasesteDupaEmailSauUsername(string identificator)
    {
        await h.Anonim().Post("/autentificare/parola-uitata", new { identificator, canal = "email" });
        h.Emailuri.Trimise.ShouldHaveSingleItem().Catre.ShouldBe("george@example.invalid");
    }

    [Fact]
    public async Task ParolaPreaScurtaEsteRefuzataSiCodulRamaneValabil()
    {
        var cod = await CerePrinEmail();
        await Schimba(cod, "scurta").Status(HttpStatusCode.BadRequest);
        await Schimba(cod).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CodulGresitEsteRefuzat()
    {
        var cod = await CerePrinEmail();
        await Schimba(cod == "000000" ? "111111" : "000000").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CodulSchimbaParola()
    {
        await Schimba(await CerePrinEmail()).Status(HttpStatusCode.OK);
        await Intra(Harness.ParolaTest).Status(HttpStatusCode.Unauthorized);
        await Intra(Noua).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CodulMergeODataSingura()
    {
        var cod = await CerePrinEmail();
        await Schimba(cod).Status(HttpStatusCode.OK);
        await Schimba(cod, "Inca-una-2026!").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CodulExpiraDupa15Minute()
    {
        var cod = await CerePrinEmail();
        h.Ceas.Advance(TimeSpan.FromMinutes(15.5));
        await Schimba(cod).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CodulUnuiContNuMergePentruAltul()
    {
        var cod = await CerePrinEmail();
        await Schimba(cod, identificator: "admin").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnCodNouIlAnuleazaPeCelVechi()
    {
        var vechi = await CerePrinEmail();
        var nou = await CerePrinEmail();
        if (nou == vechi) return;
        await Schimba(vechi).Status(HttpStatusCode.BadRequest);
        await Schimba(nou).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CelelalteSesiuniSuntInchise()
    {
        using var altDispozitiv = await h.Login("george");
        await Schimba(await CerePrinEmail());
        await altDispozitiv.Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProprietarulAflaCaParolaASchimbat()
    {
        await Schimba(await CerePrinEmail());
        await h.Serviciu<ProcesorMesaje>().RuleazaOdataAsync(Ct);
        h.Emailuri.Trimise.ShouldContain(m => m.Catre == "george@example.invalid" && m.Subiect.Contains("a fost schimbată"));
    }

    [Fact]
    public async Task PrinSmsLaTelefonulVerificat()
    {
        using var george = await h.Login("george");
        await h.VerificaTelefon(george, "0777111222");
        var sms = h.Sms.Trimise.Count;
        await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "george@example.invalid", canal = "sms" });
        h.Sms.Trimise.Count.ShouldBe(sms + 1);
        h.Sms.Trimise.Last().Catre.ShouldBe("+40777111222");
        await Schimba(h.Sms.UltimulCod(), "Prin-sms-2026!").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FaraTelefonVerificatCodulVinePeEmail()
    {
        await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "admin", canal = "sms" });
        h.Sms.Trimise.ShouldBeEmpty();
        h.Emailuri.Trimise.Last().Catre.ShouldBe("admin@example.invalid");
    }

    [Fact]
    public async Task TelefonulNeverificatNuPrimesteCodul()
    {
        // irina has a phone number from the fixtures, never verified by SMS.
        await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "irina", canal = "sms" });
        h.Sms.Trimise.ShouldBeEmpty();
        h.Emailuri.Trimise.Last().Catre.ShouldBe("irina@example.invalid");
    }

    [Fact]
    public async Task PestePlafonulDeSmsCodulVinePeEmail()
    {
        await using var plafon = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Sms:MaxPeZi"] = "1", ["Limite:EmailuriPeOra"] = "100" });
        using var george = await plafon.Login("george");
        await plafon.VerificaTelefon(george, "0777111222");
        await plafon.Anonim().Post("/autentificare/parola-uitata", new { identificator = "george", canal = "sms" }).Status(HttpStatusCode.OK);
        plafon.Sms.Trimise.Count.ShouldBe(1);
        plafon.Emailuri.Trimise.Last().Catre.ShouldBe("george@example.invalid");
    }

    [Fact]
    public async Task ResetareaPrinEmailConfirmaContul()
    {
        await h.Anonim().Post("/autentificare/inregistrare", new { nume = "Uitucul", username = "uituc", email = "uituc@example.invalid", parola = Harness.ParolaTest });
        await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "uituc", canal = "email" });
        await Schimba(h.Emailuri.UltimulCod("uituc@example.invalid"), identificator: "uituc").Status(HttpStatusCode.OK);
        await Intra(Noua, "uituc").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmailulCareNuPleacaNuDezvaluieContul()
    {
        h.Emailuri.Esueaza = true;
        var r = await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "george", canal = "email" }).Json();
        r.Str("mesaj").ShouldStartWith("Dacă există un cont");
        h.Jurnal.Linii.ShouldContain(l => l.Contains("Password reset code could not be sent"));
    }

    [Fact]
    public async Task IdentificatorulGolNuTrimiteNimic()
    {
        await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "", canal = "email" }).Status(HttpStatusCode.OK);
        h.Emailuri.Trimise.ShouldBeEmpty();
    }
}
