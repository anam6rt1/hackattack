using System.Collections.Concurrent;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Api.Setari;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MarkAi.Tests.Suport;

public sealed class FakeMail : IEmailConfirmare
{
    public string Code = "", Email = ""; public bool Fail;
    public Task Trimite(string email, string cod, string actiune, CancellationToken ct)
    { if (Fail) throw new EmailNeconfiguratException(); Email = email; Code = cod; return Task.CompletedTask; }
}

/// <summary>The AI model: answers with <see cref="Raspunde"/> (default: an empty JSON object) and records every call.</summary>
public sealed class FakeAi : IModelAi
{
    public string Nume => "Fake";
    public Func<ScopAi, string, IReadOnlyList<MesajAi>, string> Raspunde { get; set; } = (_, _, _) => "{}";
    public ConcurrentQueue<(ScopAi Scop, string Sistem, IReadOnlyList<MesajAi> Mesaje)> Apeluri { get; } = new();

    public Task<string> JsonAsync(ScopAi scop, string sistem, IReadOnlyList<MesajAi> mesaje, Dictionary<string, JsonElement> schema,
        int maxTokens, CancellationToken ct)
    {
        Apeluri.Enqueue((scop, sistem, mesaje));
        return Task.FromResult(Raspunde(scop, sistem, mesaje));
    }
}

/// <summary>
/// Twilio's REST API for outbound calls, served by the test app itself under <see cref="Cale"/>.
/// Records each call's form and answers like Twilio (201 with a CallSid), or with <see cref="Eroare"/> when set.
/// </summary>
public sealed class FakeTwilio
{
    public const string Cale = "/__fals/twilio";
    public const string Sid = "ACfa4e000000000000000000000000000";
    public const string Token = "fake-twilio-auth-token";
    public const string UrlPublic = "https://markai.test/api";

    /// <summary>X-Twilio-Signature for a POST to <paramref name="cale"/> (path + query) with this form, as Twilio computes it.</summary>
    public static string Semnatura(string cale, IReadOnlyDictionary<string, string> form)
    {
        var sb = new System.Text.StringBuilder(UrlPublic + cale);
        foreach (var kv in form.OrderBy(k => k.Key, StringComparer.Ordinal)) sb.Append(kv.Key).Append(kv.Value);
        return Convert.ToBase64String(System.Security.Cryptography.HMACSHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(Token), System.Text.Encoding.UTF8.GetBytes(sb.ToString())));
    }

    public ConcurrentQueue<Dictionary<string, string>> Apeluri { get; } = new();
    public (int Status, int Cod, string Mesaj)? Eroare { get; set; }

    public void Mapeaza(WebApplication app) =>
        app.MapPost(Cale + "/2010-04-01/Accounts/{sid}/Calls.json", async (string sid, HttpRequest req) =>
        {
            var form = await req.ReadFormAsync();
            Apeluri.Enqueue(form.ToDictionary(x => x.Key, x => x.Value.ToString()));
            if (Eroare is var (status, cod, mesaj)) return Results.Json(new { code = cod, message = mesaj, status }, statusCode: status);
            return Results.Json(new { sid = "CA" + Guid.NewGuid().ToString("N"), status = "queued" }, statusCode: 201);
        }).AllowAnonymous();
}

/// <summary>Keeps every formatted log line in memory.</summary>
public sealed class JurnalCapturat : ILoggerProvider
{
    public ConcurrentQueue<string> Linii { get; } = new();
    public ILogger CreateLogger(string categorie) => new Logger(this);
    public void Dispose() { }

    private sealed class Logger(JurnalCapturat jurnal) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel nivel) => true;
        public void Log<TState>(LogLevel nivel, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> format) =>
            jurnal.Linii.Enqueue(format(state, ex) + (ex is null ? "" : " " + ex));
    }
}

/// <summary>The mail provider: keeps every email; <see cref="Esueaza"/> makes it fail like an unreachable server.</summary>
public sealed class FakeEmail : IExpeditorEmail
{
    public string Nume => "fals";
    public bool Esueaza { get; set; }
    public ConcurrentQueue<Email> Trimise { get; } = new();

    public Task TrimiteAsync(Email mesaj, CancellationToken ct)
    {
        if (Esueaza) throw new TrimitereEsuata("Serverul de email nu răspunde.");
        Trimise.Enqueue(mesaj);
        return Task.CompletedTask;
    }

    public string UltimulCod(string catre) => System.Text.RegularExpressions.Regex.Match(
        Trimise.Last(m => m.Catre.Equals(catre, StringComparison.OrdinalIgnoreCase)).Text, @"\b\d{6}\b").Value;
}

public sealed class FakeSms : IExpeditorSms
{
    public string Nume => "fals";
    public ConcurrentQueue<Sms> Trimise { get; } = new();

    public Task TrimiteAsync(Sms mesaj, CancellationToken ct) { Trimise.Enqueue(mesaj); return Task.CompletedTask; }

    public string UltimulCod() => System.Text.RegularExpressions.Regex.Match(Trimise.Last().Text, @"\b\d{6}\b").Value;
}
