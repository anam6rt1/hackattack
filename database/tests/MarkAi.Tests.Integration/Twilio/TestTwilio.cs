using System.Net;
using System.Web;
using MarkAi.Tests.Suport;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Tests.Integration.Twilio;

/// <summary>
/// "Sună-mă" and the voice webhooks with fake Twilio credentials (process-wide, hence the "Twilio" collection that runs alone).
/// george has a verified phone (+40722111222); the fake AI tutor answers "Foarte bine, continuăm.". No real call is placed.
/// </summary>
[Collection("Twilio")]
public abstract class TestTwilio : TestCuHarness
{
    protected override bool CuTwilio => true;
    protected override IReadOnlyDictionary<string, string?> Setari => new Dictionary<string, string?>
    {
        ["Limite:ApeluriPeMinut"] = "100", ["Limite:AutentificariPeMinut"] = "100", ["Limite:SmsPeOra"] = "100", ["Limite:InregistrariPeOra"] = "100",
    };

    protected const string RaspunsTutor = """{"raspuns":"Foarte bine, continuăm.","final":false,"voce":{"voce":"ioana","viteza":100,"ton":0}}""";
    protected HttpClient george = null!;
    protected HttpClient Twilio => h.Brut();

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        h.Ai.Raspunde = (_, _, _) => RaspunsTutor;
        george = await h.Login("george");
        await h.VerificaTelefon(george, "0722111222");
    }

    /// <summary>A "Sună-mă" call: the session id and key from the URL sent to (fake) Twilio, and the CallSid it answered with.</summary>
    protected async Task<(int Sesiune, string Cheie, string Sid)> Suna(HttpClient? elev = null)
    {
        var inainte = h.Twilio.Apeluri.Count;
        await (elev ?? george).Post("/linia-ai/suna-ma").Status(HttpStatusCode.OK);
        h.Twilio.Apeluri.Count.ShouldBe(inainte + 1);
        var q = HttpUtility.ParseQueryString(new Uri(h.Twilio.Apeluri.Last()["Url"]).Query);
        var sesiune = int.Parse(q["s"]!);
        var sid = await h.Db(db => db.Sesiuni.Where(s => s.Id == sesiune).Select(s => s.TwilioCallSid).SingleAsync());
        return (sesiune, q["k"]!, sid!);
    }

    /// <summary>A webhook as the trial proxy sends it: form fields, no signature.</summary>
    protected Task<HttpResponseMessage> Webhook(string cale, string callSid, params (string, string)[] extra)
    {
        var form = new Dictionary<string, string> { ["CallSid"] = callSid, ["Direction"] = "outbound-api", ["To"] = "+40722111222" };
        foreach (var (k, v) in extra) form[k] = v;
        return Post(cale, form, semnat: false);
    }

    protected Task<HttpResponseMessage> Post(string cale, Dictionary<string, string> form, bool semnat)
    {
        var cerere = new HttpRequestMessage(HttpMethod.Post, cale) { Content = new FormUrlEncodedContent(form) };
        if (semnat) cerere.Headers.Add("X-Twilio-Signature", FakeTwilio.Semnatura(cale, form));
        return Twilio.SendAsync(cerere, Ct);
    }

    protected Task Inchide(string sid, int secunde = 30) =>
        Post("/twilio/voce/stare", new() { ["CallSid"] = sid, ["CallStatus"] = "completed", ["CallDuration"] = secunde.ToString() }, semnat: true);

    /// <summary>Pause between two "Sună-mă" (a double click must not ring twice).</summary>
    protected void DupaPauza() => h.Ceas.Advance(TimeSpan.FromSeconds(31));
}
