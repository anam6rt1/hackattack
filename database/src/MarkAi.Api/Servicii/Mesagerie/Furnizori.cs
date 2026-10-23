using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;

namespace MarkAi.Api.Servicii.Mesagerie;

public sealed record Email(string Catre, string Subiect, string Html, string Text);
public sealed record Sms(string Catre, string Text);

/// <summary>The provider could not deliver the message (not configured, refused, unreachable).</summary>
public sealed class TrimitereEsuata(string mesaj, Exception? cauza = null) : Exception(mesaj, cauza);

/// <summary>
/// Sends one email. Providers are interchangeable: chosen by <c>Email:Furnizor</c> in configuration ("smtp" or "jurnal"),
/// so moving to another service (e.g. an HTTP email API) means adding one class here and changing one setting.
/// </summary>
public interface IExpeditorEmail
{
    string Nume { get; }
    Task TrimiteAsync(Email mesaj, CancellationToken ct);
}

/// <summary>Sends one SMS; chosen by <c>Sms:Furnizor</c> ("twilio" or "jurnal"), replaceable the same way.</summary>
public interface IExpeditorSms
{
    string Nume { get; }
    Task TrimiteAsync(Sms mesaj, CancellationToken ct);
}

/// <summary>SMTP relay (Brevo: smtp-relay.brevo.com:587 with the account's SMTP key), configured by <c>SetariEmail:*</c>.</summary>
public sealed class ExpeditorEmailSmtp(IConfiguration cfg) : IExpeditorEmail
{
    public string Nume => "smtp";

    public async Task TrimiteAsync(Email mesaj, CancellationToken ct)
    {
        var host = cfg["SetariEmail:Host"];
        var expeditor = cfg["SetariEmail:Expeditor"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(expeditor)) throw new TrimitereEsuata("Emailul nu este configurat pe server.");
        using var client = new SmtpClient(host, cfg.GetValue("SetariEmail:Port", 587))
        {
            EnableSsl = cfg.GetValue("SetariEmail:Tls", true),
            Credentials = new NetworkCredential(cfg["SetariEmail:Utilizator"], cfg["SetariEmail:Parola"]),
            Timeout = 15000,
        };
        using var m = new MailMessage(MailAddress.TryCreate(expeditor, out var de) ? de : new MailAddress(expeditor), new MailAddress(mesaj.Catre))
        {
            Subject = mesaj.Subiect, Body = mesaj.Html, IsBodyHtml = true, SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8,
        };
        m.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(mesaj.Text, Encoding.UTF8, "text/plain"));
        try { await client.SendMailAsync(m, ct); }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException) { throw new TrimitereEsuata("Emailul nu a putut fi trimis.", ex); }
    }
}

/// <summary>SMS through Twilio's Messages API, from <c>Twilio:Numar</c>. A trial account only reaches verified numbers.</summary>
public sealed class ExpeditorSmsTwilio(IConfiguration cfg, IHttpClientFactory http) : IExpeditorSms
{
    public string Nume => "twilio";

    public async Task TrimiteAsync(Sms mesaj, CancellationToken ct)
    {
        var sid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
        var token = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
        var de = cfg["Sms:Numar"] ?? cfg["Twilio:Numar"];
        if (string.IsNullOrEmpty(sid) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(de)) throw new TrimitereEsuata("SMS-urile nu sunt configurate pe server.");
        using var cerere = new HttpRequestMessage(HttpMethod.Post,
            $"{(cfg["Twilio:ApiUrl"] ?? "https://api.twilio.com").TrimEnd('/')}/2010-04-01/Accounts/{sid}/Messages.json")
        { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["To"] = mesaj.Catre, ["From"] = de, ["Body"] = mesaj.Text }) };
        cerere.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sid}:{token}")));
        try
        {
            using var r = await http.CreateClient("twilio").SendAsync(cerere, ct);
            // 21608: a trial account may only text verified numbers.
            if (!r.IsSuccessStatusCode)
                throw new TrimitereEsuata((await r.Content.ReadAsStringAsync(ct)).Contains("21608")
                    ? "Numărul nu este verificat în contul Twilio de probă, deci nu poate primi SMS."
                    : $"SMS-ul nu a putut fi trimis ({(int)r.StatusCode}).");
        }
        catch (HttpRequestException ex) { throw new TrimitereEsuata("SMS-ul nu a putut fi trimis.", ex); }
    }
}

/// <summary>
/// Development provider: nothing leaves the server. The message (with its code) is kept and shown to admins on the
/// "Mesaje trimise" page, so sign-up and phone verification can be tested before a real provider is set up.
/// </summary>
public sealed class ExpeditorJurnal(ILogger<ExpeditorJurnal> log) : IExpeditorEmail, IExpeditorSms
{
    public string Nume => "jurnal";

    public Task TrimiteAsync(Email mesaj, CancellationToken ct)
    {
        log.LogInformation("Email not sent (jurnal provider): {Subiect}", mesaj.Subiect);
        return Task.CompletedTask;
    }

    public Task TrimiteAsync(Sms mesaj, CancellationToken ct)
    {
        log.LogInformation("SMS not sent (jurnal provider)");
        return Task.CompletedTask;
    }
}
