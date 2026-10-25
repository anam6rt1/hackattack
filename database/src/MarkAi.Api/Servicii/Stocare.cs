using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using MarkAi.Api.Infrastructura;
using MarkAi.Data.Entities;

namespace MarkAi.Api.Servicii;

/// <summary>A stored chapter file: its storage key, type, size and SHA-256 (hex).</summary>
public sealed record FisierSalvat(string Cheie, string Tip, long Marime, string Sha256);

/// <summary>
/// Local disk storage for professor photos, qualification documents and chapter files. Files get random names and are
/// only ever served through API endpoints (never as static files), so access rules stay in the API.
/// The content must match the extension (checked from the first bytes), so a renamed file is refused.
/// </summary>
public sealed class Stocare(IConfiguration cfg, IWebHostEnvironment env, ILogger<Stocare> log)
{
    private readonly object fisiereLock = new();
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

    /// <summary>Largest Lecție file (<c>Fisiere:MaxMb</c>, 20 by default); its upload request may be a little larger (multipart framing).</summary>
    public int MaxFisierLectieMb => Math.Max(1, cfg.GetValue("Fisiere:MaxMb", 20));

    private static bool EsteZip(byte[] b) => Incepe(b, (byte)'P', (byte)'K', 3, 4);
    private static readonly Dictionary<string, Tip> FisiereLectie = new()
    {
        [".pdf"] = Documente[".pdf"],
        [".docx"] = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", EsteZip),
        [".xlsx"] = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", EsteZip),
        [".pptx"] = new("application/vnd.openxmlformats-officedocument.presentationml.presentation", EsteZip),
    };

    /// <summary>The folder of Lecție files, for the orphan sweep.</summary>
    public const string DirectorLectii = "lectii";

    public async Task<string> SalveazaPozaAsync(IFormFile f, CancellationToken ct)
    {
        var original = await Salveaza(f, "poze", Imagini, 5, null, ct);
        var cheie = $"poze/{Guid.NewGuid():N}.webp";
        try
        {
            var variante = await PozeProfil.CreeazaAsync(await File.ReadAllBytesAsync(Cale(original.Cheie)!, ct), ct);
            await File.WriteAllBytesAsync(Cale(cheie)!, variante.Profil, ct);
            await File.WriteAllBytesAsync(Cale(cheie + ".mic.webp")!, variante.Mica, ct);
            return cheie;
        }
        catch { Sterge(cheie); throw; }
        finally { Sterge(original.Cheie); }
    }

    /// <summary>Legacy photos acquire cached derivatives on demand, after endpoint access checks.</summary>
    public async Task<IResult> ServestePozaAsync(string? cheie, bool mica, CancellationToken ct)
    {
        if (Cale(cheie) is not { } cale || !File.Exists(cale)) return Results.NotFound();
        var varianta = cheie + (mica ? ".mic.webp" : ".profil.webp");
        if (!File.Exists(Cale(varianta)))
        {
            if (!mica && File.Exists(Cale(cheie + ".mic.webp"))) return Serveste(cheie);
            if (new FileInfo(cale).Length > 5 * 1024 * 1024) return Results.NotFound();
            var poze = await PozeProfil.CreeazaAsync(await File.ReadAllBytesAsync(cale, ct), ct);
            lock (fisiereLock)
            {
                if (!File.Exists(cale)) return Results.NotFound();
                ScrieVarianta(Cale(cheie + ".mic.webp")!, poze.Mica);
                ScrieVarianta(Cale(cheie + ".profil.webp")!, poze.Profil);
            }
        }
        return Serveste(varianta);
    }

    private static void ScrieVarianta(string cale, byte[] date)
    {
        var temporar = cale + "." + Guid.NewGuid().ToString("N") + ".partial";
        try { File.WriteAllBytes(temporar, date); File.Move(temporar, cale, overwrite: true); }
        finally { File.Delete(temporar); }
    }

    public async Task<string> SalveazaDovadaAsync(IFormFile f, CancellationToken ct) => (await Salveaza(f, "dovezi", Documente, 10, null, ct)).Cheie;

    /// <summary>A Lecție file: PDF, or DOCX/PPTX checked inside (a real Office document, no macros, no zip bomb).</summary>
    public async Task<FisierSalvat> SalveazaFisierLectieAsync(IFormFile f, CancellationToken ct, bool deduplica = true)
    {
        var salvat = await Salveaza(f, DirectorLectii, FisiereLectie, MaxFisierLectieMb, VerificaFisierLectie, ct);
        if (!deduplica) return salvat;
        var cheie = $"{DirectorLectii}/{salvat.Sha256}.{salvat.Tip}";
        var destinatie = Cale(cheie)!;
        lock (fisiereLock)
        {
            try { File.Move(Cale(salvat.Cheie)!, destinatie); }
            catch (IOException) when (File.Exists(destinatie)) { File.Delete(Cale(salvat.Cheie)!); }
            // Renew the orphan grace period before a new reference is committed.
            File.SetLastWriteTimeUtc(destinatie, DateTime.UtcNow);
        }
        return salvat with { Cheie = cheie };
    }

    public string Consolideaza(string cheie, string sha, string tip)
    {
        if (sha.Length != 64 || !sha.All(Uri.IsHexDigit) || !FisiereLectie.ContainsKey("." + tip)) return cheie;
        var noua = $"{DirectorLectii}/{sha.ToLowerInvariant()}.{tip}";
        if (noua == cheie || !File.Exists(Cale(cheie))) return cheie;
        lock (fisiereLock)
        {
            var destinatie = Cale(noua)!;
            if (!File.Exists(destinatie))
            {
                var temporar = destinatie + "." + Guid.NewGuid().ToString("N") + ".partial";
                try { File.Copy(Cale(cheie)!, temporar); try { File.Move(temporar, destinatie); } catch (IOException) when (File.Exists(destinatie)) { } }
                finally { File.Delete(temporar); }
            }
            File.SetLastWriteTimeUtc(destinatie, DateTime.UtcNow);
        }
        return noua;
    }

    private async Task<FisierSalvat> Salveaza(IFormFile f, string folder, Dictionary<string, Tip> permise, int maxMb,
        Action<string, string>? verificaContinut, CancellationToken ct)
    {
        var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
        if (!permise.TryGetValue(ext, out var tip)) throw new CerereInvalida($"Tip de fișier nepermis. Acceptăm: {string.Join(", ", permise.Keys)}.");
        if (f.Length == 0 || f.Length > (long)maxMb * 1024 * 1024) throw new CerereInvalida($"Fișierul trebuie să aibă cel mult {maxMb} MB.");

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
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var s = File.Create(temporar))
            await using (var citire = f.OpenReadStream())
            {
                var buffer = new byte[81920];
                int n;
                while ((n = await citire.ReadAsync(buffer, ct)) > 0)
                {
                    hash.AppendData(buffer, 0, n);
                    await s.WriteAsync(buffer.AsMemory(0, n), ct);
                }
            }
            verificaContinut?.Invoke(temporar, ext);
            File.Move(temporar, cale);
            return new FisierSalvat(cheie, ext.TrimStart('.'), f.Length, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        finally { File.Delete(temporar); }
    }

    // Limits for the inside of a DOCX/PPTX (a ZIP): real documents stay far below them.
    private const int MaxIntrariZip = 5000;
    private const long MaxDezarhivatOcteti = 250L * 1024 * 1024;
    private const int MaxRaportCompresie = 200;

    private static void VerificaFisierLectie(string cale, string ext)
    {
        if (ext == ".pdf") return; // the "%PDF-" signature was checked; PdfPig reads it later in the background
        var tip = ext == ".docx" ? "DOCX" : "PPTX";
        try
        {
            using var zip = ZipFile.OpenRead(cale);
            if (zip.Entries.Count > MaxIntrariZip) throw new CerereInvalida($"Fișierul {tip} are prea multe părți.");
            long total = 0;
            foreach (var e in zip.Entries)
            {
                total += e.Length;
                if (total > MaxDezarhivatOcteti || (e.Length > 1024 * 1024 && e.Length > (long)MaxRaportCompresie * Math.Max(e.CompressedLength, 1)))
                    throw new CerereInvalida($"Fișierul {tip} se dezarhivează la o mărime neobișnuit de mare și nu poate fi acceptat.");
                // Macros (VBA) and ActiveX controls are code; documents with them are refused whatever their extension.
                var nume = e.FullName.Replace('\\', '/').ToLowerInvariant();
                if (nume.EndsWith("vbaproject.bin") || nume.EndsWith("vbadata.xml") || nume.Contains("/activex/"))
                    throw new CerereInvalida($"Fișierul {tip} conține macrocomenzi. Salvează-l fără macrocomenzi și încearcă din nou.");
            }
            var tipuri = zip.GetEntry("[Content_Types].xml") ?? throw new CerereInvalida($"Fișierul nu este un {tip} valid.");
            string continut;
            using (var r = new StreamReader(tipuri.Open())) continut = CitesteCelMult(r, 1024 * 1024);
            if (continut.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase))
                throw new CerereInvalida($"Fișierul {tip} conține macrocomenzi. Salvează-l fără macrocomenzi și încearcă din nou.");
            var principal = ext switch { ".docx" => "word/document.xml", ".xlsx" => "xl/workbook.xml", _ => "ppt/presentation.xml" };
            if (zip.GetEntry(principal) is null) throw new CerereInvalida($"Fișierul nu este un {tip} valid.");
        }
        catch (InvalidDataException) { throw new CerereInvalida($"Fișierul nu este un {tip} valid."); }
    }

    private static string CitesteCelMult(StreamReader r, int caractere)
    {
        var buffer = new char[caractere];
        var n = r.ReadBlock(buffer, 0, caractere);
        return new string(buffer, 0, n);
    }

    /// <summary>Deletes a stored file that is no longer referenced (after the database change is saved).</summary>
    public void Sterge(string? cheie, bool orfan = false, DateTime? inainteDe = null)
    {
        // Content-addressed lesson files can be shared. Only the reference-aware sweep removes them.
        if (!orfan && cheie?.StartsWith(DirectorLectii + "/", StringComparison.Ordinal) == true
            && Path.GetFileNameWithoutExtension(cheie).Length == 64) return;
        if (Cale(cheie) is not { } cale) return;
        try { lock (fisiereLock) { if (inainteDe is { } prag && File.GetLastWriteTimeUtc(cale) > prag) return; File.Delete(cale); File.Delete(cale + ".mic.webp"); File.Delete(cale + ".profil.webp"); } }
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

    /// <summary>
    /// A Lecție file. A PDF may open in the browser's own viewer (<paramref name="inline"/>), still with no scripts of ours
    /// and never framed; DOCX/PPTX are always downloaded under the uploader's file name.
    /// </summary>
    public IResult ServesteFisierLectie(HttpContext h, string cheie, string tip, string nume, bool inline)
    {
        if (Cale(cheie) is not { } cale || !File.Exists(cale)) return Results.NotFound();
        var mime = FisiereLectie["." + tip].Mime;
        if (!inline || tip != FisierLectie.Pdf) return Results.File(cale, mime, fileDownloadName: nume, enableRangeProcessing: true);
        // The CSP "sandbox" set for every API response stops browsers' PDF viewers, so a PDF shown inline gets a strict policy without it.
        h.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; form-action 'none'; base-uri 'none'";
        h.Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = nume }.ToString();
        return Results.File(cale, mime, enableRangeProcessing: true);
    }

    /// <summary>Every stored key under a folder, with its last write time (the orphan sweep compares them with the database).</summary>
    public IEnumerable<(string Cheie, DateTime ModificatLa)> Chei(string folder)
    {
        var dir = Path.Combine(Radacina, folder);
        if (!Directory.Exists(dir)) yield break;
        foreach (var cale in Directory.EnumerateFiles(dir))
            yield return ($"{folder}/{Path.GetFileName(cale)}", File.GetLastWriteTimeUtc(cale));
    }

    /// <summary>The file on disk for a key, for reading (text extraction); null when the key is invalid.</summary>
    public string? CaleFisier(string cheie) => Cale(cheie);

    public void StergeDatePersonale(string cheie)
    {
        var parti = cheie.Split('/');
        var inFolderPersonal = parti.Length == 2 && (parti[0] == "poze" || parti[0] == "dovezi");
        var nume = parti[^1];
        var legacy = parti.Length == 1 && new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" }.Contains(Path.GetExtension(nume).ToLowerInvariant());
        if ((!inFolderPersonal && !legacy) || nume.Length == 0 || nume is "." or ".." || nume.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
            || Cale(cheie) is not { } cale) throw new InvalidDataException("Invalid personal-file key.");
        lock (fisiereLock)
        {
            File.Delete(cale); // Let the durable cleanup job retry failures instead of marking them completed.
            File.Delete(cale + ".mic.webp");
            File.Delete(cale + ".profil.webp");
        }
    }

    private string? Cale(string? cheie)
    {
        if (string.IsNullOrEmpty(cheie)) return null;
        var cale = Path.GetFullPath(Path.Combine(Radacina, cheie));
        return cale.StartsWith(Radacina + Path.DirectorySeparatorChar) ? cale : null;
    }
}
