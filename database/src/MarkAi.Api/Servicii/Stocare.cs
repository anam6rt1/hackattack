using MarkAi.Api.Infrastructura;

namespace MarkAi.Api.Servicii;

/// <summary>
/// Local disk storage for professor photos and qualification documents. Files get random names and are
/// only ever served through API endpoints (never as static files), so access rules stay in the API.
/// The content must match the extension (checked from the first bytes), so a renamed file is refused.
/// </summary>
public sealed class Stocare(IConfiguration cfg, IWebHostEnvironment env, ILogger<Stocare> log)
{
    /// <summary>Largest upload request (photo + document at registration), applied per endpoint.</summary>
    public const long MaxCerereMb = 16;

    private sealed record Tip(string Mime, Func<byte[], bool> Semnatura);

    private static bool Incepe(byte[] b, params byte[] prefix) => b.Length >= prefix.Length && b.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    private static readonly Tip Jpeg = new("image/jpeg", b => Incepe(b, 0xFF, 0xD8, 0xFF));
    private static readonly Dictionary<string, Tip> Imagini = new()
    {
        [".jpg"] = Jpeg, [".jpeg"] = Jpeg,
        [".png"] = new("image/png", b => Incepe(b, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)),
        [".webp"] = new("image/webp", b => Incepe(b, (byte)'R', (byte)'I', (byte)'F', (byte)'F') && b.Length >= 12
            && b.AsSpan(8, 4).SequenceEqual("WEBP"u8)),
    };
    private static readonly Dictionary<string, Tip> Documente = new(Imagini) { [".pdf"] = new("application/pdf", b => Incepe(b, "%PDF-"u8.ToArray())) };

    private string Radacina => Path.GetFullPath(Path.Combine(env.ContentRootPath, cfg["Stocare:Director"] ?? "uploads"));

    public Task<string> SalveazaPozaAsync(IFormFile f, CancellationToken ct) => Salveaza(f, "poze", Imagini, 5, ct);

    public Task<string> SalveazaDovadaAsync(IFormFile f, CancellationToken ct) => Salveaza(f, "dovezi", Documente, 10, ct);

    private async Task<string> Salveaza(IFormFile f, string folder, Dictionary<string, Tip> permise, int maxMb, CancellationToken ct)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        if (!permise.TryGetValue(ext, out var tip)) throw new CerereInvalida($"Tip de fișier nepermis. Acceptăm: {string.Join(", ", permise.Keys)}.");
        if (f.Length == 0 || f.Length > maxMb * 1024 * 1024) throw new CerereInvalida($"Fișierul trebuie să aibă cel mult {maxMb} MB.");

        var inceput = new byte[16];
        await using (var citire = f.OpenReadStream())
            Array.Resize(ref inceput, await citire.ReadAtLeastAsync(inceput, inceput.Length, throwOnEndOfStream: false, ct));
        if (!tip.Semnatura(inceput)) throw new CerereInvalida($"Fișierul nu este un {ext.TrimStart('.').ToUpperInvariant()} valid.");

        var cheie = $"{folder}/{Guid.NewGuid():N}{ext}";
        var cale = Path.Combine(Radacina, cheie);
        Directory.CreateDirectory(Path.GetDirectoryName(cale)!);
        // Written next to the final name, then moved, so a failed upload never leaves a half-written file under a real key.
        var temporar = cale + ".partial";
        try
        {
            await using (var s = File.Create(temporar)) await f.CopyToAsync(s, ct);
            File.Move(temporar, cale);
        }
        finally { File.Delete(temporar); }
        return cheie;
    }

    /// <summary>Deletes a stored file that is no longer referenced (after the database change is saved).</summary>
    public void Sterge(string? cheie)
    {
        if (Cale(cheie) is not { } cale) return;
        try { File.Delete(cale); }
        catch (IOException ex) { log.LogWarning(ex, "Could not delete stored file {Cheie}", cheie); }
    }

    /// <summary>Images are shown inline; documents are always downloaded, never rendered by the browser.</summary>
    public IResult Serveste(string? cheie, string? numeDescarcare = null)
    {
        if (Cale(cheie) is not { } cale || !File.Exists(cale)) return Results.NotFound();
        var ext = Path.GetExtension(cale).ToLowerInvariant();
        if (Imagini.TryGetValue(ext, out var imagine)) return Results.File(cale, imagine.Mime);
        return Results.File(cale, Documente.TryGetValue(ext, out var doc) ? doc.Mime : "application/octet-stream",
            fileDownloadName: (numeDescarcare ?? "document") + ext);
    }

    private string? Cale(string? cheie)
    {
        if (string.IsNullOrEmpty(cheie)) return null;
        var cale = Path.GetFullPath(Path.Combine(Radacina, cheie));
        return cale.StartsWith(Radacina + Path.DirectorySeparatorChar) ? cale : null;
    }
}
