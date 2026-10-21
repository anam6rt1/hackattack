using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MarkAi.Api.Servicii;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

/// <summary>
/// Lecție files: type checks from the content (renamed EXE, macros, zip bomb, a ZIP that is not Office), the configurable size
/// limit (413 with a message), one file per type with confirmed replacement, text extraction from PDF/DOCX/PPTX, no automatic
/// AI generation (only on request, with every chapter and file as material, within the per-account and platform question budget), who may download (outsiders get 404), inline PDF vs
/// downloaded Office files, and cleanup on disk (replacement, file and Lecție deletion, orphan sweep).
/// </summary>
static class VerificariFisiere
{
    public static readonly Dictionary<string, string?> Setari = new()
    {
        ["Limite:AutentificariPeMinut"] = "200", ["Limite:CereriPeMinut"] = "2000", ["Fisiere:MaxMb"] = "3",
        ["Limite:IntrebariAiPeZi"] = "5", ["Limite:IntrebariAiPeZiTotal"] = "7",
    };

    const string FrazaPdf = "Fraza cunoscuta din PDF despre Mihai Eminescu";
    const string FrazaDocx = "Ștefan cel Mare și țara Moldovei în DOCX";
    const string FrazaPptx = "Diapozitivul doi despre Ion Creangă";

    public static async Task Ruleaza(Harness h)
    {
        await h.Db(async db =>
        {
            var elena = new Utilizator { Nume = "Elena Elev", Username = "elena", Email = "elena@example.invalid", EmailConfirmatLa = DateTime.UtcNow };
            elena.ParolaHash = Parole.Hash(elena, Harness.ParolaTest);
            elena.Roluri.Add(new UtilizatorRol { RolId = await db.Roluri.Where(r => r.Nume == "elev").Select(r => r.Id).SingleAsync() });
            db.Add(elena);
            await db.SaveChangesAsync();
            db.Add(new MembruOrganizatie { OrganizatieId = Harness.IdLiceu, UtilizatorId = elena.Id, Rol = MembruOrganizatie.RolElev,
                Stare = MembruOrganizatie.Aprobat, CerutLa = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });
        using var irina = await h.Login("irina");       // author, professor in Liceul de Test
        using var director = await h.Login("director"); // admin of Liceul de Test
        using var elena = await h.Login("elena");       // student in Liceul de Test
        using var george = await h.Login("george");     // student in no organisation
        var procesor = h.Serviciu<ProcesorFisiere>();
        var stocare = h.Serviciu<Stocare>();
        await procesor.RuleazaOdataAsync(CancellationToken.None); // the first run also sweeps orphans; nothing is stored yet
        h.Ai.Raspunde = (_, _, _) => """{"grila":[{"text":"Cine?","variante":["A","B","C","D"],"corect":0,"explicatie":"."}],"estimari":[]}""";

        var lectie = await Id(await irina.PostAsJsonAsync("/parcursuri", new { materieId = 1, titlu = "Lecție cu fișiere", clasa = "XII",
            vizibilitate = "organizatie", organizatieId = Harness.IdLiceu }));
        await Capitol(irina, lectie, "Capitol cu fișiere");

        // Refused from the content, whatever the extension says.
        var exe = Encoding.ASCII.GetBytes("MZ\x90\0this program cannot be run in DOS mode").Concat(new byte[2000]).ToArray();
        h.Check((await Incarca(irina, lectie, exe, "tema.pdf")).StatusCode == HttpStatusCode.BadRequest, "a renamed EXE (.pdf) is refused");
        h.Check((await Incarca(irina, lectie, exe, "tema.docx")).StatusCode == HttpStatusCode.BadRequest, "a renamed EXE (.docx) is refused");
        h.Check((await Incarca(irina, lectie, exe, "tema.exe")).StatusCode == HttpStatusCode.BadRequest, "an .exe is refused");
        h.Check((await Incarca(irina, lectie, Docx(FrazaDocx), "tema.docm")).StatusCode == HttpStatusCode.BadRequest, "a .docm (macro type) is refused");
        h.Check((await Incarca(irina, lectie, Pptx(macro: true), "prezentare.pptx")).StatusCode == HttpStatusCode.BadRequest,
            "a PPTX with a VBA project is refused");
        h.Check((await Incarca(irina, lectie, Docx(FrazaDocx, macroEnabled: true), "tema.docx")).StatusCode == HttpStatusCode.BadRequest,
            "a macro-enabled DOCX renamed to .docx is refused");
        h.Check((await Incarca(irina, lectie, ZipBomba(), "bomba.docx")).StatusCode == HttpStatusCode.BadRequest, "a zip bomb is refused");
        h.Check((await Incarca(irina, lectie, ZipSimplu(), "arhiva.pptx")).StatusCode == HttpStatusCode.BadRequest, "a ZIP that is not a presentation is refused");
        // The limit comes from Fisiere:MaxMb (3 MB in this group). Over it: a 413 with a message, before the body is read.
        var preaMare = await Incarca(irina, lectie, Pdf("x", umplutura: 5 * 1024 * 1024), "mare.pdf");
        h.Check(preaMare.StatusCode == HttpStatusCode.RequestEntityTooLarge && (await preaMare.Content.ReadAsStringAsync()).Contains("cel mult 3 MB"),
            "a file over the configured limit gets a 413 with a clear message");
        var limita = await Incarca(irina, lectie, Pdf("x", umplutura: 3 * 1024 * 1024 + 100_000), "limita.pdf");
        h.Check(limita.StatusCode == HttpStatusCode.BadRequest && (await limita.Content.ReadAsStringAsync()).Contains("cel mult 3 MB"),
            "a file just over the limit (within the request margin) is refused by size");
        h.Check(await h.Db(db => db.Set<FisierLectie>().CountAsync()) == 0 && !stocare.Chei(Stocare.DirectorLectii).Any(),
            "refused files leave no row and nothing on disk");

        // Who may upload.
        h.Check((await Incarca(george, lectie, Pdf(FrazaPdf), "a.pdf")).StatusCode == HttpStatusCode.Forbidden, "a student cannot upload");
        h.Check((await Incarca(h.Anonim(), lectie, Pdf(FrazaPdf), "a.pdf")).StatusCode == HttpStatusCode.Unauthorized, "anonymous cannot upload");

        // Three real documents, one per type.
        var octetiPdf = Pdf(FrazaPdf);
        var rPdf = await Incarca(irina, lectie, octetiPdf, "../../etc/Curs: Eminescu*?.pdf");
        h.Check(rPdf.StatusCode == HttpStatusCode.Created, "the author uploads a PDF");
        var pdf = await rPdf.Content.ReadFromJsonAsync<JsonElement>();
        h.Check(pdf.GetProperty("nume").GetString() == "Curs Eminescu.pdf",
            "the file name loses its folders and reserved characters");
        h.Check(pdf.GetProperty("stareText").GetString() == "in_asteptare", "text extraction starts in the background");
        var pdfId = pdf.GetProperty("id").GetInt32();
        var docxId = await Id(await Incarca(director, lectie, Docx(FrazaDocx), "Ștefan.docx"));
        h.Check(docxId > 0, "the home organisation's admin uploads a DOCX");
        var pptxId = await Id(await Incarca(irina, lectie, Pptx(), "prezentare.pptx"));
        var sha = await h.Db(db => db.Set<FisierLectie>().Where(f => f.Id == pdfId).Select(f => f.Sha256).SingleAsync());
        h.Check(sha == Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(octetiPdf)), "the SHA-256 of the content is stored");

        // One file per type: a second PDF is refused unless the editor confirms the replacement.
        var alDoilea = await Incarca(irina, lectie, Pdf("Alt PDF cu destul text pentru extragere"), "alt.pdf");
        h.Check(alDoilea.StatusCode == HttpStatusCode.Conflict && (await alDoilea.Content.ReadAsStringAsync()).Contains("deja un fișier PDF"),
            "a second PDF without confirmation is refused");
        h.Check(stocare.Chei(Stocare.DirectorLectii).Count() == 3, "the refused second PDF is not kept on disk");

        // Extraction only; nothing is generated until the professor asks.
        h.Ai.Apeluri.Clear();
        await procesor.RuleazaOdataAsync(CancellationToken.None);
        var texte = await h.Db(db => db.Set<FisierLectie>().Where(f => f.ParcursId == lectie).ToDictionaryAsync(f => f.Id, f => (f.StareText, f.TextExtras)));
        h.Check(texte[pdfId].StareText == "gata" && texte[pdfId].TextExtras!.Contains(FrazaPdf), "PDF text is extracted");
        h.Check(texte[docxId].StareText == "gata" && texte[docxId].TextExtras!.Contains(FrazaDocx), "DOCX text is extracted (with diacritics)");
        h.Check(texte[pptxId].StareText == "gata" && texte[pptxId].TextExtras!.Contains(FrazaPptx)
            && texte[pptxId].TextExtras!.IndexOf("doi", StringComparison.Ordinal) < texte[pptxId].TextExtras!.IndexOf("zece", StringComparison.Ordinal),
            "PPTX text is extracted, slides in number order (2 before 10)");
        var editor = await irina.GetFromJsonAsync<JsonElement>($"/continut/parcursuri/{lectie}/fisiere");
        h.Check(editor.GetProperty("maxMb").GetInt32() == 3 && editor.GetProperty("elemente").EnumerateArray().All(f => f.GetProperty("caractereText").GetInt32() > 0),
            "the editor sees the extracted text sizes and the configured limit");
        h.Ceas.Advance(TimeSpan.FromMinutes(5));
        await procesor.RuleazaOdataAsync(CancellationToken.None);
        await h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(CancellationToken.None);
        h.Check(await h.Db(db => db.Set<GenerareAi>().CountAsync(g => g.ParcursId == lectie)) == 0 && h.Ai.Apeluri.IsEmpty,
            "uploading files generates no questions by itself");
        await irina.PostAsJsonAsync($"/parcursuri/{lectie}/genereaza-intrebari", new { numar = 3 });
        await h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(CancellationToken.None);
        var prompt = string.Join("\n", h.Ai.Apeluri.Where(a => a.Scop == ScopAi.Intrebari).SelectMany(a => a.Mesaje).Select(m => m.Text));
        h.Check(prompt.Contains(FrazaPdf) && prompt.Contains(FrazaDocx) && prompt.Contains(FrazaPptx) && prompt.Contains("# Fișier: prezentare.pptx")
            && prompt.Contains("Capitol cu fișiere") && prompt.Contains("Scrie 3 întrebări grilă"), "the AI material has every chapter and file, and the number asked");

        // The AI budget counts questions asked, per account (5 here) and for the whole platform (7 here); admins count too.
        var buget = await irina.GetFromJsonAsync<JsonElement>("/continut/buget-ai");
        h.Check(buget.GetProperty("ramase").GetInt32() == 2 && buget.GetProperty("peZi").GetInt32() == 5, "the professor sees the AI questions they have left");
        var pestePropriu = await irina.PostAsJsonAsync($"/parcursuri/{lectie}/genereaza-intrebari", new { numar = 3 });
        h.Check(pestePropriu.StatusCode == HttpStatusCode.Conflict && (await pestePropriu.Content.ReadAsStringAsync()).Contains("Îți mai rămân 2"),
            "asking for more questions than the personal budget is refused, with what is left");
        var pestePlatforma = await director.PostAsJsonAsync($"/parcursuri/{lectie}/genereaza-intrebari", new { numar = 5 });
        h.Check(pestePlatforma.StatusCode == HttpStatusCode.Conflict && (await pestePlatforma.Content.ReadAsStringAsync()).Contains("Îți mai rămân 4"),
            "another account is capped by what the platform has left");
        h.Check((await director.PostAsJsonAsync($"/parcursuri/{lectie}/genereaza-intrebari", new { numar = 4 })).StatusCode == HttpStatusCode.Accepted,
            "a request within the budget is accepted");
        await h.Serviciu<GeneratorIntrebari>().RuleazaOdataAsync(CancellationToken.None);
        buget = await irina.GetFromJsonAsync<JsonElement>("/continut/buget-ai");
        var dupa = await irina.PostAsJsonAsync($"/parcursuri/{lectie}/genereaza-intrebari", new { numar = 1 });
        h.Check(buget.GetProperty("ramase").GetInt32() == 0 && dupa.StatusCode == HttpStatusCode.Conflict,
            "once the platform budget is spent, nobody can generate more");

        // Reading: members of the organisation see the files, outsiders do not even learn they exist.
        var lista = await elena.GetFromJsonAsync<JsonElement>($"/parcursuri/{lectie}/fisiere");
        h.Check(lista.GetProperty("total").GetInt32() == 3 && !lista.GetRawText().Contains("cheie") && !lista.GetRawText().Contains("lectii/"),
            "a student of the organisation lists the files (no storage keys)");
        var inline = await elena.GetAsync($"/fisiere/{pdfId}");
        h.Check(inline.StatusCode == HttpStatusCode.OK && inline.Content.Headers.ContentType?.MediaType == "application/pdf"
            && inline.Content.Headers.ContentDisposition?.DispositionType == "inline", "a PDF opens in the browser");
        var csp = inline.Headers.GetValues("Content-Security-Policy").Single();
        h.Check(csp.Contains("default-src 'none'") && csp.Contains("frame-ancestors 'none'") && inline.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff",
            "an inline PDF has a strict CSP and nosniff");
        h.Check((await inline.Content.ReadAsByteArrayAsync()).SequenceEqual(octetiPdf), "the downloaded bytes are the uploaded ones");
        var descarcat = await elena.GetAsync($"/fisiere/{pdfId}?descarca=true");
        h.Check(descarcat.Content.Headers.ContentDisposition?.DispositionType == "attachment", "?descarca=true downloads the PDF");
        var docx = await elena.GetAsync($"/fisiere/{docxId}");
        h.Check(docx.Content.Headers.ContentDisposition?.DispositionType == "attachment" && docx.Content.Headers.ContentDisposition?.FileNameStar == "Ștefan.docx"
            && docx.Headers.GetValues("Content-Security-Policy").Single().Contains("sandbox"), "a DOCX is always downloaded, under its name, sandboxed");
        h.Check((await george.GetAsync($"/fisiere/{pdfId}")).StatusCode == HttpStatusCode.NotFound, "an outsider downloading an organisation-only file gets 404");
        h.Check((await george.GetAsync($"/parcursuri/{lectie}/fisiere")).StatusCode == HttpStatusCode.NotFound, "an outsider cannot list them");
        h.Check((await h.Anonim().GetAsync($"/fisiere/{pdfId}")).StatusCode == HttpStatusCode.Unauthorized, "anonymous cannot download");
        h.Check((await elena.GetAsync("/fisiere/999999")).StatusCode == HttpStatusCode.NotFound, "an unknown file is 404");
        h.Check((await elena.DeleteAsync($"/fisiere/{pdfId}")).StatusCode == HttpStatusCode.Forbidden, "a student cannot delete a file");
        h.Check((await george.GetAsync($"/continut/parcursuri/{lectie}/fisiere")).StatusCode == HttpStatusCode.Forbidden, "a student has no editor list");

        // Replacing: with confirmation the new PDF takes the old one's place (old row and stored file gone); near the limit passes.
        var cheiePdf = await Cheie(h, pdfId);
        var inlocuit = await Incarca(irina, lectie, Pdf("x", umplutura: 3 * 1024 * 1024 - 2000), "mare.pdf", inlocuieste: true);
        h.Check(inlocuit.StatusCode == HttpStatusCode.Created, "a file of almost 3 MB replaces the PDF after confirmation");
        var nouId = (await inlocuit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        h.Check(!File.Exists(stocare.CaleFisier(cheiePdf)) && await h.Db(db => db.Set<FisierLectie>().CountAsync(f => f.ParcursId == lectie && f.Tip == "pdf")) == 1
            && (await elena.GetAsync($"/fisiere/{pdfId}")).StatusCode == HttpStatusCode.NotFound, "the replaced PDF is gone from disk and database");
        await procesor.RuleazaOdataAsync(CancellationToken.None);
        h.Check(await h.Db(db => db.Set<FisierLectie>().Where(f => f.Id == nouId).Select(f => f.StareText).SingleAsync()) is "eroare" or "fara_text",
            "an unreadable PDF is kept for students, marked as having no text");

        // Deleting a file removes it from disk; nothing is regenerated.
        var cheieNou = await Cheie(h, nouId);
        h.Check((await irina.DeleteAsync($"/fisiere/{nouId}")).StatusCode == HttpStatusCode.NoContent && !File.Exists(stocare.CaleFisier(cheieNou)),
            "deleting a file removes it from disk");
        h.Check((await elena.GetAsync($"/fisiere/{nouId}")).StatusCode == HttpStatusCode.NotFound, "a deleted file is gone");

        // Deleting a chapter keeps the Lecție's files; deleting the Lecție removes them.
        var capitole = await irina.GetFromJsonAsync<JsonElement>($"/continut/cursuri?parcursId={lectie}");
        await irina.DeleteAsync($"/cursuri/{capitole.GetProperty("elemente")[0].GetProperty("id").GetInt32()}");
        h.Check(await h.Db(db => db.Set<FisierLectie>().CountAsync(f => f.ParcursId == lectie)) == 2, "deleting a chapter keeps the Lecție's files");
        var cheiDocx = await Cheie(h, docxId);
        h.Check((await irina.DeleteAsync($"/parcursuri/{lectie}")).StatusCode == HttpStatusCode.NoContent && !File.Exists(stocare.CaleFisier(cheiDocx))
            && !stocare.Chei(Stocare.DirectorLectii).Any() && await h.Db(db => db.Set<FisierLectie>().CountAsync()) == 0,
            "deleting a Lecție removes its files from disk and database");

        // Orphan sweep: a file no row points at goes once it is over an hour old; a fresh one (upload in progress) stays.
        var vechi = stocare.CaleFisier($"{Stocare.DirectorLectii}/orfan-vechi.pdf")!;
        var nou = stocare.CaleFisier($"{Stocare.DirectorLectii}/orfan-nou.pdf")!;
        Directory.CreateDirectory(Path.GetDirectoryName(vechi)!);
        await File.WriteAllBytesAsync(vechi, Pdf("orfan")); File.SetLastWriteTimeUtc(vechi, h.Ceas.GetUtcNow().UtcDateTime.AddHours(-3));
        await File.WriteAllBytesAsync(nou, Pdf("orfan")); File.SetLastWriteTimeUtc(nou, h.Ceas.GetUtcNow().UtcDateTime.AddHours(1));
        h.Ceas.Advance(TimeSpan.FromHours(1));
        await procesor.RuleazaOdataAsync(CancellationToken.None);
        h.Check(!File.Exists(vechi) && File.Exists(nou), "the orphan sweep removes old unreferenced files only");
    }

    static async Task<HttpResponseMessage> Incarca(HttpClient c, int parcursId, byte[] continut, string nume, bool inlocuieste = false)
    {
        using var form = new MultipartFormDataContent();
        var fisier = new ByteArrayContent(continut);
        fisier.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fisier, "fisier", nume);
        return await c.PostAsync($"/parcursuri/{parcursId}/fisiere{(inlocuieste ? "?inlocuieste=true" : "")}", form);
    }

    static Task<string> Cheie(Harness h, int fisierId) => h.Db(db => db.Set<FisierLectie>().Where(f => f.Id == fisierId).Select(f => f.Cheie).SingleAsync());

    static async Task<int> Id(HttpResponseMessage r)
    {
        if (!r.IsSuccessStatusCode) throw new Exception($"FAIL: request returned {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    static async Task<int> Capitol(HttpClient editor, int parcursId, string titlu) =>
        await Id(await editor.PostAsJsonAsync("/cursuri", new { parcursId, titlu, materiale = new[] { new { titlu = "Notițe", text = "Material de test." } } }));

    // A one-page PDF with a line of text (Helvetica, so ASCII only); padding makes it as large as needed (after %%EOF, still a PDF signature).
    static byte[] Pdf(string text, int umplutura = 0)
    {
        var b = new PdfDocumentBuilder();
        var font = b.AddStandard14Font(Standard14Font.Helvetica);
        b.AddPage(PageSize.A4).AddText(text, 12, new PdfPoint(25, 700), font);
        var bytes = b.Build();
        return umplutura <= bytes.Length ? bytes : bytes.Concat(new byte[umplutura - bytes.Length]).ToArray();
    }

    static byte[] Zip(params (string Nume, byte[] Continut)[] intrari)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (nume, continut) in intrari)
            {
                using var s = zip.CreateEntry(nume, CompressionLevel.Optimal).Open();
                s.Write(continut);
            }
        return ms.ToArray();
    }

    static byte[] U(string s) => Encoding.UTF8.GetBytes(s);

    const string Rels = """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>""";

    static byte[] Docx(string text, bool macroEnabled = false) => Zip(
        ("[Content_Types].xml", U($"""<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.{(macroEnabled ? "ms-word.document.macroEnabled.main+xml" : "openxmlformats-officedocument.wordprocessingml.document.main+xml")}"/></Types>""")),
        ("_rels/.rels", U(Rels)),
        ("word/document.xml", U($"""<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Primul paragraf.</w:t></w:r></w:p><w:p><w:r><w:t xml:space="preserve">{text}</w:t></w:r><w:r><w:t>.</w:t></w:r></w:p></w:body></w:document>""")));

    static byte[] Pptx(bool macro = false)
    {
        string Slide(string t) => $"""<?xml version="1.0" encoding="UTF-8"?><p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>{t}</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>""";
        var intrari = new List<(string, byte[])>
        {
            ("[Content_Types].xml", U("""<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="xml" ContentType="application/xml"/></Types>""")),
            ("_rels/.rels", U(Rels)),
            ("ppt/presentation.xml", U("""<?xml version="1.0" encoding="UTF-8"?><p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"/>""")),
            ("ppt/slides/slide10.xml", U(Slide("Diapozitivul zece, ultimul"))),
            ("ppt/slides/slide2.xml", U(Slide(FrazaPptx))),
        };
        if (macro) intrari.Add(("ppt/vbaProject.bin", new byte[512]));
        return Zip([.. intrari]);
    }

    // 60 MB of zeros compress to a few dozen KB: far over any real document's ratio.
    static byte[] ZipBomba()
    {
        var docx = Docx(FrazaDocx);
        using var ms = new MemoryStream();
        ms.Write(docx);
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            using var s = zip.CreateEntry("word/media/zero.bin", CompressionLevel.Optimal).Open();
            var zero = new byte[1024 * 1024];
            for (var i = 0; i < 60; i++) s.Write(zero);
        }
        return ms.ToArray();
    }

    static byte[] ZipSimplu() => Zip(("readme.txt", U("doar un zip")), ("[Content_Types].xml", U("<Types/>")));
}
