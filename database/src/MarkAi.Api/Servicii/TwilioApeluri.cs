using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MarkAi.Api.Infrastructura;

namespace MarkAi.Api.Servicii;

/// <summary>Twilio limits for the phone tutor. A trial account is far stricter than a paid one.</summary>
public sealed record LimiteApel(int Pasi, TimeSpan Durata, TimeSpan RaspunsMaxim)
{
    public static LimiteApel Din(IConfiguration cfg) => cfg.GetValue("Twilio:Trial", true)
        // Trial: TwiML fetches time out after 5 s, a call may follow at most 10 TwiML documents, and lasts at most 10 minutes.
        ? new(cfg.GetValue("Twilio:LimitaPasi", 10), TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(4))
        : new(cfg.GetValue("Twilio:LimitaPasi", 0), TimeSpan.FromHours(4), TimeSpan.FromSeconds(12));
}

/// <summary>
/// Outbound "Sună-mă" calls: Twilio calls the student's phone and, once answered, fetches the same
/// voice webhook as an incoming call. This works on a Twilio trial (calls to verified numbers only).
/// </summary>
public sealed class TwilioApeluri(IConfiguration cfg, IHttpClientFactory http, ILogger<TwilioApeluri> log)
{
    private static readonly ConcurrentDictionary<int, DateTime> UltimaCerere = new();
    private static readonly TimeSpan Pauza = TimeSpan.FromSeconds(30);

    private static string? Sid => Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
    private static string? Token => Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
    private string? NumarTwilio => cfg["Twilio:Numar"];
    private string UrlPublic => (cfg["Twilio:UrlPublic"] ?? "").TrimEnd('/');

    public bool Configurat => !string.IsNullOrEmpty(Sid) && !string.IsNullOrEmpty(Token)
        && !string.IsNullOrEmpty(NumarTwilio) && UrlPublic.StartsWith("https://");

    public async Task SunaAsync(int utilizatorId, string telefon, CancellationToken ct)
    {
        if (!Configurat) throw new InvalidOperationException("Apelurile nu sunt configurate pe server.");
        // A double click must not ring the phone twice.
        var acum = DateTime.UtcNow;
        if (UltimaCerere.TryGetValue(utilizatorId, out var ultima) && acum - ultima < Pauza)
            throw new InvalidOperationException("Te sunăm deja. Așteaptă câteva secunde.");
        UltimaCerere[utilizatorId] = acum;

        using var cerere = new HttpRequestMessage(HttpMethod.Post,
            $"{(cfg["Twilio:ApiUrl"] ?? "https://api.twilio.com").TrimEnd('/')}/2010-04-01/Accounts/{Sid}/Calls.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = telefon,
                ["From"] = NumarTwilio!,
                ["Url"] = UrlPublic + "/twilio/voce",
                ["Method"] = "POST",
                ["StatusCallback"] = UrlPublic + "/twilio/voce/stare",
                ["StatusCallbackMethod"] = "POST",
                ["StatusCallbackEvent"] = "completed",
            }),
        };
        cerere.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Sid}:{Token}")));
        using var r = await http.CreateClient("twilio").SendAsync(cerere, ct);
        if (r.IsSuccessStatusCode) return;

        UltimaCerere.TryRemove(utilizatorId, out _);
        var eroare = await r.Content.ReadFromJsonAsync<JsonNode>(ct);
        var cod = eroare?["code"]?.GetValue<int>();
        log.LogWarning("Twilio a refuzat apelul: {Status} {Cod} {Mesaj}", (int)r.StatusCode, cod, eroare?["message"]?.GetValue<string>());
        throw new CerereInvalida(cod switch
        {
            // Twilio error codes: unverified number on a trial account, and calls to a blocked country.
            // 21210: the "From" number is not a Twilio number (or verified caller ID) of this account.
            21210 => "Numărul Twilio al serverului nu aparține contului Twilio. Configurează un număr Twilio valid.",
            21219 or 21608 => "Numărul tău nu este verificat în contul Twilio de probă. Verifică-l în consola Twilio și încearcă din nou.",
            21215 or 13227 => "Apelurile către acest număr nu sunt permise din contul Twilio (verifică permisiunile geografice).",
            21211 => "Numărul de telefon salvat nu este valid.",
            _ => $"Twilio nu a putut iniția apelul ({cod?.ToString() ?? ((int)r.StatusCode).ToString()}).",
        });
    }
}
