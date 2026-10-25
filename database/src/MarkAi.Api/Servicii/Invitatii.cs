using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace MarkAi.Api.Servicii;
public record RandInvitatie(int Rand, string Nume, string Email, string Clasa, string? Eroare);
public static class Invitatii
{
    public static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string Hash(string? token)
    {
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit)) throw new CerereInvalida("Linkul nu este valid.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
    public static string Adresa(string email)
    {
        email = Validare.Obligatoriu(email, "Email", 200).ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out var m) || m.Address != email) throw new CerereInvalida("Adresa de email nu este validă.");
        return email;
    }
    public static string Url(IConfiguration cfg, string cale, string token)
    {
        var origine = cfg["Aplicatie:Url"] ?? "http://localhost:3000";
        if (!Uri.TryCreate(origine, UriKind.Absolute, out var u) || (u.Scheme != "https" && !(u.Scheme == "http" && u.IsLoopback))) throw new InvalidOperationException("Configurează Aplicatie:Url cu adresa HTTPS a aplicației.");
        return origine.TrimEnd('/') + cale + "#" + token;
    }
    public static Email Mesaj(string catre, string titlu, string text, string url) => new(catre, titlu,
        $"<p>{WebUtility.HtmlEncode(text)}</p><p><a href=\"{WebUtility.HtmlEncode(url)}\">Deschide în TeleBac</a></p>", text + "\n" + url);
    // Called inside the transaction; serializes budget reservations across C2/C4 requests and the worker.
    public static async Task Buget(MarkAiDbContext db, IConfiguration cfg, DateTime now, int numar, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7731024)", ct);
        var deLa = now.AddHours(-24);
        if (await db.Set<MesajIesire>().CountAsync(m => m.Canal == "email" && m.CreatLa >= deLa, ct) + numar > cfg.GetValue("Email:MaxPeZi", 500))
            throw new Conflict("Limita zilnică de emailuri a fost atinsă. Încearcă mâine.");
    }
    public static List<RandInvitatie> Citeste(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv) || Encoding.UTF8.GetByteCount(csv) > 150_000) throw new CerereInvalida("Alege un CSV de cel mult 150 KB și 100 de elevi.");
        using var parser = new TextFieldParser(new StringReader(csv.TrimStart('\uFEFF'))) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(",");
        try
        {
            var antet = parser.ReadFields();
            if (antet is null || !antet.Select(x => x.ToLowerInvariant()).SequenceEqual(new[] { "nume", "email", "clasa" })) throw new CerereInvalida("Antetul trebuie să fie: nume,email,clasa.");
            var lista = new List<RandInvitatie>(); var adrese = new HashSet<string>();
            while (!parser.EndOfData)
            {
                if (lista.Count == 100) throw new CerereInvalida("Un import poate avea cel mult 100 de elevi.");
                var f = parser.ReadFields() ?? []; string? eroare = null;
                var nume = f.ElementAtOrDefault(0) ?? ""; var email = f.ElementAtOrDefault(1) ?? ""; var clasa = f.ElementAtOrDefault(2) ?? "";
                try
                {
                    if (f.Length != 3) throw new CerereInvalida("Sunt necesare exact 3 coloane.");
                    nume = Validare.Obligatoriu(nume, "Nume", 120); email = Adresa(email);
                    if (!Parcurs.Clase.Contains(clasa)) throw new CerereInvalida("Clasa trebuie să fie VIII, IX, X, XI, XII sau Bac.");
                    if (!adrese.Add(email)) throw new CerereInvalida("Adresa apare de mai multe ori în fișier.");
                }
                catch (CerereInvalida ex) { eroare = ex.Message; }
                lista.Add(new(lista.Count + 2, nume, email, clasa, eroare));
            }
            if (lista.Count == 0) throw new CerereInvalida("Fișierul nu conține elevi.");
            return lista;
        }
        catch (MalformedLineException) { throw new CerereInvalida("CSV invalid: verifică ghilimelele și separatorul virgulă."); }
    }
}
