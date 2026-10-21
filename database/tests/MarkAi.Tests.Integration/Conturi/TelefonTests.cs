using System.Net;
using MarkAi.Data.Entities;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Conturi;

/// <summary>Phone numbers are verified by an SMS code before they are saved; SMS cost money, so there is a daily cap.</summary>
public sealed class TelefonTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Sms:MaxPeZi"] = "3", ["Limite:SmsPeOra"] = "100", ["Limite:AutentificariPeMinut"] = "100",
    };

    private const string Cod = "/utilizatori/me/setari/telefon/cod", Confirma = "/utilizatori/me/setari/telefon/confirma";

    [Fact]
    public async Task FaraTelefonVerificatNuSePoateSuna()
    {
        using var george = await h.Login("george");
        (await george.Post("/linia-ai/suna-ma")).StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CodulSeTrimiteLaNumarulNormalizat()
    {
        using var george = await h.Login("george");
        var r = await george.Post(Cod, new { telefon = "0766 111 222" }).Json();
        r.Str("telefon").ShouldBe("+40766111222");
        h.Sms.Trimise.ShouldHaveSingleItem().Catre.ShouldBe("+40766111222");
        h.Sms.UltimulCod().Length.ShouldBe(6);
    }

    [Fact]
    public async Task NumarulNuSeSalveazaInainteDeCod()
    {
        using var george = await h.Login("george");
        await george.Post(Cod, new { telefon = "0766111222" });
        (await george.GetJson("/utilizatori/me")).EsteNull("telefon").ShouldBeTrue();
    }

    [Fact]
    public async Task CodulGresitEsteRefuzat()
    {
        using var george = await h.Login("george");
        await george.Post(Cod, new { telefon = "0766111222" });
        await george.Post(Confirma, new { telefon = "0766111222", cod = "000000" }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CodulMergeDoarPentruNumarulLaCareAFostTrimis()
    {
        using var george = await h.Login("george");
        await george.Post(Cod, new { telefon = "0766111222" });
        await george.Post(Confirma, new { telefon = "0766999999", cod = h.Sms.UltimulCod() }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CodulBunVerificaSiSalveazaNumarul()
    {
        using var george = await h.Login("george");
        await george.Post(Cod, new { telefon = "0766111222" });
        await george.Post(Confirma, new { telefon = "+40 766 111 222", cod = h.Sms.UltimulCod() }).Status(HttpStatusCode.OK);
        var eu = await george.GetJson("/utilizatori/me");
        eu.Str("telefon").ShouldBe("+40766111222");
        eu.Bool("telefonVerificat").ShouldBeTrue();
    }

    [Fact]
    public async Task CodulExpiraDupaZeceMinute()
    {
        using var george = await h.Login("george");
        await george.Post(Cod, new { telefon = "0766111222" });
        h.Ceas.Advance(TimeSpan.FromMinutes(10.5));
        await george.Post(Confirma, new { telefon = "0766111222", cod = h.Sms.UltimulCod() }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CodulAltuiContNuMerge()
    {
        using var george = await h.Login("george");
        using var admin = await h.Login("admin");
        await george.Post(Cod, new { telefon = "0766111222" });
        await admin.Post(Confirma, new { telefon = "0766111222", cod = h.Sms.UltimulCod() }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NumarulAltuiContEsteRefuzat()
    {
        using var george = await h.Login("george");
        (await george.Post(Cod, new { telefon = "0722123456" }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("altui cont");
        h.Sms.Trimise.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("12")]
    [InlineData("07abc")]
    [InlineData("+40 7661112223334445")]
    public async Task NumarulNevalidEsteRefuzat(string telefon)
    {
        using var george = await h.Login("george");
        (await george.Post(Cod, new { telefon }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este valid");
    }

    [Fact]
    public async Task SchimbareaNumaruluiInProfilScoateVerificarea()
    {
        using var george = await h.Login("george");
        await h.VerificaTelefon(george, "0766111222");
        await george.Patch("/utilizatori/me/setari", new { nume = "George Pavel", username = "george", telefon = "0766222333" }).Status(HttpStatusCode.OK);
        (await george.GetJson("/utilizatori/me")).Bool("telefonVerificat").ShouldBeFalse();
    }

    [Fact]
    public async Task AcelasiNumarInProfilPastreazaVerificarea()
    {
        using var george = await h.Login("george");
        await h.VerificaTelefon(george, "0766111222");
        await george.Patch("/utilizatori/me/setari", new { nume = "George Nou", username = "george", telefon = "0766111222" });
        (await george.GetJson("/utilizatori/me")).Bool("telefonVerificat").ShouldBeTrue();
    }

    [Fact]
    public async Task ElevulIsiPoateStergeNumarul()
    {
        using var george = await h.Login("george");
        await h.VerificaTelefon(george, "0766111222");
        await george.Delete("/utilizatori/me/setari/telefon").Status(HttpStatusCode.OK);
        var eu = await george.GetJson("/utilizatori/me");
        eu.EsteNull("telefon").ShouldBeTrue();
        eu.Bool("telefonVerificat").ShouldBeFalse();
    }

    [Fact]
    public async Task ProfesorulNuIsiPoateStergeNumarul()
    {
        using var irina = await h.Login("irina");
        (await irina.Delete("/utilizatori/me/setari/telefon").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("obligatoriu");
    }

    [Fact]
    public async Task PlafonulZilnicOpresteSmsurile()
    {
        using var george = await h.Login("george");
        for (var i = 0; i < 3; i++) await george.Post(Cod, new { telefon = $"076633344{i}" }).Status(HttpStatusCode.OK);
        (await george.Post(Cod, new { telefon = "0766444555" }).Mesaj(HttpStatusCode.Conflict)).ShouldContain("limita zilnică");
        h.Sms.Trimise.Count.ShouldBe(3);
    }

    [Fact]
    public async Task PlafonulEstePePlatformaNuPeCont()
    {
        using var george = await h.Login("george");
        using var admin = await h.Login("admin");
        for (var i = 0; i < 3; i++) await george.Post(Cod, new { telefon = $"076633344{i}" });
        await admin.Post(Cod, new { telefon = "0766444555" }).Status(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PlafonulSeReiaDupa24DeOre()
    {
        using var george = await h.Login("george");
        for (var i = 0; i < 3; i++) await george.Post(Cod, new { telefon = $"076633344{i}" });
        h.Ceas.Advance(TimeSpan.FromHours(24.1));
        await george.Post(Cod, new { telefon = "0766444555" }).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FiecareSmsEsteInregistratFaraCod()
    {
        using var george = await h.Login("george");
        await george.Post(Cod, new { telefon = "0766111222" });
        var rand = await h.Db(db => db.Set<MesajIesire>().SingleAsync(m => m.Canal == "sms"));
        (rand.Catre, rand.Tip, rand.Furnizor).ShouldBe(("+40766111222", "cod_telefon", "fals"));
        rand.TrimisLa.ShouldNotBeNull();
        rand.Text.ShouldBeNull();
    }

    [Fact]
    public async Task LimitaPeOraPeContOpresteCererile()
    {
        await using var strict = await Harness.Porneste(setari: new Dictionary<string, string?> { ["Limite:SmsPeOra"] = "2" });
        using var george = await strict.Login("george");
        await george.Post(Cod, new { telefon = "0766111220" }).Status(HttpStatusCode.OK);
        await george.Post(Cod, new { telefon = "0766111221" }).Status(HttpStatusCode.OK);
        await george.Post(Cod, new { telefon = "0766111222" }).Status(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task FaraContNuSeTrimiteCod()
    {
        await h.Anonim().Post(Cod, new { telefon = "0766111222" }).Status(HttpStatusCode.Unauthorized);
    }
}
