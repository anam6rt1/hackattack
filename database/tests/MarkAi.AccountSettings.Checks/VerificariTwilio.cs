using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// "Sună-mă" and the voice webhooks, with fake Twilio credentials: every webhook belongs to one session and needs its key
/// (or Twilio's signature) plus the session's own CallSid; keys are never logged; daily caps hold. No real call is placed.
/// </summary>
static class VerificariTwilio
{
    public static readonly Dictionary<string, string?> Setari = new() { ["Limite:ApeluriPeMinut"] = "100" };

    public static async Task Ruleaza(Harness h)
    {
        h.Ai.Raspunde = (_, _, _) => """{"raspuns":"Foarte bine, continuăm.","final":false,"voce":{"voce":"ioana","viteza":100,"ton":0}}""";
        using var george = await h.Login("george");
        await george.PutAsJsonAsync("/utilizatori/me/setari/telefon", new { telefon = "0722111222" });

        // 1. "Sună-mă": the session exists before Twilio dials, and the URL names it.
        var (sesiune1, cheie1, _) = await Suna(h, george, "first call");
        var sid1 = await h.Db(db => db.Sesiuni.Where(s => s.Id == sesiune1).Select(s => s.TwilioCallSid).SingleAsync());
        h.Check(sid1 is not null && sid1.StartsWith("CA"), "session stores the CallSid returned by Twilio");
        var stare = await h.Db(db => db.Sesiuni.Where(s => s.Id == sesiune1).Select(s => new { s.Status, s.CheieHash }).SingleAsync());
        h.Check(stare.Status == "initiat" && stare.CheieHash is { Length: 64 } && stare.CheieHash != cheie1, "only a hash of the call key is stored");

        using var twilio = h.Anonim();
        h.Check((await Webhook(twilio, $"/twilio/voce?s={sesiune1}&k=gresita", sid1!)).StatusCode == HttpStatusCode.Forbidden, "wrong key rejected");
        h.Check((await Webhook(twilio, $"/twilio/voce?s={sesiune1}&k={cheie1}", "CAaltapel")).StatusCode == HttpStatusCode.Forbidden, "another call's CallSid rejected");
        h.Check((await Webhook(twilio, $"/twilio/voce?k={cheie1}", sid1!)).StatusCode == HttpStatusCode.Forbidden, "key without its session rejected");
        var voce = await Webhook(twilio, $"/twilio/voce?s={sesiune1}&k={cheie1}", sid1!);
        var twiml = await voce.Content.ReadAsStringAsync();
        h.Check(voce.IsSuccessStatusCode && twiml.Contains("<Gather") && twiml.Contains($"s={sesiune1}&amp;k="), "valid webhook answers with TwiML bound to the session");

        var raspuns = await Webhook(twilio, $"/twilio/voce/raspuns?s={sesiune1}&k={cheie1}", sid1!, ("SpeechResult", "Despre Luceafărul"));
        h.Check(raspuns.IsSuccessStatusCode && (await raspuns.Content.ReadAsStringAsync()).Contains("Foarte bine"), "tutor turn works with the session key");
        h.Check(!h.Jurnal.Linii.Any(l => l.Contains(cheie1)), "the call key never appears in the logs"
            + string.Concat(h.Jurnal.Linii.Where(l => l.Contains(cheie1)).Take(3).Select(l => "\n   ↳ " + l[..Math.Min(l.Length, 200)])));
        h.Check(h.Jurnal.Linii.Any(l => l.Contains("Webhook Twilio") && l.Contains($"sesiune {sesiune1}")), "webhooks are still logged, by session");

        // Status callback: only with Twilio's signature; it closes the session and records the length.
        var stareForm = new Dictionary<string, string> { ["CallSid"] = sid1!, ["CallStatus"] = "completed", ["CallDuration"] = "60" };
        h.Check((await Post(twilio, "/twilio/voce/stare", stareForm, semnat: false)).StatusCode == HttpStatusCode.Forbidden, "unsigned status callback rejected");
        h.Check((await Post(twilio, "/twilio/voce/stare", stareForm, semnat: true)).StatusCode == HttpStatusCode.NoContent, "signed status callback accepted");
        var inchisa = await h.Db(db => db.Sesiuni.Where(s => s.Id == sesiune1).Select(s => new { s.FinalizatLa, s.DurataSecunde }).SingleAsync());
        h.Check(inchisa.FinalizatLa is not null && inchisa.DurataSecunde == 60, "call closed with its length");
        h.Check((await Webhook(twilio, $"/twilio/voce/raspuns?s={sesiune1}&k={cheie1}", sid1!, ("SpeechResult", "Mai sunt?"))).StatusCode == HttpStatusCode.Forbidden,
            "a finished call's key no longer works");

        // 2. A call the student makes: needs Twilio's signature, then gets its own key.
        var intrare = new Dictionary<string, string> { ["CallSid"] = "CAintrare1", ["From"] = "+40722111222", ["Direction"] = "inbound" };
        h.Check((await Post(twilio, "/twilio/voce", intrare, semnat: false)).StatusCode == HttpStatusCode.Forbidden, "unsigned incoming call rejected");
        var semnat = await Post(twilio, "/twilio/voce", intrare, semnat: true);
        var twimlIntrare = await semnat.Content.ReadAsStringAsync();
        h.Check(semnat.IsSuccessStatusCode && twimlIntrare.Contains("<Gather"), "signed incoming call from a known student answered");
        var sesiune2 = await h.Db(db => db.Sesiuni.Where(s => s.TwilioCallSid == "CAintrare1").Select(s => s.Id).SingleAsync());
        var cheie2 = HttpUtility.ParseQueryString(HttpUtility.HtmlDecode(twimlIntrare.Split("action=\"")[1].Split('"')[0]).Split('?')[1])["k"]!;
        h.Check((await Webhook(twilio, $"/twilio/voce/raspuns?s={sesiune2}&k={cheie1}", "CAintrare1", ("SpeechResult", "Salut"))).StatusCode == HttpStatusCode.Forbidden,
            "one call's key does not work for another session");
        await Post(twilio, "/twilio/voce/stare", new() { ["CallSid"] = "CAintrare1", ["CallStatus"] = "completed", ["CallDuration"] = "30" }, semnat: true);

        // Expired key.
        h.Ceas.Advance(TimeSpan.FromSeconds(31));
        var (sesiune3, cheie3, sid3) = await Suna(h, george, "third call");
        await h.Db(db => db.Sesiuni.Where(s => s.Id == sesiune3).ExecuteUpdateAsync(s => s.SetProperty(x => x.CheieExpiraLa, DateTime.UtcNow.AddMinutes(-1))));
        h.Check((await Webhook(twilio, $"/twilio/voce?s={sesiune3}&k={cheie3}", sid3)).StatusCode == HttpStatusCode.Forbidden, "expired key rejected");
        await Post(twilio, "/twilio/voce/stare", new() { ["CallSid"] = sid3, ["CallStatus"] = "completed", ["CallDuration"] = "30" }, semnat: true);

        // 3. Daily cap per student: 3 calls in 24 hours (two "Sună-mă" + one incoming so far).
        h.Ceas.Advance(TimeSpan.FromSeconds(31));
        var refuz = await george.PostAsync("/linia-ai/suna-ma", null);
        h.Check(refuz.StatusCode == HttpStatusCode.Conflict && (await refuz.Content.ReadAsStringAsync()).Contains("cele 3 apeluri"), "fourth call in 24 hours refused");

        // 4. A call Twilio refuses is marked failed and hidden from the call list.
        using var maria = h.Anonim();
        var inregistrare = await maria.PostAsJsonAsync("/autentificare/inregistrare", new { nume = "Maria Pop", username = "maria", email = "maria@example.invalid", parola = Harness.ParolaTest, telefon = "0733111222" });
        h.Check(inregistrare.IsSuccessStatusCode, "second student registered");
        h.Twilio.Eroare = (400, 21219, "The number is unverified.");
        var esuat = await maria.PostAsync("/linia-ai/suna-ma", null);
        h.Check(esuat.StatusCode == HttpStatusCode.BadRequest && (await esuat.Content.ReadAsStringAsync()).Contains("nu este verificat"), "Twilio's refusal explained to the student");
        h.Twilio.Eroare = null;
        var apeluri = await maria.GetFromJsonAsync<JsonElement>("/sesiuni?canal=telefon");
        h.Check(apeluri.GetProperty("elemente").GetArrayLength() == 0, "failed call hidden from the call list");

        // 5. Daily minutes for everyone: once 20 minutes are used, nobody else is called.
        await h.Db(db => db.Sesiuni.Where(s => s.Id == sesiune1).ExecuteUpdateAsync(s => s.SetProperty(x => x.DurataSecunde, 20 * 60)));
        h.Ceas.Advance(TimeSpan.FromSeconds(31));
        var minute = await maria.PostAsync("/linia-ai/suna-ma", null);
        h.Check(minute.StatusCode == HttpStatusCode.Conflict && (await minute.Content.ReadAsStringAsync()).Contains("limita de minute"), "daily minute cap stops new calls");
    }

    static async Task<(int Sesiune, string Cheie, string Sid)> Suna(Harness h, HttpClient elev, string eticheta)
    {
        var inainte = h.Twilio.Apeluri.Count;
        var r = await elev.PostAsync("/linia-ai/suna-ma", null);
        h.Check(r.IsSuccessStatusCode && h.Twilio.Apeluri.Count == inainte + 1, $"{eticheta}: request sent to the fake Twilio API");
        var url = new Uri(h.Twilio.Apeluri.Last()["Url"]);
        var q = HttpUtility.ParseQueryString(url.Query);
        var sesiune = int.Parse(q["s"]!);
        var sid = await h.Db(db => db.Sesiuni.Where(s => s.Id == sesiune).Select(s => s.TwilioCallSid).SingleAsync());
        return (sesiune, q["k"]!, sid!);
    }

    // A webhook as the trial proxy sends it: form fields, no signature.
    static Task<HttpResponseMessage> Webhook(HttpClient c, string cale, string callSid, params (string, string)[] extra)
    {
        var form = new Dictionary<string, string> { ["CallSid"] = callSid, ["Direction"] = "outbound-api", ["To"] = "+40722111222" };
        foreach (var (k, v) in extra) form[k] = v;
        return Post(c, cale, form, semnat: false);
    }

    static Task<HttpResponseMessage> Post(HttpClient c, string cale, Dictionary<string, string> form, bool semnat)
    {
        var cerere = new HttpRequestMessage(HttpMethod.Post, cale) { Content = new FormUrlEncodedContent(form) };
        if (semnat) cerere.Headers.Add("X-Twilio-Signature", FakeTwilio.Semnatura(cale, form));
        return c.SendAsync(cerere);
    }
}
