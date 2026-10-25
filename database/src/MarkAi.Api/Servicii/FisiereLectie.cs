using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Xml;
using System.Xml.Linq;
using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace MarkAi.Api.Servicii;

/// <summary>
/// The text of a Lecție file, for AI question generation: PDF through PdfPig, DOCX/PPTX by reading the text nodes of their
/// XML (no Office needed). Stops at <paramref name="maxCaractere"/>; scanned PDFs (images only) give no text.
/// </summary>
public static partial class ExtragereText
{
    private const string NsWord = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string NsDesen = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const int MaxPaginiPdf = 500;
    // Bounds the XML read from one ZIP entry, whatever size it claims, so a zip bomb stops early.
    private const long MaxCaractereXml = 50_000_000;

    public static string Extrage(string cale, string tip, int maxCaractere)
    {
        var text = new StringBuilder();
        switch (tip)
        {
            case FisierLectie.Pdf: Pdf(cale, text, maxCaractere); break;
            case FisierLectie.Docx: Office(cale, ["word/document.xml"], NsWord, "t", text, maxCaractere); break;
            case FisierLectie.Xlsx: Excel(cale, text, maxCaractere); break;
            case FisierLectie.Pptx:
                using (var zip = ZipFile.OpenRead(cale))
                {
                    var slide = zip.Entries.Select(e => (e.FullName, Nr: SlideNr().Match(e.FullName)))
                        .Where(x => x.Nr.Success).OrderBy(x => int.Parse(x.Nr.Groups[1].Value)).Select(x => x.FullName).ToArray();
                    Office(cale, slide, NsDesen, "t", text, maxCaractere);
                }
                break;
            default: throw new ArgumentException("Unknown file type " + tip);
        }
        return Curata(text.ToString(), maxCaractere);
    }

    private static void Pdf(string cale, StringBuilder text, int maxCaractere)
    {
        using var doc = PdfDocument.Open(cale);
        foreach (var pagina in doc.GetPages().Take(MaxPaginiPdf))
        {
            text.AppendJoin(' ', pagina.GetWords().Select(w => w.Text)).Append("\n\n");
            if (text.Length >= maxCaractere) return;
        }
    }

    // Cached cell values only: formulas, external links and macros are never executed.
    private static void Excel(string cale, StringBuilder text, int maxCaractere)
    {
        using var zip = ZipFile.OpenRead(cale);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument Litera(ZipArchiveEntry e)
        {
            using var reader = XmlReader.Create(e.Open(), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxCaractereXml });
            return XDocument.Load(reader);
        }
        var shared = zip.GetEntry("xl/sharedStrings.xml") is { } strings
            ? Litera(strings).Descendants(ns + "si").Select(s => string.Concat(s.Descendants(ns + "t").Select(t => t.Value))).ToArray()
            : [];
        foreach (var sheet in zip.Entries.Where(e => Regex.IsMatch(e.FullName, @"^xl/worksheets/sheet\d+\.xml$"))
                     .OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            foreach (var row in Litera(sheet).Descendants(ns + "row"))
            {
                foreach (var cell in row.Elements(ns + "c"))
                {
                    var value = cell.Element(ns + "v")?.Value ?? "";
                    var type = (string?)cell.Attribute("t");
                    if (type == "s") value = int.TryParse(value, out var n) && n >= 0 && n < shared.Length ? shared[n] : "";
                    else if (type == "inlineStr") value = string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
                    var remaining = maxCaractere - text.Length;
                    if (remaining <= 0) return;
                    text.Append(value.AsSpan(0, Math.Min(value.Length, remaining))).Append(' ');
                }
                text.Append('\n');
                if (text.Length >= maxCaractere) return;
            }
        }
    }

    // Text nodes in document order; a paragraph end becomes a new line, a tab or a line break a space or a new line.
    private static void Office(string cale, string[] intrari, string ns, string nodText, StringBuilder text, int maxCaractere)
    {
        using var zip = ZipFile.OpenRead(cale);
        var setari = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxCaractereXml };
        foreach (var nume in intrari)
        {
            if (zip.GetEntry(nume) is not { } intrare) continue;
            using var xml = XmlReader.Create(intrare.Open(), setari);
            var areNod = xml.Read();
            while (areNod && text.Length < maxCaractere)
            {
                if (xml.NamespaceURI == ns && xml.NodeType == XmlNodeType.Element && xml.LocalName == nodText && !xml.IsEmptyElement)
                {
                    // Moves to the node after the text element, which is looked at next (no extra Read).
                    text.Append(xml.ReadElementContentAsString());
                    areNod = !xml.EOF;
                    continue;
                }
                if (xml.NamespaceURI == ns && xml.NodeType == XmlNodeType.Element && xml.LocalName == "tab") text.Append(' ');
                else if (xml.NamespaceURI == ns && xml.NodeType == XmlNodeType.Element && xml.LocalName is "br" or "cr") text.Append('\n');
                else if (xml.NamespaceURI == ns && xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "p") text.Append('\n');
                areNod = xml.Read();
            }
            text.Append("\n\n");
        }
    }

    // No control characters (PostgreSQL refuses NUL), trimmed lines, at most one empty line in a row, then the cap.
    private static string Curata(string text, int maxCaractere)
    {
        var fara = new string(text.Where(c => c is '\n' or '\t' || !char.IsControl(c)).ToArray()).Replace('\t', ' ');
        var linii = fara.Split('\n').Select(l => SpatiiMultiple().Replace(l, " ").Trim());
        var rezultat = LiniiGoale().Replace(string.Join('\n', linii), "\n\n").Trim();
        return rezultat.Length <= maxCaractere ? rezultat : rezultat[..maxCaractere];
    }

    [GeneratedRegex(@"^ppt/slides/slide(\d+)\.xml$")] private static partial Regex SlideNr();
    [GeneratedRegex(@" {2,}")] private static partial Regex SpatiiMultiple();
    [GeneratedRegex(@"\n{3,}")] private static partial Regex LiniiGoale();
}

/// <summary>
/// Background work for Lecție files: extracts the text of each uploaded file (one at a time, with a time limit), and hourly
/// removes stored files that no database row points at (left by a crash between disk and database). Files still waiting
/// after a restart are picked up again at start. Questions are generated only when the professor asks (see <see cref="GeneratorIntrebari"/>).
/// </summary>
public sealed class ProcesorFisiere(IServiceScopeFactory scopes, Stocare stocare, IConfiguration cfg, TimeProvider ceas,
    ILogger<ProcesorFisiere> log) : BackgroundService
{
    private readonly Channel<int> _fisiere = Channel.CreateUnbounded<int>();
    private DateTime _ultimaCuratare = DateTime.MinValue;

    private int MaxCaractere => cfg.GetValue("Fisiere:MaxCaractereText", 60_000);
    private DateTime Acum => ceas.GetUtcNow().UtcDateTime;

    public void Extrage(int fisierId) => _fisiere.Writer.TryWrite(fisierId);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            using (var scope = scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
                foreach (var id in await db.Set<FisierLectie>().Where(f => f.StareText == FisierLectie.InAsteptare).Select(f => f.Id).ToListAsync(ct))
                    Extrage(id);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Could not reload the waiting Lecție files"); }

        while (!ct.IsCancellationRequested)
        {
            try { await RuleazaOdataAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Lecție file processing failed"); }
            await Task.WhenAny(_fisiere.Reader.WaitToReadAsync(ct).AsTask(), Task.Delay(TimeSpan.FromSeconds(5), ceas, ct));
        }
    }

    /// <summary>Extracts the files queued so far, then sweeps orphans when an hour has passed.</summary>
    public async Task RuleazaOdataAsync(CancellationToken ct)
    {
        while (_fisiere.Reader.TryRead(out var id)) await ExtrageAsync(id, ct);
        if (Acum - _ultimaCuratare >= TimeSpan.FromHours(1)) { _ultimaCuratare = Acum; await CurataOrfaneAsync(ct); }
    }

    private async Task ExtrageAsync(int fisierId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        var f = await db.Set<FisierLectie>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == fisierId && x.StareText == FisierLectie.InAsteptare, ct);
        if (f is null || stocare.CaleFisier(f.Cheie) is not { } cale) return; // deleted or replaced meanwhile

        string stare; string? text = null;
        try
        {
            // PdfPig cannot be cancelled; a file that takes too long is given up (its thread finishes on its own).
            text = await Task.Run(() => ExtragereText.Extrage(cale, f.Tip, MaxCaractere), ct).WaitAsync(TimeSpan.FromSeconds(60), ct);
            stare = text.Length >= 20 ? FisierLectie.Gata : FisierLectie.FaraText;
            if (stare == FisierLectie.FaraText) text = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Text extraction failed for Lecție file {Id} ({Tip})", f.Id, f.Tip);
            stare = FisierLectie.Eroare;
        }
        await db.Set<FisierLectie>().Where(x => x.Id == fisierId && x.StareText == FisierLectie.InAsteptare)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StareText, stare).SetProperty(x => x.TextExtras, text), ct);
        log.LogInformation("Lecție file {Id}: text {Stare} ({Caractere} characters)", f.Id, stare, text?.Length ?? 0);
    }

    private async Task CurataOrfaneAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        // Gradually share legacy uploads too; keep old copies until their references have committed.
        var vechiFisiere = await db.Set<FisierLectie>().Where(f => f.Sha256 != null && f.Cheie.Length < 60)
            .Select(f => new { f.Id, f.Cheie, f.Sha256, f.Tip }).Take(100).ToListAsync(ct);
        foreach (var f in vechiFisiere)
        {
            var noua = stocare.Consolideaza(f.Cheie, f.Sha256!, f.Tip);
            if (noua != f.Cheie) await db.Set<FisierLectie>().Where(x => x.Id == f.Id && x.Cheie == f.Cheie)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Cheie, noua), ct);
        }
        var cunoscute = (await db.Set<FisierLectie>().Select(f => f.Cheie).ToListAsync(ct)).ToHashSet();
        // An hour of margin: an upload in progress has its file on disk before its row is saved.
        var vechi = Acum - TimeSpan.FromHours(1);
        var sterse = 0;
        foreach (var (cheie, modificatLa) in stocare.Chei(Stocare.DirectorLectii).ToList())
        {
            if (cunoscute.Contains(cheie) || modificatLa > vechi) continue;
            stocare.Sterge(cheie, orfan: true, inainteDe: vechi);
            sterse++;
        }
        if (sterse > 0) log.LogInformation("Removed {Count} orphan Lecție files", sterse);
    }
}
