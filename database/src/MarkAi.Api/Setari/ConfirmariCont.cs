using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using MarkAi.Api.Infrastructura;

namespace MarkAi.Api.Setari;

public interface IEmailConfirmare
{
    Task Trimite(string email, string cod, string actiune, CancellationToken ct);
}

public sealed class EmailConfirmareSmtp(IConfiguration cfg) : IEmailConfirmare
{
    public async Task Trimite(string email, string cod, string actiune, CancellationToken ct)
    {
        var host = cfg["SetariEmail:Host"];
        var from = cfg["SetariEmail:Expeditor"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new EmailNeconfiguratException();
        using var client = new SmtpClient(host, cfg.GetValue("SetariEmail:Port", 587))
        {
            EnableSsl = cfg.GetValue("SetariEmail:Tls", true),
            Credentials = new NetworkCredential(cfg["SetariEmail:Utilizator"], cfg["SetariEmail:Parola"]),
            Timeout = 15000
        };
        using var message = new MailMessage(from, email)
        {
            Subject = "Mark AI · Confirmă modificarea contului",
            IsBodyHtml = true,
            Body = $"""
            <div style="background:#eaf5fa;padding:32px;font-family:Arial,sans-serif;color:#15305a">
            <div style="max-width:480px;margin:auto;background:white;border-radius:24px;padding:32px;border-bottom:6px solid #0d5c9e">
            <h2 style="color:#6b3410">Bac la Română</h2><h3>Încă un pas și e gata!</h3>
            <p>Confirmă {WebUtility.HtmlEncode(actiune)} cu acest cod:</p>
            <p style="background:#fff8dc;border:2px solid #ffc61a;border-radius:16px;padding:20px;font-size:32px;letter-spacing:8px;text-align:center">{cod}</p>
            <p>Codul expiră în 5 minute și poate fi folosit o singură dată.</p>
            <p>Dacă nu ai cerut această modificare, nu comunica acest cod nimănui și verifică securitatea contului.</p>
            </div></div>
            """
        };
        try { await client.SendMailAsync(message, ct); }
        catch (SmtpException) { throw new EmailNeconfiguratException(); }
    }
}

public sealed class EmailNeconfiguratException : Exception;
public sealed record ModificareConfirmata(string Scop, string Valoare, string HashAnterior, string EmailAnterior);

/// <summary>Single-use, user-bound challenges. Only hashes are retained, never plaintext passwords/codes.</summary>
public sealed class ConfirmariCont(TimeProvider clock)
{
    private sealed record Provocare(string Id, byte[] HashCod, ModificareConfirmata Date, DateTimeOffset Expira)
    { public int Incercari { get; set; } }
    private readonly Dictionary<int, Provocare> active = new();
    private readonly Dictionary<int, DateTimeOffset> cereri = new();
    private readonly object sync = new();
    private readonly byte[] secret = RandomNumberGenerator.GetBytes(32);
    private byte[] Hash(string id, string code) => HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(id + ":" + code));

    public void Limiteaza(int userId)
    {
        lock (sync)
        {
            var now = clock.GetUtcNow();
            foreach (var key in cereri.Where(x => now - x.Value > TimeSpan.FromMinutes(10)).Select(x => x.Key).ToArray()) cereri.Remove(key);
            foreach (var key in active.Where(x => x.Value.Expira <= now).Select(x => x.Key).ToArray()) active.Remove(key);
            if (cereri.TryGetValue(userId, out var last) && now - last < TimeSpan.FromSeconds(60))
                throw new CerereInvalida("Așteaptă un minut înainte de o cerere nouă.");
            cereri[userId] = now;
        }
    }

    public (string Id, string Cod) Creeaza(int userId, ModificareConfirmata date)
    {
        lock (sync)
        {
            var id = Guid.NewGuid().ToString("N");
            var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6");
            active[userId] = new(id, Hash(id, code), date, clock.GetUtcNow().AddMinutes(5));
            return (id, code);
        }
    }

    public void Anuleaza(int userId) { lock (sync) active.Remove(userId); }

    public ModificareConfirmata Consuma(int userId, string id, string code)
    {
        lock (sync)
        {
            if (!active.TryGetValue(userId, out var challenge) || challenge.Expira <= clock.GetUtcNow())
            { active.Remove(userId); throw new CerereInvalida("Codul a expirat. Solicită unul nou."); }
            if (++challenge.Incercari > 5) { active.Remove(userId); throw new CerereInvalida("Prea multe încercări. Solicită un cod nou."); }
            if (challenge.Id != id || !CryptographicOperations.FixedTimeEquals(challenge.HashCod, Hash(id, code)))
                throw new CerereInvalida("Codul nu este corect. Verifică emailul și încearcă din nou.");
            active.Remove(userId);
            return challenge.Date;
        }
    }
}
