using System.IO.Compression;
using System.Net;
using System.Text.Json;
using MarkAi.Tests.Suport;
using SkiaSharp;

namespace MarkAi.Tests.Integration.Conturi;

public class OptimizareRaspunsuriTests : TestCuHarness
{
    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task JsonComprimatSeDecodificaIdentic(string format)
    {
        using var client = h.Anonim();
        var normal = await client.GetByteArrayAsync("/materii", Ct);
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd(format);
        var r = await client.GetAsync("/materii", Ct);
        r.Content.Headers.ContentEncoding.ShouldContain(format);
        r.Headers.Vary.ShouldContain("Accept-Encoding");
        var comprimat = await r.Content.ReadAsByteArrayAsync(Ct);
        comprimat.Length.ShouldBeLessThan(normal.Length);
        using var sursa = new MemoryStream(comprimat);
        using Stream decomprimare = format == "br" ? new BrotliStream(sursa, CompressionMode.Decompress) : new GZipStream(sursa, CompressionMode.Decompress);
        using var destinatie = new MemoryStream();
        await decomprimare.CopyToAsync(destinatie, Ct);
        destinatie.ToArray().ShouldBe(normal);
    }

    [Fact]
    public async Task FaraNegociereSiDatePersonaleRamanNecomprimate()
    {
        using var anonim = h.Anonim();
        (await anonim.GetAsync("/materii", Ct)).Content.Headers.ContentEncoding.ShouldBeEmpty();
        anonim.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br;q=0, gzip;q=0");
        (await anonim.GetAsync("/materii", Ct)).Content.Headers.ContentEncoding.ShouldBeEmpty();
        using var elev = await h.Login("george");
        elev.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br, gzip");
        var export = await elev.Post("/utilizatori/me/date/export", new { parolaActuala = Harness.ParolaTest });
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        export.Content.Headers.ContentEncoding.ShouldBeEmpty();
        (await elev.GetAsync("/utilizatori/me", Ct)).Content.Headers.ContentEncoding.ShouldBeEmpty();
    }

    [Fact]
    public async Task PozeleSuntWebpMiciCuAceleasiReguliDeAcces()
    {
        using var profesor = await h.Login("irina");
        var id = (await profesor.GetJson("/utilizatori/me")).Int("id");
        using var imagine = new SKBitmap(1200, 800);
        imagine.Erase(SKColors.DarkCyan);
        using var date = imagine.Encode(SKEncodedImageFormat.Png, 100);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(date.ToArray()), "Poza", "poza.png");
        (await profesor.PostAsync("/utilizatori/me/setari/documente", form, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var anonim = h.Anonim();
        anonim.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br, gzip");
        var r = await anonim.GetAsync($"/profesori/{id}/poza?marime=mica", Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        r.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        r.Content.Headers.ContentEncoding.ShouldBeEmpty();
        using var mica = SKBitmap.Decode(await r.Content.ReadAsByteArrayAsync(Ct));
        mica.Width.ShouldBe(96);
        using var profil = SKBitmap.Decode(await anonim.GetByteArrayAsync($"/profesori/{id}/poza", Ct));
        profil.Width.ShouldBe(400);
        await h.Db(async db => { var p = await db.ProfiluriMentor.FindAsync([id], Ct); p!.StareVerificare = "in_asteptare"; await db.SaveChangesAsync(Ct); });
        (await anonim.GetAsync($"/profesori/{id}/poza?marime=mica", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await profesor.GetAsync($"/profesori/{id}/poza?marime=mica", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
