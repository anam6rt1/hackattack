using System.Net;
using System.Web;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Twilio;

/// <summary>Every webhook belongs to one session and needs its key (or Twilio's signature) plus the session's own CallSid; keys are never logged.</summary>
public sealed class WebhookuriTests : TestTwilio
{
    [Fact]
    public async Task CheiaGresitaEsteRefuzata()
    {
        var (s, _, sid) = await Suna();
        await Webhook($"/twilio/voce?s={s}&k=gresita", sid).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CallSidulAltuiApelEsteRefuzat()
    {
        var (s, k, _) = await Suna();
        await Webhook($"/twilio/voce?s={s}&k={k}", "CAaltapel").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CheiaFaraSesiuneEsteRefuzata()
    {
        var (_, k, sid) = await Suna();
        await Webhook($"/twilio/voce?k={k}", sid).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FaraCallSidEsteRefuzat()
    {
        var (s, k, _) = await Suna();
        await Webhook($"/twilio/voce?s={s}&k={k}", "").Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WebhookulValidRaspundeCuTwimlLegatDeSesiune()
    {
        var (s, k, sid) = await Suna();
        var r = await Webhook($"/twilio/voce?s={s}&k={k}", sid).Status(HttpStatusCode.OK);
        r.Content.Headers.ContentType!.MediaType.ShouldBe("application/xml");
        var twiml = await r.Content.ReadAsStringAsync(Ct);
        twiml.ShouldContain("<Gather");
        twiml.ShouldContain($"s={s}&amp;k=");
        twiml.ShouldContain("language=\"ro-RO\"");
    }

    [Fact]
    public async Task PrimulWebhookPuneSesiuneaInCurs()
    {
        var (s, k, sid) = await Suna();
        await Webhook($"/twilio/voce?s={s}&k={k}", sid);
        (await h.Db(db => db.Sesiuni.Where(x => x.Id == s).Select(x => x.Status).SingleAsync())).ShouldBe("in_curs");
    }

    [Fact]
    public async Task WebhookulMergeSiPrinGet()
    {
        var (s, k, sid) = await Suna();
        await Twilio.Get($"/twilio/voce?s={s}&k={Uri.EscapeDataString(k)}&CallSid={sid}").Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReplicaElevuluiPrimesteRaspunsulTutorului()
    {
        var (s, k, sid) = await Suna();
        var r = await Webhook($"/twilio/voce/raspuns?s={s}&k={k}", sid, ("SpeechResult", "Despre Luceafărul")).Status(HttpStatusCode.OK);
        (await r.Content.ReadAsStringAsync(Ct)).ShouldContain("Foarte bine");
        var mesaje = await h.Db(db => db.Mesaje.Where(m => m.SesiuneId == s).OrderBy(m => m.Id).Select(m => m.Expeditor).ToListAsync());
        mesaje.ShouldBe(["elev", "tutor"]);
    }

    [Fact]
    public async Task FaraVorbeTutorulCereSaRepete()
    {
        var (s, k, sid) = await Suna();
        var r = await Webhook($"/twilio/voce/raspuns?s={s}&k={k}", sid, ("SpeechResult", "  "));
        (await r.Content.ReadAsStringAsync(Ct)).ShouldContain("Poți să repeți?");
        h.Ai.Apeluri.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheiaNuApareNiciodataInJurnal()
    {
        var (s, k, sid) = await Suna();
        await Webhook($"/twilio/voce?s={s}&k={k}", sid);
        await Webhook($"/twilio/voce/raspuns?s={s}&k={k}", sid, ("SpeechResult", "Salut"));
        h.Jurnal.Linii.ShouldNotContain(l => l.Contains(k));
    }

    [Fact]
    public async Task WebhookurileSuntInJurnalDupaSesiune()
    {
        var (s, k, sid) = await Suna();
        await Webhook($"/twilio/voce?s={s}&k={k}", sid);
        h.Jurnal.Linii.ShouldContain(l => l.Contains("Webhook Twilio") && l.Contains($"sesiune {s}"));
    }

    [Fact]
    public async Task StareaNesemnataEsteRefuzata()
    {
        var (_, _, sid) = await Suna();
        await Post("/twilio/voce/stare", new() { ["CallSid"] = sid, ["CallStatus"] = "completed", ["CallDuration"] = "60" }, semnat: false)
            .Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StareaSemnataInchideApelulCuDurata()
    {
        var (s, _, sid) = await Suna();
        await Post("/twilio/voce/stare", new() { ["CallSid"] = sid, ["CallStatus"] = "completed", ["CallDuration"] = "60" }, semnat: true)
            .Status(HttpStatusCode.NoContent);
        var x = await h.Db(db => db.Sesiuni.SingleAsync(z => z.Id == s));
        x.FinalizatLa.ShouldNotBeNull();
        x.DurataSecunde.ShouldBe(60);
    }

    [Theory]
    [InlineData("busy")]
    [InlineData("no-answer")]
    [InlineData("failed")]
    [InlineData("canceled")]
    public async Task OricareFinalDeApelInchideSesiunea(string stare)
    {
        var (s, _, sid) = await Suna();
        await Post("/twilio/voce/stare", new() { ["CallSid"] = sid, ["CallStatus"] = stare }, semnat: true).Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.Sesiuni.Where(z => z.Id == s).Select(z => z.FinalizatLa).SingleAsync())).ShouldNotBeNull();
    }

    [Fact]
    public async Task StareaIntermediaraNuInchide()
    {
        var (s, _, sid) = await Suna();
        await Post("/twilio/voce/stare", new() { ["CallSid"] = sid, ["CallStatus"] = "ringing" }, semnat: true).Status(HttpStatusCode.NoContent);
        (await h.Db(db => db.Sesiuni.Where(z => z.Id == s).Select(z => z.FinalizatLa).SingleAsync())).ShouldBeNull();
    }

    [Fact]
    public async Task CheiaUnuiApelTerminatNuMaiMerge()
    {
        var (s, k, sid) = await Suna();
        await Inchide(sid, 60);
        await Webhook($"/twilio/voce/raspuns?s={s}&k={k}", sid, ("SpeechResult", "Mai sunt?")).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CheiaExpirataEsteRefuzata()
    {
        var (s, k, sid) = await Suna();
        await h.Db(db => db.Sesiuni.Where(x => x.Id == s).ExecuteUpdateAsync(x => x.SetProperty(y => y.CheieExpiraLa, DateTime.UtcNow.AddMinutes(-1))));
        await Webhook($"/twilio/voce?s={s}&k={k}", sid).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SemnaturaTwilioInlocuiesteCheia()
    {
        var (s, _, sid) = await Suna();
        var cale = $"/twilio/voce?s={s}";
        await Post(cale, new() { ["CallSid"] = sid, ["Direction"] = "outbound-api" }, semnat: true).Status(HttpStatusCode.OK);
    }

    private async Task<(int Sesiune, string Cheie, string Twiml)> ApelDeIntrare(string sid = "CAintrare1", string de = "+40722111222")
    {
        var r = await Post("/twilio/voce", new() { ["CallSid"] = sid, ["From"] = de, ["Direction"] = "inbound" }, semnat: true).Status(HttpStatusCode.OK);
        var twiml = await r.Content.ReadAsStringAsync(Ct);
        var sesiune = await h.Db(db => db.Sesiuni.Where(s => s.TwilioCallSid == sid).Select(s => s.Id).SingleOrDefaultAsync());
        var cheie = twiml.Contains("action=\"") ? HttpUtility.ParseQueryString(HttpUtility.HtmlDecode(twiml.Split("action=\"")[1].Split('"')[0]).Split('?')[1])["k"]! : "";
        return (sesiune, cheie, twiml);
    }

    [Fact]
    public async Task ApelulDeIntrareNesemnatEsteRefuzat()
    {
        await Post("/twilio/voce", new() { ["CallSid"] = "CAintrare1", ["From"] = "+40722111222", ["Direction"] = "inbound" }, semnat: false)
            .Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApelulDeIntrareSemnatPrimesteSesiuneSiCheie()
    {
        var (s, k, twiml) = await ApelDeIntrare();
        twiml.ShouldContain("<Gather");
        s.ShouldBeGreaterThan(0);
        k.Length.ShouldBeGreaterThan(20);
        await Webhook($"/twilio/voce/raspuns?s={s}&k={k}", "CAintrare1", ("SpeechResult", "Salut")).Status(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CheiaUnuiApelNuMergePentruAltul()
    {
        var (_, k1, sid1) = await Suna();
        await Inchide(sid1);
        var (s2, _, _) = await ApelDeIntrare();
        await Webhook($"/twilio/voce/raspuns?s={s2}&k={k1}", "CAintrare1", ("SpeechResult", "Salut")).Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NumarulNecunoscutAudeCumSeLeaga()
    {
        var (s, _, twiml) = await ApelDeIntrare(de: "+40799999999");
        twiml.ShouldContain("nu este asociat unui cont");
        twiml.ShouldContain("<Hangup");
        s.ShouldBe(0);
    }

    [Fact]
    public async Task ReluareaUnuiApelDeIntrareEsteRefuzata()
    {
        await ApelDeIntrare();
        await Post("/twilio/voce", new() { ["CallSid"] = "CAintrare1", ["From"] = "+40722111222", ["Direction"] = "inbound" }, semnat: true)
            .Status(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApelulDeIntrarePesteLimitaEsteInchisPoliticos()
    {
        for (var i = 0; i < 3; i++) { var (_, _, sid) = await Suna(); await Inchide(sid); DupaPauza(); }
        var (_, _, twiml) = await ApelDeIntrare();
        twiml.ShouldContain("cele 3 apeluri");
        twiml.ShouldContain("<Hangup");
    }

    [Fact]
    public async Task UltimulPasIncheieApelul()
    {
        await using var scurt = await MarkAi.Tests.Suport.Harness.Porneste(cuTwilio: true, setari: new Dictionary<string, string?> { ["Twilio:LimitaPasi"] = "2" });
        scurt.Ai.Raspunde = (_, _, _) => RaspunsTutor;
        using var g = await scurt.Login("george");
        await scurt.VerificaTelefon(g, "0722111222");
        await g.Post("/linia-ai/suna-ma").Status(HttpStatusCode.OK);
        var q = HttpUtility.ParseQueryString(new Uri(scurt.Twilio.Apeluri.Last()["Url"]).Query);
        var (s, k) = (q["s"]!, q["k"]!);
        var sid = await scurt.Db(db => db.Sesiuni.Select(x => x.TwilioCallSid).SingleAsync());
        var form = new Dictionary<string, string> { ["CallSid"] = sid!, ["SpeechResult"] = "Salut" };
        using var tw = scurt.Brut();
        await tw.PostAsync($"/twilio/voce?s={s}&k={Uri.EscapeDataString(k)}", new FormUrlEncodedContent(form), Ct);
        // Step 2 of 2: the tutor's answer must end the call.
        scurt.Ai.Raspunde = (_, _, _) => """{"raspuns":"Ne oprim aici.","final":true,"voce":{"voce":"ioana","viteza":100,"ton":0}}""";
        var r = await tw.PostAsync($"/twilio/voce/raspuns?s={s}&k={Uri.EscapeDataString(k)}", new FormUrlEncodedContent(form), Ct);
        var twiml = await r.Content.ReadAsStringAsync(Ct);
        twiml.ShouldContain("<Hangup");
        twiml.ShouldNotContain("<Gather");
    }

    [Fact]
    public async Task TutorulCareCadeCereScuze()
    {
        var (s, k, sid) = await Suna();
        h.Ai.Raspunde = (_, _, _) => throw new HttpRequestException("model down");
        var r = await Webhook($"/twilio/voce/raspuns?s={s}&k={k}", sid, ("SpeechResult", "Salut")).Status(HttpStatusCode.OK);
        (await r.Content.ReadAsStringAsync(Ct)).ShouldContain("problemă de conexiune");
        h.Jurnal.Linii.ShouldContain(l => l.Contains("Phone tutor failed"));
    }

    [Fact]
    public async Task ElevulIsiVedeApelulSiMesajele()
    {
        var (s, k, sid) = await Suna();
        await Webhook($"/twilio/voce?s={s}&k={k}", sid);
        await Webhook($"/twilio/voce/raspuns?s={s}&k={k}", sid, ("SpeechResult", "Despre Luceafărul"));
        var apel = (await george.GetJson("/sesiuni?canal=telefon")).Lista().Single();
        apel.Int("numarMesaje").ShouldBe(3);
        (await george.GetJson($"/sesiuni/{s}/mesaje")).Lista().Select(m => m.Str("expeditor")).ShouldBe(["tutor", "elev", "tutor"]);
    }

    [Fact]
    public async Task MesajeleApeluluiSuntPrivate()
    {
        var (s, _, _) = await Suna();
        using var admin = await h.Login("admin");
        await admin.Get($"/sesiuni/{s}/mesaje").Status(HttpStatusCode.NotFound);
    }
}
