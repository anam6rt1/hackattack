using System.Diagnostics;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Xml.Linq;
using MarkAi.Api.Infrastructura;
using Microsoft.Extensions.Caching.Memory;

namespace MarkAi.Api.Servicii;

/// <summary>Creates a disposable PDF copy; the uploaded Office document is never modified.</summary>
public sealed class PrevizualizareOffice(IConfiguration cfg, IWebHostEnvironment env, ILogger<PrevizualizareOffice> log) : IDisposable
{
    private readonly SemaphoreSlim coada = new(1, 1);
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 40 * 1024 * 1024 });

    public void Dispose() { cache.Dispose(); coada.Dispose(); }

    private string DirectorCache => Path.GetFullPath(Path.Combine(env.ContentRootPath, cfg["Stocare:Director"] ?? "uploads", "previzualizari-v2"));

    public static string[] Foi(string sursa)
    {
        using var zip = ZipFile.OpenRead(sursa);
        using var xml = zip.GetEntry("xl/workbook.xml")!.Open();
        return XDocument.Load(xml).Descendants().Where(x => x.Name.LocalName == "sheet").Select(x => (string?)x.Attribute("name") ?? "Foaie").ToArray();
    }

    private static void AlegeFoaia(string intrare, int foaie)
    {
        using var zip = ZipFile.Open(intrare, ZipArchiveMode.Update);
        var entry = zip.GetEntry("xl/workbook.xml")!;
        XDocument doc;
        using (var s = entry.Open()) doc = XDocument.Load(s);
        var foi = doc.Descendants().Where(x => x.Name.LocalName == "sheet").ToArray();
        if (foaie < 0 || foaie >= foi.Length) throw new CerereInvalida("Foaia aleasă nu există.");
        for (var i = 0; i < foi.Length; i++) foi[i].SetAttributeValue("state", i == foaie ? "visible" : "hidden");
        foreach (var view in doc.Descendants().Where(x => x.Name.LocalName == "workbookView")) view.SetAttributeValue("activeTab", foaie);
        entry.Delete();
        using (var output = zip.CreateEntry("xl/workbook.xml").Open()) doc.Save(output);
        // Fit wide tables horizontally; keep vertical pagination and all formula/data cells intact.
        foreach (var sheet in zip.Entries.Where(x => x.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal) && x.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToArray())
        {
            XDocument pagina;
            using (var s = sheet.Open()) pagina = XDocument.Load(s);
            var root = pagina.Root!;
            var ns = root.Name.Namespace;
            var proprietati = root.Element(ns + "sheetPr");
            if (proprietati is null) { proprietati = new XElement(ns + "sheetPr"); root.AddFirst(proprietati); }
            var potrivire = proprietati.Element(ns + "pageSetUpPr");
            if (potrivire is null) { potrivire = new XElement(ns + "pageSetUpPr"); proprietati.Add(potrivire); }
            potrivire.SetAttributeValue("fitToPage", "1");
            var setup = root.Element(ns + "pageSetup");
            if (setup is null)
            {
                setup = new XElement(ns + "pageSetup");
                var dupa = root.Elements().FirstOrDefault(x => new[] { "headerFooter", "rowBreaks", "colBreaks", "customProperties", "cellWatches", "ignoredErrors", "smartTags", "drawing", "legacyDrawing", "legacyDrawingHF", "picture", "oleObjects", "controls", "webPublishItems", "tableParts", "extLst" }.Contains(x.Name.LocalName));
                if (dupa is null) root.Add(setup); else dupa.AddBeforeSelf(setup);
            }
            setup.SetAttributeValue("fitToWidth", "1"); setup.SetAttributeValue("fitToHeight", "0");
            var nume = sheet.FullName;
            sheet.Delete();
            using var destinatie = zip.CreateEntry(nume).Open();
            pagina.Save(destinatie);
        }
    }

    public async Task<byte[]> PdfAsync(string sursa, string tip, CancellationToken ct, int? foaie = null)
    {
        if (tip is not ("docx" or "pptx" or "xlsx")) throw new CerereInvalida("Alege un document Word, Excel sau PowerPoint.");
        if (foaie is not null && (tip != "xlsx" || foaie < 0)) throw new CerereInvalida("Foaia aleasă nu există.");
        var executabil = cfg["Previzualizare:LibreOffice"];
        if (string.IsNullOrWhiteSpace(executabil))
        {
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "markai-libreoffice", "program", "soffice.exe");
            executabil = File.Exists(local) ? local : OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe") : "/usr/bin/libreoffice";
        }
        using var termen = CancellationTokenSource.CreateLinkedTokenSource(ct);
        termen.CancelAfter(TimeSpan.FromSeconds(90));
        var intrat = false;
        string? director = null;
        try
        {
            await using var original = File.OpenRead(sursa);
            var cheie = tip + Convert.ToHexString(await SHA256.HashDataAsync(original, termen.Token)) + (foaie is null ? "" : "-" + foaie);
            var persistent = Path.Combine(DirectorCache, cheie + ".pdf");
            if (File.Exists(persistent))
            {
                var salvat = await File.ReadAllBytesAsync(persistent, termen.Token);
                if (salvat.Length <= 20 * 1024 * 1024 && salvat.AsSpan().StartsWith("%PDF-"u8)) return salvat;
                File.Delete(persistent);
            }
            if (!File.Exists(executabil)) throw new CerereInvalida("Previzualizarea documentelor nu este disponibilă momentan. Poți descărca fișierul pentru a-l deschide.");
            if (cache.TryGetValue<byte[]>(cheie, out var existent)) return existent!;
            await coada.WaitAsync(termen.Token);
            intrat = true;
            if (cache.TryGetValue<byte[]>(cheie, out existent)) return existent!;
            director = Directory.CreateTempSubdirectory("markai-preview-").FullName;
            var intrare = Path.Combine(director, "document." + tip);
            File.Copy(sursa, intrare);
            if (tip == "xlsx" && foaie is { } index) AlegeFoaia(intrare, index);
            var profil = Path.Combine(director, "profil");
            Directory.CreateDirectory(Path.Combine(profil, "user"));
            // Separate profile: never execute macros or refresh linked external content.
            await File.WriteAllTextAsync(Path.Combine(profil, "user", "registrymodifications.xcu"), """
                <?xml version="1.0" encoding="UTF-8"?>
                <oor:items xmlns:oor="http://openoffice.org/2001/registry">
                <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item>
                <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="DisableMacrosExecution" oor:op="fuse"><value>true</value></prop><prop oor:name="DisableActiveContent" oor:op="fuse"><value>true</value></prop><prop oor:name="BlockUntrustedRefererLinks" oor:op="fuse"><value>true</value></prop></item>
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
            Directory.CreateDirectory(DirectorCache);
            var partial = persistent + "." + Guid.NewGuid().ToString("N") + ".partial";
            try { await File.WriteAllBytesAsync(partial, date, termen.Token); File.Move(partial, persistent, true); }
            finally { File.Delete(partial); }
            // Bound the disk cache; originals are never included in this directory.
            var fisiere = new DirectoryInfo(DirectorCache).GetFiles("*.pdf").OrderByDescending(x => x.LastWriteTimeUtc).ToArray();
            long total = 0;
            foreach (var f in fisiere) { total += f.Length; if (total > 512L * 1024 * 1024 || f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30)) f.Delete(); }
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
