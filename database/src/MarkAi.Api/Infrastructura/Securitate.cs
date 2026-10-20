using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace MarkAi.Api.Infrastructura;

/// <summary>Named rate-limit policies, applied per endpoint with <c>RequireRateLimiting</c>.</summary>
public static class LimiteCereri
{
    /// <summary>Login attempts, per client IP.</summary>
    public const string Autentificare = "autentificare";
    /// <summary>New accounts, per client IP.</summary>
    public const string Inregistrare = "inregistrare";
    /// <summary>Emailed confirmation codes, per user.</summary>
    public const string Email = "email";
    /// <summary>Requests that cost AI credits, per user.</summary>
    public const string Ai = "ai";
    /// <summary>"Sună-mă" outbound calls, per user.</summary>
    public const string Telefon = "telefon";
}

/// <summary>
/// Request-level protections: the client IP behind the proxies, security headers, a CSRF header on browser
/// mutations, rate limits and a default request size. Limits come from <c>Limite:*</c> settings so a demo can relax them.
/// </summary>
public static class Securitate
{
    /// <summary>Every browser mutation carries this header; a cross-site form or image cannot add it.</summary>
    public const string AntetCsrf = "X-MarkAi";
    public const long MaxCerereImplicitaMb = 2;

    public static void AdaugaServicii(WebApplicationBuilder builder)
    {
        var cfg = builder.Configuration;
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxCerereImplicitaMb * 1024 * 1024);
        // ASP.NET's "Request starting/finished" lines contain the full URL, and a phone call's webhook URLs carry its key.
        // Pinned here, so raising the log level in appsettings can never put those keys into api.log.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

        // Browser → cloudflared → Next.js (/api rewrite, passes headers through) → API. The connection comes from Next on
        // localhost, and the last X-Forwarded-For entry is the address cloudflared saw; anything before it is client-supplied.
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownIPNetworks.Clear(); o.KnownProxies.Clear();
            o.KnownProxies.Add(IPAddress.Loopback); o.KnownProxies.Add(IPAddress.IPv6Loopback);
        });

        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var dupa))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(dupa.TotalSeconds)).ToString();
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { status = 429, mesaj = "Prea multe încercări. Așteaptă puțin și încearcă din nou." }, ct);
            };
            Fereastra(o, LimiteCereri.Autentificare, cfg.GetValue("Limite:AutentificariPeMinut", 10), TimeSpan.FromMinutes(1), peUtilizator: false);
            Fereastra(o, LimiteCereri.Inregistrare, cfg.GetValue("Limite:InregistrariPeOra", 5), TimeSpan.FromHours(1), peUtilizator: false);
            Fereastra(o, LimiteCereri.Email, cfg.GetValue("Limite:EmailuriPeOra", 6), TimeSpan.FromHours(1), peUtilizator: true);
            Fereastra(o, LimiteCereri.Ai, cfg.GetValue("Limite:CereriAiPeMinut", 10), TimeSpan.FromMinutes(1), peUtilizator: true);
            Fereastra(o, LimiteCereri.Telefon, cfg.GetValue("Limite:ApeluriPeMinut", 2), TimeSpan.FromMinutes(1), peUtilizator: true);
            // Everything else: a generous ceiling per user (or per IP when signed out) against scripted abuse.
            var global = cfg.GetValue("Limite:CereriPeMinut", 600);
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(h => RateLimitPartition.GetFixedWindowLimiter(Cheie(h, true),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = global, Window = TimeSpan.FromMinutes(1) }));
        });
    }

    private static void Fereastra(RateLimiterOptions o, string nume, int limita, TimeSpan fereastra, bool peUtilizator) =>
        o.AddPolicy(nume, h => RateLimitPartition.GetFixedWindowLimiter(Cheie(h, peUtilizator),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limita, Window = fereastra }));

    private static string Cheie(HttpContext h, bool peUtilizator) =>
        peUtilizator && h.User.Identity?.IsAuthenticated == true
            ? "u:" + h.User.Id()
            : "ip:" + (h.Connection.RemoteIpAddress?.ToString() ?? "necunoscut");

    /// <summary>Runs before authentication: client IP, headers, CSRF. Rate limiting runs after it (it needs the user).</summary>
    public static void FoloseteInainteDeAutentificare(WebApplication app)
    {
        app.UseForwardedHeaders();
        app.Use(async (ctx, next) =>
        {
            var antete = ctx.Response.Headers;
            antete.XContentTypeOptions = "nosniff";
            antete["Referrer-Policy"] = "no-referrer";
            antete.XFrameOptions = "DENY";
            // The API only returns JSON, TwiML and files; nothing it serves should run scripts or be framed.
            if (!ctx.Request.Path.StartsWithSegments("/swagger"))
                antete.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; sandbox";

            if (CereCsrf(ctx.Request) && ctx.Request.Headers[AntetCsrf] != "1")
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { status = 403, mesaj = "Cerere nevalidă. Reîncarcă pagina și încearcă din nou." });
                return;
            }
            await next();
        });
    }

    // Twilio's webhooks are signed instead; the SignalR negotiate request comes from the game client library.
    private static bool CereCsrf(HttpRequest r) =>
        !HttpMethods.IsGet(r.Method) && !HttpMethods.IsHead(r.Method) && !HttpMethods.IsOptions(r.Method)
        && !r.Path.StartsWithSegments("/twilio") && !r.Path.StartsWithSegments("/hub");
}
