using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MarkAi.Tests.Integration;

/// <summary>Short forms for the HTTP calls and checks every test makes.</summary>
public static class Raspunsuri
{
    public static async Task<JsonElement> Json(this HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken));

    /// <summary>Checks the status code, showing the body when it differs (the API's message says why).</summary>
    public static async Task<HttpResponseMessage> Status(this Task<HttpResponseMessage> cerere, HttpStatusCode asteptat)
    {
        var r = await cerere;
        if (r.StatusCode != asteptat)
            throw new ShouldAssertException($"Expected {(int)asteptat} {asteptat}, got {(int)r.StatusCode} {r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return r;
    }

    public static async Task<JsonElement> Json(this Task<HttpResponseMessage> cerere, HttpStatusCode asteptat = HttpStatusCode.OK) =>
        await (await cerere.Status(asteptat)).Json();

    /// <summary>The user-facing message of an error response.</summary>
    public static async Task<string> Mesaj(this Task<HttpResponseMessage> cerere, HttpStatusCode asteptat) =>
        (await cerere.Json(asteptat)).GetProperty("mesaj").GetString()!;

    public static Task<HttpResponseMessage> Post(this HttpClient c, string url, object? corp = null) =>
        corp is null ? c.PostAsync(url, null, TestContext.Current.CancellationToken) : c.PostAsJsonAsync(url, corp, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> Put(this HttpClient c, string url, object corp) =>
        c.PutAsJsonAsync(url, corp, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> Patch(this HttpClient c, string url, object corp) =>
        c.PatchAsJsonAsync(url, corp, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> Get(this HttpClient c, string url) => c.GetAsync(url, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> Delete(this HttpClient c, string url) => c.DeleteAsync(url, TestContext.Current.CancellationToken);

    public static async Task<JsonElement> GetJson(this HttpClient c, string url) => await c.Get(url).Json();

    public static string Str(this JsonElement e, string proprietate) => e.GetProperty(proprietate).GetString()!;
    public static int Int(this JsonElement e, string proprietate) => e.GetProperty(proprietate).GetInt32();
    public static bool Bool(this JsonElement e, string proprietate) => e.GetProperty(proprietate).GetBoolean();
    public static List<JsonElement> Lista(this JsonElement e, string proprietate = "elemente") => e.GetProperty(proprietate).EnumerateArray().ToList();
    public static bool EsteNull(this JsonElement e, string proprietate) => e.GetProperty(proprietate).ValueKind == JsonValueKind.Null;

    /// <summary>A file part for multipart uploads.</summary>
    public static ByteArrayContent Fisier(byte[] date, string tip = "application/octet-stream")
    {
        var c = new ByteArrayContent(date);
        c.Headers.ContentType = new MediaTypeHeaderValue(tip);
        return c;
    }

    public static readonly byte[] Png = CreeazaPng();
    private static byte[] CreeazaPng()
    {
        using var imagine = new SkiaSharp.SKBitmap(2, 2);
        imagine.Erase(SkiaSharp.SKColors.CornflowerBlue);
        using var date = imagine.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return date.ToArray();
    }
    public static readonly byte[] Pdf = "%PDF-1.4\n"u8.ToArray();
}
