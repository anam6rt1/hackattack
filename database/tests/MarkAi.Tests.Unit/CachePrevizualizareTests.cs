using System.Security.Cryptography;
using MarkAi.Api.Servicii;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarkAi.Tests.Unit;

public sealed class CachePrevizualizareTests : IDisposable
{
    private readonly string dir = Directory.CreateTempSubdirectory("markai-cache-test-").FullName;
    public void Dispose() => Directory.Delete(dir, true);
    private sealed class Mediu(string dir) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = dir;
        public string WebRootPath { get; set; } = dir;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    [Fact]
    public async Task CopiaPersistentaSupravietuiesteUneiInstanteNoiFaraConverter()
    {
        var ct = TestContext.Current.CancellationToken;
        var sursa = Path.Combine(dir, "document.docx");
        var date = "exemplu de original"u8.ToArray();
        await File.WriteAllBytesAsync(sursa, date, ct);
        var folder = Directory.CreateDirectory(Path.Combine(dir, "previzualizari-v2")).FullName;
        var pdf = MarkAi.Tests.Suport.DocumenteTest.Pdf();
        await File.WriteAllBytesAsync(Path.Combine(folder, "docx" + Convert.ToHexString(SHA256.HashData(date)) + ".pdf"), pdf, ct);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Stocare:Director"] = dir, ["Previzualizare:LibreOffice"] = Path.Combine(dir, "inexistent") }).Build();
        for (var i = 0; i < 2; i++)
        {
            using var converter = new PrevizualizareOffice(cfg, new Mediu(dir), NullLogger<PrevizualizareOffice>.Instance);
            (await converter.PdfAsync(sursa, "docx", ct)).ShouldBe(pdf);
        }
        (await File.ReadAllBytesAsync(sursa, ct)).ShouldBe(date);
    }
}
