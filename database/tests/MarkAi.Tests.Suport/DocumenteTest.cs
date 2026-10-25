using System.IO.Compression;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace MarkAi.Tests.Suport;

/// <summary>Small real documents built in memory: PDF (PdfPig), DOCX and PPTX (minimal Office packages), and hostile variants.</summary>
public static class DocumenteTest
{
    public const string FrazaPdf = "Fraza cunoscuta din PDF despre Mihai Eminescu";
    public const string FrazaDocx = "Ștefan cel Mare și țara Moldovei în DOCX";
    public const string FrazaPptx = "Diapozitivul doi despre Ion Creangă";

    public static readonly byte[] Exe = Encoding.ASCII.GetBytes("MZ\x90\0this program cannot be run in DOS mode").Concat(new byte[2000]).ToArray();

    /// <summary>A one-page PDF with a line of text (Helvetica, so ASCII only); padding makes it as large as needed.</summary>
    public static byte[] Pdf(string text = FrazaPdf, int umplutura = 0)
    {
        var b = new PdfDocumentBuilder();
        var font = b.AddStandard14Font(Standard14Font.Helvetica);
        b.AddPage(PageSize.A4).AddText(text, 12, new PdfPoint(25, 700), font);
        var bytes = b.Build();
        return umplutura <= bytes.Length ? bytes : bytes.Concat(new byte[umplutura - bytes.Length]).ToArray();
    }

    public static byte[] Zip(params (string Nume, byte[] Continut)[] intrari)
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

    private static byte[] U(string s) => Encoding.UTF8.GetBytes(s);

    private const string Rels = """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"/>""";

    public static byte[] Docx(string text = FrazaDocx, bool macroEnabled = false, bool faraDocument = false)
    {
        var intrari = new List<(string, byte[])>
        {
            ("[Content_Types].xml", U($"""<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.{(macroEnabled ? "ms-word.document.macroEnabled.main+xml" : "openxmlformats-officedocument.wordprocessingml.document.main+xml")}"/></Types>""")),
            ("_rels/.rels", U(Rels)),
        };
        if (!faraDocument)
            intrari.Add(("word/document.xml", U($"""<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Primul paragraf.</w:t></w:r></w:p><w:p><w:r><w:t xml:space="preserve">{text}</w:t></w:r><w:r><w:t>.</w:t></w:r></w:p></w:body></w:document>""")));
        return Zip([.. intrari]);
    }

    public static byte[] Pptx(bool macro = false, bool activeX = false)
    {
        static string Slide(string t) => $"""<?xml version="1.0" encoding="UTF-8"?><p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>{t}</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>""";
        var intrari = new List<(string, byte[])>
        {
            ("[Content_Types].xml", U("""<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="xml" ContentType="application/xml"/></Types>""")),
            ("_rels/.rels", U(Rels)),
            ("ppt/presentation.xml", U("""<?xml version="1.0" encoding="UTF-8"?><p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"/>""")),
            ("ppt/slides/slide10.xml", U(Slide("Diapozitivul zece, ultimul"))),
            ("ppt/slides/slide2.xml", U(Slide(FrazaPptx))),
        };
        if (macro) intrari.Add(("ppt/vbaProject.bin", new byte[512]));
        if (activeX) intrari.Add(("ppt/activeX/activeX1.xml", U("<ax/>")));
        return Zip([.. intrari]);
    }

    public static byte[] Xlsx(bool macro = false) => Zip(
        ("[Content_Types].xml", U("<Types/>")),
        ("xl/workbook.xml", U("<workbook/>")),
        ("xl/sharedStrings.xml", U("""<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><si><t>Ștefan în Excel</t></si></sst>""")),
        ("xl/worksheets/sheet1.xml", U("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="s"><v>0</v></c><c r="C1"><v>42</v></c><c r="D1" t="inlineStr"><is><t>Valoare locală</t></is></c><c r="E1"><f>1+1</f><v>2</v></c></row></sheetData></worksheet>""")),
        (macro ? "xl/vbaProject.bin" : "xl/styles.xml", macro ? new byte[50] : U("<styles/>")));

    /// <summary>60 MB of zeros compress to a few dozen KB: far over any real document's ratio.</summary>
    public static byte[] ZipBomba()
    {
        var docx = Docx();
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

    public static byte[] ZipSimplu() => Zip(("readme.txt", U("doar un zip")), ("[Content_Types].xml", U("<Types/>")));

    /// <summary>The ZIP signature followed by garbage: looks like Office from its first bytes, but cannot be opened.</summary>
    public static byte[] ZipStricat() => [.. "PK\x03\x04"u8.ToArray(), .. new byte[200]];
}
