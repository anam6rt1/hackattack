using MarkAi.Api.Infrastructura;

namespace MarkAi.Api.Servicii;

/// <summary>
/// Local disk storage for professor photos and qualification documents. Files get random names and are
/// only ever served through API endpoints (never as static files), so access rules stay in the API.
/// </summary>
public sealed class Stocare(IConfiguration cfg, IWebHostEnvironment env)
{
    private static readonly Dictionary<string, string> Imagini = new()
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp",
    };
    private static readonly Dictionary<string, string> Documente = new(Imagini) { [".pdf"] = "application/pdf" };

    private string Radacina => Path.GetFullPath(Path.Combine(env.ContentRootPath, cfg["Stocare:Director"] ?? "uploads"));

    public Task<string> SalveazaPozaAsync(IFormFile f, CancellationToken ct) => Salveaza(f, "poze", Imagini, 5, ct);

    public Task<string> SalveazaDovadaAsync(IFormFile f, CancellationToken ct) => Salveaza(f, "dovezi", Documente, 10, ct);

    private async Task<string> Salveaza(IFormFile f, string folder, Dictionary<string, string> permise, int maxMb, CancellationToken ct)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        if (!permise.ContainsKey(ext)) throw new CerereInvalida($"Tip de fișier nepermis. Acceptăm: {string.Join(", ", permise.Keys)}.");
        if (f.Length == 0 || f.Length > maxMb * 1024 * 1024) throw new CerereInvalida($"Fișierul trebuie să aibă cel mult {maxMb} MB.");
        var cheie = $"{folder}/{Guid.NewGuid():N}{ext}";
        var cale = Path.Combine(Radacina, cheie);
        Directory.CreateDirectory(Path.GetDirectoryName(cale)!);
        await using var s = File.Create(cale);
        await f.CopyToAsync(s, ct);
        return cheie;
    }

    public IResult Serveste(string? cheie)
    {
        if (string.IsNullOrEmpty(cheie)) return Results.NotFound();
        var cale = Path.GetFullPath(Path.Combine(Radacina, cheie));
        if (!cale.StartsWith(Radacina + Path.DirectorySeparatorChar) || !File.Exists(cale)) return Results.NotFound();
        return Results.File(cale, Documente.GetValueOrDefault(Path.GetExtension(cale), "application/octet-stream"));
    }
}
