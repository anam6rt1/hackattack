using Microsoft.Extensions.Caching.Memory;

namespace MarkAi.Api.Infrastructura;

// Only public, identity-independent JSON. Subject counts depend on access, so authenticated /materii is excluded.
public sealed class CacheReferinte : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 512 });
    private long versiune;
    public async Task Ruleaza(HttpContext h, RequestDelegate next)
    {
        var path = h.Request.Path.Value;
        if (!HttpMethods.IsGet(h.Request.Method))
        {
            await next(h);
            if (h.Response.StatusCode < 300 && new[] { "/materii", "/organizatii", "/organizatia-mea", "/admin/organizatii", "/parcursuri", "/cursuri" }.Any(p => h.Request.Path.StartsWithSegments(p))) Interlocked.Increment(ref versiune);
            return;
        }
        var eligibil = path is "/judete" or "/organizatii" || path == "/materii" && h.User.Identity?.IsAuthenticated != true;
        if (!eligibil || h.Request.QueryString.Value?.Length > 512) { await next(h); return; }
        var key = $"{Interlocked.Read(ref versiune)}:{path}:{h.Request.QueryString}";
        if (cache.TryGetValue<byte[]>(key, out var bytes))
        {
            h.Response.ContentType = "application/json; charset=utf-8"; await h.Response.Body.WriteAsync(bytes!, h.RequestAborted); return;
        }
        var original = h.Response.Body;
        await using var buffer = new MemoryStream(); h.Response.Body = buffer;
        try
        {
            await next(h);
            bytes = buffer.ToArray();
            if (h.Response.StatusCode == 200 && h.Response.ContentType?.StartsWith("application/json") == true && bytes.Length < 256000)
                cache.Set(key, bytes, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2), Size = 1 });
        }
        finally { h.Response.Body = original; }
        buffer.Position = 0; await buffer.CopyToAsync(original, h.RequestAborted);
    }
    public void Dispose() => cache.Dispose();
}
