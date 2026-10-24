using System.Diagnostics;
using System.Security.Cryptography;
using MarkAi.Api.Infrastructura;
using Microsoft.Extensions.Caching.Memory;

namespace MarkAi.Api.Servicii;

/// <summary>Creates a disposable PDF copy; the uploaded Office document is never modified.</summary>
public sealed class PrevizualizareOffice(IConfiguration cfg, ILogger<PrevizualizareOffice> log) : IDisposable
{
    private readonly SemaphoreSlim coada = new(1, 1);
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 40 * 1024 * 1024 });

    public void Dispose() { cache.Dispose(); coada.Dispose(); }

    public async Task<byte[]> PdfAsync(string sursa, string tip, CancellationToken ct)
    {
        if (tip is not ("docx" or "pptx" or "xlsx")) throw new CerereInvalida("Alege un document Word, Excel sau PowerPoint.");
        var executabil = cfg["Previzualizare:LibreOffice"];
        if (string.IsNullOrWhiteSpace(executabil))
        {
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "markai-libreoffice", "program", "soffice.exe");
            executabil = File.Exists(local) ? local : OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe") : "/usr/bin/libreoffice";
        }
        if (!File.Exists(executabil)) throw new CerereInvalida("Previzualizarea documentelor nu este disponibilă momentan. Poți descărca fișierul pentru a-l deschide.");
        using var termen = CancellationTokenSource.CreateLinkedTokenSource(ct);
        termen.CancelAfter(TimeSpan.FromSeconds(90));
        var intrat = false;
        string? director = null;
        try
        {
            await using var original = File.OpenRead(sursa);
            var cheie = tip + Convert.ToHexString(await SHA256.HashDataAsync(original, termen.Token));
            if (cache.TryGetValue<byte[]>(cheie, out var existent)) return existent!;
            await coada.WaitAsync(termen.Token);
            intrat = true;
            if (cache.TryGetValue<byte[]>(cheie, out existent)) return existent!;
            director = Directory.CreateTempSubdirectory("markai-preview-").FullName;
            var intrare = Path.Combine(director, "document." + tip);
            File.Copy(sursa, intrare);
            var profil = Path.Combine(director, "profil");
            Directory.CreateDirectory(Path.Combine(profil, "user"));
            // Separate profile: never execute macros or refresh linked external content.
            await File.WriteAllTextAsync(Path.Combine(profil, "user", "registrymodifications.xcu"), """
                <?xml version="1.0" encoding="UTF-8"?>
                <oor:items xmlns:oor="http://openoffice.org/2001/registry">
                <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item>
                <item oor:path="/org.openoffice.Office.Calc/Content/Update"><prop oor:name="Link" oor:op="fuse"><value>0</value></prop></item>
                <item oor:path="/org.openoffice.Office.Writer/Content/Update"><prop oor:name="Link" oor:op="fuse"><value>2</value></prop></item>
                </oor:items>
                """, termen.Token);
            var start = new ProcessStartInfo(executabil) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-env:UserInstallation=" + new Uri(profil).AbsoluteUri, "--headless", "--nologo", "--nodefault", "--norestore", "--convert-to", "pdf", "--outdir", director, intrare }) start.ArgumentList.Add(arg);
            using var proces = Process.Start(start) ?? throw new IOException("Converter did not start.");
            var stdout = proces.StandardOutput.ReadToEndAsync(termen.Token);
            var stderr = proces.StandardError.ReadToEndAsync(termen.Token);
            try { await proces.WaitForExitAsync(termen.Token); await Task.WhenAll(stdout, stderr); }
            finally { if (!proces.HasExited) { proces.Kill(entireProcessTree: true); await proces.WaitForExitAsync(CancellationToken.None); } }
            var pdf = Path.Combine(director, "document.pdf");
            if (proces.ExitCode != 0 || !File.Exists(pdf)) throw new IOException("No PDF produced.");
            if (new FileInfo(pdf).Length > 20 * 1024 * 1024) throw new CerereInvalida("Documentul este prea mare pentru previzualizare. Descarcă-l pentru a-l deschide.");
            var date = await File.ReadAllBytesAsync(pdf, termen.Token);
            if (!date.AsSpan().StartsWith("%PDF-"u8)) throw new IOException("Invalid PDF output.");
            cache.Set(cheie, date, new MemoryCacheEntryOptions { Size = date.Length, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) });
            return date;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new CerereInvalida("Deschiderea documentului durează prea mult. Încearcă din nou sau descarcă fișierul."); }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            log.LogWarning("Office preview failed ({TipEroare}).", ex.GetType().Name);
            throw new CerereInvalida("Documentul nu poate fi previzualizat. Descarcă-l pentru a-l deschide.");
        }
        finally
        {
            if (director is not null) { try { Directory.Delete(director, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            if (intrat) coada.Release();
        }
    }
}
