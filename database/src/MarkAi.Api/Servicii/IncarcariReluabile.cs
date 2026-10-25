using System.Security.Cryptography;
using System.Text.Json;
using MarkAi.Api.Endpoints;
using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

public record CerereIncarcare(string Nume, long Marime, string Sha256, bool Inlocuieste = false);

/// <summary>Authenticated, disk-backed upload sessions. A failed chunk never advances the committed offset.</summary>
public sealed class IncarcariReluabile(IConfiguration cfg, IWebHostEnvironment env) : IDisposable
{
    public const int Bucata = 1024 * 1024;
    private readonly SemaphoreSlim mutex = new(1, 1);
    private string Radacina => Path.GetFullPath(Path.Combine(env.ContentRootPath, cfg["Stocare:Director"] ?? "uploads", "incarcari"));
    private sealed record Sesiune(int Autor, int Lectie, CerereIncarcare Cerere, DateTime Expira);
    public void Dispose() => mutex.Dispose();
    public async Task<object[]> ExportaPentruAsync(int autor, CancellationToken ct)
    {
        await mutex.WaitAsync(ct);
        try
        {
            var rezultat = new List<object>();
            if (!Directory.Exists(Radacina)) return [];
            foreach (var cale in Directory.EnumerateFiles(Radacina, "*.json"))
            {
                var sesiune = JsonSerializer.Deserialize<Sesiune>(await File.ReadAllTextAsync(cale, ct))!;
                if (sesiune.Autor == autor) rezultat.Add(new { sesiune.Lectie, sesiune.Cerere.Nume, sesiune.Cerere.Marime, sesiune.Expira });
            }
            return rezultat.ToArray();
        }
        finally { mutex.Release(); }
    }
    public async Task StergePentruAsync(int autor, CancellationToken ct)
    {
        await mutex.WaitAsync(ct);
        try
        {
            if (!Directory.Exists(Radacina)) return;
            foreach (var cale in Directory.EnumerateFiles(Radacina, "*.json"))
            {
                var sesiune = JsonSerializer.Deserialize<Sesiune>(await File.ReadAllTextAsync(cale, ct))!;
                if (sesiune.Autor != autor) continue;
                File.Delete(Path.ChangeExtension(cale, ".bin"));
                File.Delete(cale);
            }
        }
        finally { mutex.Release(); }
    }
    private string Cale(string token)
    {
        if (!Guid.TryParseExact(token, "N", out _)) throw new KeyNotFoundException("Încărcarea nu mai este disponibilă.");
        return Path.Combine(Radacina, token);
    }
    private async Task<Sesiune> Citeste(string token, int autor, CancellationToken ct)
    {
        var cale = Cale(token) + ".json";
        if (!File.Exists(cale)) throw new KeyNotFoundException("Încărcarea nu mai este disponibilă.");
        var s = JsonSerializer.Deserialize<Sesiune>(await File.ReadAllTextAsync(cale, ct))!;
        if (s.Autor != autor || s.Expira < DateTime.UtcNow) throw new KeyNotFoundException("Încărcarea nu mai este disponibilă.");
        return s;
    }
    public static void Mapeaza(RouteGroupBuilder e)
    {
        e.MapPost("/parcursuri/{id:int}/incarcari", async (int id, CerereIncarcare c, HttpContext h, AccesContinut acces,
            Stocare stocare, IncarcariReluabile serviciu, CancellationToken ct) =>
        {
            await acces.PentruEditareAsync(h.User, id, ct);
            if (string.IsNullOrWhiteSpace(c.Nume) || c.Nume.Length > 255 || string.IsNullOrEmpty(c.Sha256) || c.Marime <= 0 || c.Marime > stocare.MaxFisierLectieMb * 1024L * 1024 || c.Sha256.Length != 64 || !c.Sha256.All(Uri.IsHexDigit)
                || Path.GetExtension(c.Nume).ToLowerInvariant() is not (".pdf" or ".docx" or ".pptx" or ".xlsx")) throw new CerereInvalida("Alege un document valid, în limita permisă.");
            await serviciu.mutex.WaitAsync(ct);
            try
            {
                Directory.CreateDirectory(serviciu.Radacina);
                var active = 0;
                foreach (var f in Directory.EnumerateFiles(serviciu.Radacina, "*.json"))
                {
                    var s = JsonSerializer.Deserialize<Sesiune>(await File.ReadAllTextAsync(f, ct))!;
                    if (s.Expira < DateTime.UtcNow) { File.Delete(Path.ChangeExtension(f, ".bin")); File.Delete(f); }
                    else if (s.Autor == h.User.Id()) active++;
                }
                if (active >= 10) throw new CerereInvalida("Ai deja mai multe încărcări neterminate. Reia-le sau încearcă mai târziu.");
                var token = Guid.NewGuid().ToString("N");
                var sesiune = new Sesiune(h.User.Id(), id, c, DateTime.UtcNow.AddHours(24));
                var cale = serviciu.Cale(token);
                try
                {
                    await File.WriteAllBytesAsync(cale + ".bin", [], ct);
                    await File.WriteAllTextAsync(cale + ".partial", JsonSerializer.Serialize(sesiune), ct);
                    File.Move(cale + ".partial", cale + ".json");
                }
                catch { File.Delete(cale + ".bin"); File.Delete(cale + ".partial"); throw; }
                return Results.Ok(new { token, offset = 0, bucata = Bucata });
            }
            finally { serviciu.mutex.Release(); }
        });
        e.MapGet("/incarcari/{token}", async (string token, HttpContext h, IncarcariReluabile serviciu, AccesContinut acces, CancellationToken ct) =>
        {
            var s = await serviciu.Citeste(token, h.User.Id(), ct);
            await acces.PentruEditareAsync(h.User, s.Lectie, ct);
            return Results.Ok(new { token, offset = new FileInfo(serviciu.Cale(token) + ".bin").Length, bucata = Bucata });
        });
        e.MapPut("/incarcari/{token}", async (string token, long offset, HttpContext h, IncarcariReluabile serviciu, AccesContinut acces, CancellationToken ct) =>
        {
            await serviciu.mutex.WaitAsync(ct);
            try
            {
                var s = await serviciu.Citeste(token, h.User.Id(), ct);
                await acces.PentruEditareAsync(h.User, s.Lectie, ct);
                await using var destinatie = new FileStream(serviciu.Cale(token) + ".bin", FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                if (offset != destinatie.Length) return Results.Conflict(new { mesaj = "Încărcarea trebuie reluată de la ultima porțiune primită.", offset = destinatie.Length });
                // Buffer at most one chunk plus one byte; reject oversized bodies without saving a prefix.
                var bytes = new byte[Bucata + 1];
                var n = await h.Request.Body.ReadAtLeastAsync(bytes, bytes.Length, false, ct);
                if (n == 0 || n > Bucata || offset + n > s.Cerere.Marime) throw new CerereInvalida("Porțiunea încărcată nu este validă.");
                destinatie.Position = offset;
                try { await destinatie.WriteAsync(bytes.AsMemory(0, n), ct); await destinatie.FlushAsync(ct); }
                catch { destinatie.SetLength(offset); throw; }
                return Results.Ok(new { offset = offset + n });
            }
            finally { serviciu.mutex.Release(); }
        }).DisableAntiforgery();
        e.MapPost("/incarcari/{token}/finalizeaza", async (string token, HttpContext h, IncarcariReluabile serviciu, AccesContinut acces,
            MarkAiDbContext db, Stocare stocare, ProcesorFisiere procesor, CancellationToken ct) =>
        {
            await serviciu.mutex.WaitAsync(ct);
            try
            {
                var s = await serviciu.Citeste(token, h.User.Id(), ct);
                await acces.PentruEditareAsync(h.User, s.Lectie, ct);
                await using var stream = File.OpenRead(serviciu.Cale(token) + ".bin");
                if (stream.Length != s.Cerere.Marime) throw new CerereInvalida("Încărcarea nu este încă completă.");
                var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
                if (!hash.Equals(s.Cerere.Sha256, StringComparison.OrdinalIgnoreCase)) throw new CerereInvalida("Fișierul primit este incomplet. Selectează-l din nou.");
                var existent = await db.Set<FisierLectie>().AsNoTracking().SingleOrDefaultAsync(x => x.ParcursId == s.Lectie
                    && x.Tip == Path.GetExtension(s.Cerere.Nume).TrimStart('.').ToLowerInvariant(), ct);
                if (existent?.Sha256 == hash && existent.IncarcatDeId == s.Autor)
                    return Results.Ok(new { existent.Id }); // A lost final response can safely be retried.
                stream.Position = 0;
                var formular = new FormFile(stream, 0, stream.Length, "fisier", s.Cerere.Nume);
                return await FisiereEndpoints.SalveazaAsync(s.Lectie, s.Cerere.Inlocuieste, formular, h, db, acces, stocare, procesor, ct);
            }
            finally { serviciu.mutex.Release(); }
        });
        e.MapDelete("/incarcari/{token}", async (string token, HttpContext h, IncarcariReluabile serviciu, CancellationToken ct) =>
        {
            await serviciu.mutex.WaitAsync(ct);
            try { await serviciu.Citeste(token, h.User.Id(), ct); File.Delete(serviciu.Cale(token) + ".bin"); File.Delete(serviciu.Cale(token) + ".json"); return Results.NoContent(); }
            finally { serviciu.mutex.Release(); }
        });
    }
}
