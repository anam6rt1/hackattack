using System.Net;
using System.Text.Json;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Conturi;

/// <summary>Account settings: profile fields, place and school year, emailed one-time codes for password and email changes.</summary>
public sealed class SetariContTests : TestCuHarness
{
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:AutentificariPeMinut"] = "100", ["Limite:EmailuriPeOra"] = "100",
    };

    private const string Url = "/utilizatori/me/setari";
    private const string ParolaNoua = "New-test-password-2026!";

    private static async Task<string> CereCod(HttpClient c, string scop, string valoare, string parola = Harness.ParolaTest) =>
        (await c.Post(Url + "/confirmari", new { scop, parolaActuala = parola, valoareNoua = valoare }).Json()).Str("id");

    private static Task<HttpResponseMessage> Finalizeaza(HttpClient c, string id, string cod) =>
        c.Post(Url + "/confirmari/finalizeaza", new { id, cod });

    [Fact]
    public async Task FaraContNuSeVadSetarile()
    {
        await h.Anonim().Get(Url).Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ElevulNuVedeCampuriDeProfesor()
    {
        using var george = await h.Login("george");
        var p = await george.GetJson(Url);
        p.GetProperty("utilizator").Str("rol").ShouldBe("elev");
        p.EsteNull("profesor").ShouldBeTrue();
    }

    [Fact]
    public async Task ElevulNuPoateTrimiteCampuriDeProfesor()
    {
        using var george = await h.Login("george");
        await george.Patch(Url, new { nume = "George Pavel", username = "george", telefon = "", descriere = "pretend teacher", materieIds = new[] { 1 } })
            .Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ElevulIsiSalveazaDatele()
    {
        using var george = await h.Login("george");
        await george.Patch(Url, new { nume = "George Actualizat", username = "george_nou", telefon = "0722111222" }).Status(HttpStatusCode.OK);
        var u = (await george.GetJson(Url)).GetProperty("utilizator");
        (u.Str("nume"), u.Str("username"), u.Str("telefon")).ShouldBe(("George Actualizat", "george_nou", "+40722111222"));
    }

    [Fact]
    public async Task UsernameulAltcuivaEsteRefuzat()
    {
        using var george = await h.Login("george");
        (await george.Patch(Url, new { nume = "George", username = "IRINA", telefon = "" }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("deja folosit");
    }

    [Fact]
    public async Task TelefonulAltcuivaEsteRefuzat()
    {
        using var george = await h.Login("george");
        (await george.Patch(Url, new { nume = "George", username = "george", telefon = "0722123456" }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("altui cont");
    }

    [Fact]
    public async Task UsernameCuSpatiuEsteRefuzat()
    {
        using var george = await h.Login("george");
        await george.Patch(Url, new { nume = "George", username = "george pavel", telefon = "" }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FaraAntetulCsrfModificareaEsteRefuzata()
    {
        using var george = await h.Login("george");
        george.DefaultRequestHeaders.Remove("X-MarkAi");
        await george.Patch(Url, new { nume = "George", username = "george", telefon = "" }).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdministratorulIsiVedeSetarile()
    {
        using var admin = await h.Login("admin");
        (await admin.GetJson(Url)).GetProperty("utilizator").Str("rol").ShouldBe("admin");
    }

    [Fact]
    public async Task ProfesorulIsiVedeStareaDovezii()
    {
        using var irina = await h.Login("irina");
        var p = (await irina.GetJson(Url)).GetProperty("profesor");
        p.Bool("areDovada").ShouldBeTrue();
        p.Bool("arePoza").ShouldBeTrue();
        p.Lista("materieIds").Select(x => x.GetInt32()).ShouldBe([1]);
    }

    [Fact]
    public async Task ProfesorulIsiSchimbaMateriileSiRamaneAprobat()
    {
        using var irina = await h.Login("irina");
        await irina.Patch(Url, new { nume = "Irina Marin", username = "irina", telefon = "0722123456", descriere = "Profil actualizat", materieIds = new[] { 1, 2 } })
            .Status(HttpStatusCode.OK);
        var p = (await irina.GetJson(Url)).GetProperty("profesor");
        p.Str("stareVerificare").ShouldBe("aprobat");
        p.Str("descriere").ShouldBe("Profil actualizat");
        p.Lista("materieIds").Select(x => x.GetInt32()).Order().ShouldBe([1, 2]);
    }

    [Fact]
    public async Task ProfesorulTrebuieSaAibaTelefon()
    {
        using var irina = await h.Login("irina");
        (await irina.Patch(Url, new { nume = "Irina", username = "irina", telefon = "", descriere = "x", materieIds = new[] { 1 } })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("obligatoriu");
    }

    [Fact]
    public async Task ProfesorulTrebuieSaAibaOMaterieValida()
    {
        using var irina = await h.Login("irina");
        await irina.Patch(Url, new { nume = "Irina", username = "irina", telefon = "0722123456", descriere = "x", materieIds = Array.Empty<int>() }).Status(HttpStatusCode.BadRequest);
        await irina.Patch(Url, new { nume = "Irina", username = "irina", telefon = "0722123456", descriere = "x", materieIds = new[] { 999 } }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ElevulIsiAlegeLoculSiClasa()
    {
        using var george = await h.Login("george");
        await george.Put(Url + "/loc", new { judetId = 1, localitateId = 1026, clasa = "x" }).Status(HttpStatusCode.OK);
        var eu = await george.GetJson("/utilizatori/me");
        (eu.Int("judetId"), eu.Int("localitateId"), eu.Str("clasa")).ShouldBe((1, 1026, "X"));
    }

    [Fact]
    public async Task FaraJudetLoculSeSterge()
    {
        using var george = await h.Login("george");
        await george.Put(Url + "/loc", new { judetId = 1, localitateId = 1026, clasa = "X" });
        await george.Put(Url + "/loc", new { judetId = (int?)null, localitateId = (int?)null, clasa = "X" }).Status(HttpStatusCode.OK);
        var eu = await george.GetJson("/utilizatori/me");
        eu.EsteNull("judetId").ShouldBeTrue();
        eu.EsteNull("localitateId").ShouldBeTrue();
    }

    [Fact]
    public async Task ProfesorulNuAreClasa()
    {
        using var irina = await h.Login("irina");
        await irina.Put(Url + "/loc", new { judetId = 40, localitateId = 179141, clasa = "XII" }).Status(HttpStatusCode.OK);
        (await irina.GetJson("/utilizatori/me")).EsteNull("clasa").ShouldBeTrue();
    }

    [Fact]
    public async Task JudetulInexistentEsteRefuzat()
    {
        using var george = await h.Login("george");
        (await george.Put(Url + "/loc", new { judetId = 77, localitateId = (int?)null, clasa = (string?)null }).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("județul");
    }

    [Fact]
    public async Task CodulPentruParolaMergeLaEmailulContului()
    {
        using var george = await h.Login("george");
        await CereCod(george, "parola", ParolaNoua);
        h.Mail.Email.ShouldBe("george@example.invalid");
    }

    [Fact]
    public async Task ParolaActualaGresitaEsteRefuzata()
    {
        using var george = await h.Login("george");
        (await george.Post(Url + "/confirmari", new { scop = "parola", parolaActuala = "gresita-123", valoareNoua = ParolaNoua })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Parola actuală");
    }

    [Fact]
    public async Task CodulEsteLegatDeCont()
    {
        using var george = await h.Login("george");
        using var irina = await h.Login("irina");
        var id = await CereCod(george, "parola", ParolaNoua);
        await Finalizeaza(irina, id, h.Mail.Code).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CodulGresitEsteRefuzat()
    {
        using var george = await h.Login("george");
        var id = await CereCod(george, "parola", ParolaNoua);
        await Finalizeaza(george, id, "bad").Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ParolaSeSchimbaDoarCuCodulBun()
    {
        using var george = await h.Login("george");
        var id = await CereCod(george, "parola", ParolaNoua);
        await Finalizeaza(george, id, h.Mail.Code).Status(HttpStatusCode.OK);
        await h.Anonim().Post("/autentificare", new { identificator = "george", parola = ParolaNoua }).Status(HttpStatusCode.OK);
        await h.Anonim().Post("/autentificare", new { identificator = "george", parola = Harness.ParolaTest }).Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CodulFolositNuMaiMerge()
    {
        using var george = await h.Login("george");
        var id = await CereCod(george, "parola", ParolaNoua);
        var cod = h.Mail.Code;
        await Finalizeaza(george, id, cod);
        await Finalizeaza(george, id, cod).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcestDispozitivRamaneInContIarCelelalteIes()
    {
        using var george = await h.Login("george");
        using var altul = await h.Login("george");
        var id = await CereCod(george, "parola", ParolaNoua);
        await Finalizeaza(george, id, h.Mail.Code);
        await george.Get("/utilizatori/me").Status(HttpStatusCode.OK);
        await altul.Get("/utilizatori/me").Status(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OCerereNouaInUnMinutEsteRefuzata()
    {
        using var george = await h.Login("george");
        await CereCod(george, "parola", ParolaNoua);
        (await george.Post(Url + "/confirmari", new { scop = "email", parolaActuala = Harness.ParolaTest, valoareNoua = "new@example.invalid" })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("un minut");
    }

    [Fact]
    public async Task CodulExpiraDupaCinciMinute()
    {
        using var george = await h.Login("george");
        var id = await CereCod(george, "email", "new@example.invalid");
        h.Ceas.Advance(TimeSpan.FromMinutes(6));
        await Finalizeaza(george, id, h.Mail.Code).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmailulSeSchimbaCuCodul()
    {
        using var george = await h.Login("george");
        var id = await CereCod(george, "email", "New@Example.invalid");
        h.Mail.Email.ShouldBe("george@example.invalid");
        await Finalizeaza(george, id, h.Mail.Code).Status(HttpStatusCode.OK);
        (await george.GetJson(Url)).GetProperty("utilizator").Str("email").ShouldBe("new@example.invalid");
    }

    [Fact]
    public async Task EmailulFolositDeAltContEsteRefuzat()
    {
        using var george = await h.Login("george");
        (await george.Post(Url + "/confirmari", new { scop = "email", parolaActuala = Harness.ParolaTest, valoareNoua = "irina@example.invalid" })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("deja folosit");
    }

    [Fact]
    public async Task ParolaNouaIdenticaEsteRefuzata()
    {
        using var george = await h.Login("george");
        (await george.Post(Url + "/confirmari", new { scop = "parola", parolaActuala = Harness.ParolaTest, valoareNoua = Harness.ParolaTest })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("diferită");
    }

    [Theory]
    [InlineData("scurta")]
    [InlineData(null)]
    public async Task ParolaNouaNevalidaEsteRefuzata(string? parola)
    {
        using var george = await h.Login("george");
        await george.Post(Url + "/confirmari", new { scop = "parola", parolaActuala = Harness.ParolaTest, valoareNoua = parola }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScopulNecunoscutEsteRefuzat()
    {
        using var george = await h.Login("george");
        (await george.Post(Url + "/confirmari", new { scop = "rol", parolaActuala = Harness.ParolaTest, valoareNoua = "admin" })
            .Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nevalid");
    }

    [Fact]
    public async Task DupaCinciIncercariGresiteCodulEsteAnulat()
    {
        using var george = await h.Login("george");
        var id = await CereCod(george, "parola", ParolaNoua);
        for (var i = 0; i < 5; i++) await Finalizeaza(george, id, "bad");
        (await Finalizeaza(george, id, h.Mail.Code).Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Prea multe");
    }

    [Fact]
    public async Task SchimbareaParoleiIntreTimpAnuleazaCodulDeEmail()
    {
        using var george = await h.Login("george");
        var id = await CereCod(george, "email", "new@example.invalid");
        var cod = h.Mail.Code;
        // Meanwhile, the password is reset with a code (another device).
        await h.Anonim().Post("/autentificare/parola-uitata", new { identificator = "george", canal = "email" });
        await h.Anonim().Post("/autentificare/parola-noua", new { identificator = "george", cod = h.Emailuri.UltimulCod("george@example.invalid"), parola = ParolaNoua });
        using var nou = await h.Login("george", ParolaNoua);
        await Finalizeaza(nou, id, cod).Status(HttpStatusCode.BadRequest);
        (await h.Db(db => db.Utilizatori.Where(u => u.Username == "george").Select(u => u.Email).SingleAsync())).ShouldBe("george@example.invalid");
    }

    [Fact]
    public async Task EmailulNeconfiguratNuPretindeCaCodulAPlecat()
    {
        using var george = await h.Login("george");
        h.Mail.Fail = true;
        await george.Post(Url + "/confirmari", new { scop = "parola", parolaActuala = Harness.ParolaTest, valoareNoua = ParolaNoua })
            .Status(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task NumelePrinProfilulScurtSeSchimba()
    {
        using var george = await h.Login("george");
        (await george.Patch("/utilizatori/me", new { nume = "George Scurt" }).Json()).Str("nume").ShouldBe("George Scurt");
        await george.Patch("/utilizatori/me", new { nume = "  " }).Status(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProfesorulIsiInlocuiestePozaSiStergeFisierulVechi()
    {
        using var irina = await h.Login("irina");
        var vechi = await h.Db(db => db.ProfiluriMentor.Where(p => p.Utilizator.Username == "irina").Select(p => p.PozaUrl).SingleAsync());
        using var f = new MultipartFormDataContent();
        f.Add(Raspunsuri.Fisier(Raspunsuri.Png), "Poza", "poza.png");
        await irina.PostAsync(Url + "/documente", f, Ct).Status(HttpStatusCode.OK);
        var noua = await h.Db(db => db.ProfiluriMentor.Where(p => p.Utilizator.Username == "irina").Select(p => p.PozaUrl).SingleAsync());
        noua.ShouldNotBe(vechi);
        File.Exists(Path.Combine(h.DirStocare, noua!)).ShouldBeTrue();
    }

    [Fact]
    public async Task ElevulNuPoateIncarcaDocumente()
    {
        using var george = await h.Login("george");
        using var f = new MultipartFormDataContent();
        f.Add(Raspunsuri.Fisier(Raspunsuri.Png), "Poza", "poza.png");
        await george.PostAsync(Url + "/documente", f, Ct).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FaraFisiereDocumenteleSuntRefuzate()
    {
        using var irina = await h.Login("irina");
        using var f = new MultipartFormDataContent { { new StringContent("x"), "Altceva" } };
        await irina.PostAsync(Url + "/documente", f, Ct).Status(HttpStatusCode.BadRequest);
    }
}
