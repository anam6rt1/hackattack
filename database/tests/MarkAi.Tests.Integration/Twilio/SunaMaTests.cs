using System.Net;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Twilio;

/// <summary>"Sună-mă": a session and a key per call before Twilio dials, trial-safe parameters, daily caps, Twilio's errors explained.</summary>
public sealed class SunaMaTests : TestTwilio
{
    [Fact]
    public async Task SesiuneaPastreazaCallSidulDeLaTwilio()
    {
        var (_, _, sid) = await Suna();
        sid.ShouldStartWith("CA");
    }

    [Fact]
    public async Task DoarHashulCheiiEsteSalvat()
    {
        var (sesiune, cheie, _) = await Suna();
        var s = await h.Db(db => db.Sesiuni.SingleAsync(x => x.Id == sesiune));
        s.Status.ShouldBe("initiat");
        s.CheieHash!.Length.ShouldBe(64);
        s.CheieHash.ShouldNotContain(cheie);
    }

    [Fact]
    public async Task PeTrialSeTrimitDoarParametriiPermisi()
    {
        await Suna();
        var parametri = h.Twilio.Apeluri.Last();
        parametri.Keys.Order().ShouldBe(["StatusCallback", "To", "Url"]);
        parametri["To"].ShouldBe("+40722111222");
        parametri["Url"].ShouldStartWith(FakeTwilio.UrlPublic + "/twilio/voce?s=");
        parametri["StatusCallback"].ShouldBe(FakeTwilio.UrlPublic + "/twilio/voce/stare");
    }

    [Fact]
    public async Task ContulPlatitTrimiteSiNumarulDeLaCareSuna()
    {
        await using var platit = await Harness.Porneste(cuTwilio: true, setari: new Dictionary<string, string?> { ["Twilio:Trial"] = "false" });
        platit.Ai.Raspunde = (_, _, _) => RaspunsTutor;
        using var g = await platit.Login("george");
        await platit.VerificaTelefon(g, "0722111222");
        await g.Post("/linia-ai/suna-ma").Status(HttpStatusCode.OK);
        platit.Twilio.Apeluri.Last()["From"].ShouldBe("+15005550006");
    }

    [Fact]
    public async Task FaraTelefonVerificatNuSeSuna()
    {
        using var maria = await h.InregistreazaElev("maria");
        (await maria.Post("/linia-ai/suna-ma").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("Verifică mai întâi");
        h.Twilio.Apeluri.ShouldBeEmpty();
    }

    [Fact]
    public async Task DubluClickNuSunaDeDouaOri()
    {
        await Suna();
        (await george.Post("/linia-ai/suna-ma").Mesaj(HttpStatusCode.Conflict)).ShouldContain("deja");
        h.Twilio.Apeluri.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UnApelInCursOpresteUnulNou()
    {
        await Suna();
        DupaPauza();
        (await george.Post("/linia-ai/suna-ma").Mesaj(HttpStatusCode.Conflict)).ShouldContain("apel în curs");
    }

    [Fact]
    public async Task CelMultTreiApeluriPeZi()
    {
        for (var i = 0; i < 3; i++)
        {
            var (_, _, sid) = await Suna();
            await Inchide(sid);
            DupaPauza();
        }
        (await george.Post("/linia-ai/suna-ma").Mesaj(HttpStatusCode.Conflict)).ShouldContain("cele 3 apeluri");
    }

    [Fact]
    public async Task RefuzulTwilioEsteExplicatSiApelulAscuns()
    {
        h.Twilio.Eroare = (400, 21219, "The number is unverified.");
        (await george.Post("/linia-ai/suna-ma").Mesaj(HttpStatusCode.BadRequest)).ShouldContain("nu este verificat");
        h.Twilio.Eroare = null;
        (await george.GetJson("/sesiuni?canal=telefon")).Lista().ShouldBeEmpty();
        (await h.Db(db => db.Sesiuni.SingleAsync())).Status.ShouldBe("esuat");
    }

    [Fact]
    public async Task ApelulEsuatNuConteazaLaLimita()
    {
        h.Twilio.Eroare = (400, 21219, "The number is unverified.");
        for (var i = 0; i < 3; i++) { await george.Post("/linia-ai/suna-ma"); DupaPauza(); }
        h.Twilio.Eroare = null;
        await Suna();
    }

    [Fact]
    public async Task MinutelePeZiOprescApelurileTuturor()
    {
        var (sesiune, _, sid) = await Suna();
        await Inchide(sid, 20 * 60);
        using var maria = await h.InregistreazaElev("maria", telefon: "0733111222");
        (await maria.Post("/linia-ai/suna-ma").Mesaj(HttpStatusCode.Conflict)).ShouldContain("limita de minute");
    }

    [Fact]
    public async Task ApelurileInCursConteazaCuTimpulScurs()
    {
        var irina = await h.Db(db => db.Utilizatori.Where(u => u.Username == "irina").Select(u => u.Id).SingleAsync());
        await h.Db(async db =>
        {
            db.Add(new MarkAi.Data.Entities.Sesiune { UtilizatorId = irina, Canal = "telefon", Status = "in_curs", InceputLa = DateTime.UtcNow.AddMinutes(-21) });
            await db.SaveChangesAsync();
        });
        (await george.Post("/linia-ai/suna-ma").Mesaj(HttpStatusCode.Conflict)).ShouldContain("limita de minute");
    }

    [Fact]
    public async Task PaginaLiniaAiSpuneCaSePoateSuna()
    {
        var l = await george.GetJson("/linia-ai");
        l.Bool("poateSuna").ShouldBeTrue();
        l.Bool("telefonVerificat").ShouldBeTrue();
        l.Str("telefonulMeu").ShouldBe("+40722111222");
    }

    [Fact]
    public async Task FaraAiPaginaSpuneCaNuSePoateSuna()
    {
        await using var faraAi = await Harness.Porneste(cuTwilio: true, faraAi: true);
        using var g = await faraAi.Login("george");
        (await g.GetJson("/linia-ai")).Bool("poateSuna").ShouldBeFalse();
        (await g.Post("/linia-ai/suna-ma").Mesaj(HttpStatusCode.Conflict)).ShouldContain("Tutorele nu este");
    }

    [Fact]
    public async Task DoarEleviiCerApel()
    {
        using var irina = await h.Login("irina");
        await irina.Post("/linia-ai/suna-ma").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VoceaTutoruluiSeSalveaza()
    {
        var v = await george.Put("/utilizatori/me/voce-tutor", new { voce = "ioana", viteza = 90, ton = 5 }).Json();
        (v.Str("voce"), v.Int("viteza"), v.Int("ton")).ShouldBe(("ioana", 90, 5));
        await george.Put("/utilizatori/me/voce-tutor", new { voce = "robot", viteza = 100, ton = 0 }).Status(HttpStatusCode.BadRequest);
    }
}
