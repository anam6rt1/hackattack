using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace MarkAi.Api.Infrastructura;

/// <summary>
/// Lists that change rarely and are the same for everyone (counties, localities, the public organisations; the subjects for
/// anonymous visitors) are kept in memory for a short while: a burst of identical requests runs the query once (the others wait
/// for it) instead of each taking a database connection. Any successful change to what they show empties the cache at once.
/// </summary>
public static class CacheListe
{
    public const string Tag = "liste";

    /// <summary>For everyone, signed in or not: the response does not depend on who asks.</summary>
    public static IOutputCachePolicy PentruToti(TimeSpan durata) => new Politica(durata, doarAnonim: false);

    /// <summary>Only for anonymous visitors: signed-in users see their own counts, so they always get a fresh answer.</summary>
    public static IOutputCachePolicy DoarAnonim(TimeSpan durata) => new Politica(durata, doarAnonim: true);

    // Changes under these paths can alter a cached list (subjects and their counts, Lecții, organisations); the quiz answers cannot.
    private static readonly string[] Schimbari = ["/materii", "/parcursuri", "/cursuri", "/admin", "/organizatia-mea"];

    public static void Foloseste(WebApplication app)
    {
        app.UseOutputCache();
        app.Use(async (ctx, next) =>
        {
            await next();
            var r = ctx.Request;
            if (HttpMethods.IsGet(r.Method) || HttpMethods.IsHead(r.Method) || HttpMethods.IsOptions(r.Method) || ctx.Response.StatusCode >= 400) return;
            var schimba = Schimbari.Any(p => r.Path.StartsWithSegments(p)) && !r.Path.Value!.Contains("/quiz")
                || (HttpMethods.IsDelete(r.Method) && r.Path.Equals("/utilizatori/me")); // a deleted account takes its organisation and Lecții
            if (schimba) await ctx.RequestServices.GetRequiredService<IOutputCacheStore>().EvictByTagAsync(Tag, CancellationToken.None);
        });
    }

    private sealed class Politica(TimeSpan durata, bool doarAnonim) : IOutputCachePolicy
    {
        public ValueTask CacheRequestAsync(OutputCacheContext c, CancellationToken ct)
        {
            var r = c.HttpContext.Request;
            var se = (HttpMethods.IsGet(r.Method) || HttpMethods.IsHead(r.Method)) && !(doarAnonim && c.HttpContext.User.Identity?.IsAuthenticated == true);
            c.EnableOutputCaching = true;
            c.AllowCacheLookup = c.AllowCacheStorage = se;
            c.AllowLocking = true; // identical requests during a refresh wait for the first one
            c.ResponseExpirationTimeSpan = durata;
            c.CacheVaryByRules.QueryKeys = "*";
            c.Tags.Add(Tag);
            return ValueTask.CompletedTask;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext c, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext c, CancellationToken ct)
        {
            var raspuns = c.HttpContext.Response;
            if (raspuns.StatusCode != StatusCodes.Status200OK || !StringValues.IsNullOrEmpty(raspuns.Headers.SetCookie)) c.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }
    }
}
