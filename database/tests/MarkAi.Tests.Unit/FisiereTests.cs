using MarkAi.Api.Endpoints;
using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using static MarkAi.Tests.Suport.DocumenteTest;

namespace MarkAi.Tests.Unit;

/// <summary>Stored files: names, content checks (signatures, macros, zip bombs), text extraction, paths that stay inside storage.</summary>
public sealed class FisiereTests : IDisposable
{
    private readonly string dir = Directory.CreateTempSubdirectory("markai-unit-").FullName;
    private readonly Stocare stocare;

    public FisiereTests()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Stocare:Director"] = dir, ["Fisiere:MaxMb"] = "2" }).Build();
        stocare = new Stocare(cfg, new Mediu(dir), NullLogger<Stocare>.Instance);
    }

    public void Dispose() => Directory.Delete(dir, true);

    private static IFormFile Formular(byte[] date, string nume) => new FormFile(new MemoryStream(date), 0, date.Length, "fisier", nume);

    private Task<FisierSalvat> Salveaza(byte[] date, string nume) => stocare.SalveazaFisierLectieAsync(Formular(date, nume), TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("../../etc/Curs: Eminescu*?.pdf", "pdf", "Curs Eminescu.pdf")]
    [InlineData("C:\\Users\\ana\\Tema 1.docx", "docx", "Tema 1.docx")]
    [InlineData("   .pdf", "pdf", "document.pdf")]
    [InlineData(null, "pdf", "document.pdf")]
    [InlineData("a<b>c|d\".pptx", "pptx", "a b c d.pptx")]
    [InlineData("...ascuns..pdf", "pdf", "ascuns.pdf")]
    [InlineData("Ștefan cel Mare.docx", "docx", "Ștefan cel Mare.docx")]
    public void NumeleFisieruluiEsteCuratat(string? original, string tip, string asteptat) => FisiereEndpoints.NumeCurat(original, tip).ShouldBe(asteptat);

    [Fact]
    public void NumeleLungEsteTaiatLa120() => FisiereEndpoints.NumeCurat(new string('a', 300) + ".pdf", "pdf").ShouldBe(new string('a', 120) + ".pdf");

    [Fact]
    public void ExtensiaEsteCeaReala() => FisiereEndpoints.NumeCurat("tema.exe", "pdf").ShouldBe("tema.pdf");

    [Fact]
    public async Task PdfulValidEsteSalvatCuShaSiMarime()
    {
        var pdf = Pdf();
        var f = await Salveaza(pdf, "a.pdf");
        f.Tip.ShouldBe("pdf");
        f.Marime.ShouldBe(pdf.Length);
        f.Sha256.ShouldBe(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(pdf)));
        f.Cheie.ShouldMatch(@"^lectii/[0-9a-f]{32}\.pdf$");
        File.ReadAllBytes(stocare.CaleFisier(f.Cheie)!).ShouldBe(pdf);
    }

    [Fact]
    public async Task DocxSiPptxValideSuntSalvate()
    {
        (await Salveaza(Docx(), "a.docx")).Tip.ShouldBe("docx");
        (await Salveaza(Pptx(), "a.pptx")).Tip.ShouldBe("pptx");
    }

    [Fact]
    public async Task ExcelEsteValidatSiTextulEsteExtras()
    {
        var f = await Salveaza(Xlsx(), "tabel.xlsx");
        f.Tip.ShouldBe("xlsx");
        var text = ExtragereText.Extrage(stocare.CaleFisier(f.Cheie)!, "xlsx", 10_000);
        text.ShouldContain("Ștefan în Excel");
        text.ShouldContain("42");
        text.ShouldContain("Valoare locală");
        text.ShouldContain("2");
        text.ShouldNotContain("1+1");
    }

    [Fact]
    public async Task ExcelCuMacroSauAltTipEsteRefuzat()
    {
        (await Should.ThrowAsync<CerereInvalida>(() => Salveaza(Xlsx(macro: true), "a.xlsx"))).Message.ShouldContain("macrocomenzi");
        await Should.ThrowAsync<CerereInvalida>(() => Salveaza(Docx(), "a.xlsx"));
        await Should.ThrowAsync<CerereInvalida>(() => Salveaza(ZipBomba(), "a.xlsx"));
    }

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("a.docx")]
    public async Task ExecutabilulEsteRefuzat(string nume) => await Should.ThrowAsync<CerereInvalida>(() => Salveaza(Exe, nume));

    [Fact]
    public async Task MacrocomenzileSuntRefuzate()
    {
        (await Should.ThrowAsync<CerereInvalida>(() => Salveaza(Pptx(macro: true), "a.pptx"))).Message.ShouldContain("macrocomenzi");
        (await Should.ThrowAsync<CerereInvalida>(() => Salveaza(Docx(macroEnabled: true), "a.docx"))).Message.ShouldContain("macrocomenzi");
        (await Should.ThrowAsync<CerereInvalida>(() => Salveaza(Pptx(activeX: true), "a.pptx"))).Message.ShouldContain("macrocomenzi");
    }

    [Fact]
    public async Task BombaZipEsteRefuzata() =>
        (await Should.ThrowAsync<CerereInvalida>(() => Salveaza(ZipBomba(), "a.docx"))).Message.ShouldContain("neobișnuit de mare");

    [Fact]
    public async Task ZipulFaraDocumentEsteRefuzat()
    {
        await Should.ThrowAsync<CerereInvalida>(() => Salveaza(ZipSimplu(), "a.pptx"));
        await Should.ThrowAsync<CerereInvalida>(() => Salveaza(Docx(faraDocument: true), "a.docx"));
        await Should.ThrowAsync<CerereInvalida>(() => Salveaza(ZipStricat(), "a.docx"));
    }

    [Fact]
    public async Task PesteLimitaEsteRefuzat() =>
        (await Should.ThrowAsync<CerereInvalida>(() => Salveaza(Pdf("x", umplutura: 2 * 1024 * 1024 + 1), "a.pdf"))).Message.ShouldContain("2 MB");

    [Fact]
    public async Task RefuzulNuLasaNimicPeDisc()
    {
        await Should.ThrowAsync<CerereInvalida>(() => Salveaza(Pptx(macro: true), "a.pptx"));
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("../afara.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("lectii/../../afara.pdf")]
    [InlineData("")]
    [InlineData(null)]
    public void CaleaInAfaraStocariiEsteRefuzata(string? cheie) => stocare.CaleFisier(cheie!).ShouldBeNull();

    [Fact]
    public void StergereaUneiCheiInvalideNuFaceNimic() => Should.NotThrow(() => stocare.Sterge("../../etc/passwd"));

    [Fact]
    public void ServireaUneiCheiInexistenteEste404() => stocare.Serveste("poze/nu-exista.png").ShouldBeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();

    [Fact]
    public void LimitaImplicitaEste20Mb() =>
        new Stocare(new ConfigurationBuilder().Build(), new Mediu(dir), NullLogger<Stocare>.Instance).MaxFisierLectieMb.ShouldBe(20);

    private string Scrie(byte[] date, string ext)
    {
        var cale = Path.Combine(dir, Guid.NewGuid().ToString("N") + ext);
        File.WriteAllBytes(cale, date);
        return cale;
    }

    [Fact]
    public void TextulPdf() => ExtragereText.Extrage(Scrie(Pdf(), ".pdf"), "pdf", 10_000).ShouldContain(FrazaPdf);

    [Fact]
    public void TextulDocxPastreazaParagrafeleSiDiacriticele()
    {
        var text = ExtragereText.Extrage(Scrie(Docx(), ".docx"), "docx", 10_000);
        text.ShouldBe("Primul paragraf.\n" + FrazaDocx + ".");
    }

    [Fact]
    public void TextulPptxUrmeazaNumarulDiapozitivelor()
    {
        var text = ExtragereText.Extrage(Scrie(Pptx(), ".pptx"), "pptx", 10_000);
        text.IndexOf(FrazaPptx, StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("zece", StringComparison.Ordinal));
    }

    [Fact]
    public void TextulEsteTaiatLaLimita() => ExtragereText.Extrage(Scrie(Docx(), ".docx"), "docx", 10).Length.ShouldBe(10);

    [Fact]
    public void TaburileSiSpatiileMultipleDevinUnSpatiu() =>
        ExtragereText.Extrage(Scrie(Docx("A\tB    C"), ".docx"), "docx", 10_000).ShouldEndWith("A B C.");

    [Fact]
    public void TipulNecunoscutEsteRefuzat() => Should.Throw<ArgumentException>(() => ExtragereText.Extrage(Scrie([1], ".txt"), "txt", 10));

    private sealed class Mediu(string radacina) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = radacina;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "teste";
        public string ContentRootPath { get; set; } = radacina;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
