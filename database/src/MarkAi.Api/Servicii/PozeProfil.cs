using MarkAi.Api.Infrastructura;
using SkiaSharp;

namespace MarkAi.Api.Servicii;

/// <summary>Bound decoded memory before allocation; render orientation and strip metadata.</summary>
internal static class PozeProfil
{
    private static readonly SemaphoreSlim Procesari = new(2, 2);
    internal static async Task<(byte[] Profil, byte[] Mica)> CreeazaAsync(byte[] sursa, CancellationToken ct)
    {
        await Procesari.WaitAsync(ct);
        try
        {
            using var stream = new SKMemoryStream(sursa);
            using var codec = SKCodec.Create(stream);
            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0
                || (long)codec.Info.Width * codec.Info.Height > 24_000_000)
                throw new CerereInvalida("Fotografia nu este validă sau depășește 24 de megapixeli.");
            using var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
                throw new CerereInvalida("Fotografia nu poate fi citită integral.");
            ct.ThrowIfCancellationRequested();
            return (Redimensioneaza(bitmap, codec.EncodedOrigin, 400), Redimensioneaza(bitmap, codec.EncodedOrigin, 96));
        }
        finally { Procesari.Release(); }
    }

    internal static byte[] Redimensioneaza(SKBitmap sursa, SKEncodedOrigin origine, int maxim)
    {
        var schimbate = origine is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var w = schimbate ? sursa.Height : sursa.Width;
        var h = schimbate ? sursa.Width : sursa.Height;
        var factor = Math.Min(1d, (double)maxim / Math.Max(w, h));
        var latime = Math.Max(1, (int)Math.Round(w * factor));
        var inaltime = Math.Max(1, (int)Math.Round(h * factor));
        using var destinatie = new SKBitmap(latime, inaltime, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(destinatie))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)latime / w, (float)inaltime / h);
            switch (origine)
            {
                case SKEncodedOrigin.TopRight: canvas.Translate(w, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: canvas.Translate(w, h); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: canvas.Translate(0, h); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: canvas.Translate(w, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(w, h); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: canvas.Translate(0, h); canvas.RotateDegrees(270); break;
            }
            using var imagine = SKImage.FromBitmap(sursa);
            canvas.DrawImage(imagine, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));
        }
        using var rezultat = SKImage.FromBitmap(destinatie);
        using var date = rezultat.Encode(SKEncodedImageFormat.Webp, 82);
        return date.ToArray();
    }
}
