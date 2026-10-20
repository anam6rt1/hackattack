using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace MarkAi.Api.Infrastructura;

public static class Roluri
{
    public const string Elev = "elev";
    public const string Profesor = "profesor";
    public const string Admin = "admin";

    /// <summary>An account has exactly one experience; the highest role wins if several were assigned.</summary>
    public static string Principal(IEnumerable<string> roluri)
    {
        var set = roluri.ToHashSet();
        return set.Contains(Admin) ? Admin : set.Contains(Profesor) ? Profesor : Elev;
    }
}

public static class Politici
{
    public const string Elev = "elev";
    public const string ProfesorAprobat = "profesor-aprobat";
    public const string Admin = "admin";
    // Approved professors and admins may publish content.
    public const string Continut = "continut";
}

public sealed class OptiuniJwt
{
    public required SymmetricSecurityKey Cheie { get; init; }
    public TimeSpan Durata { get; init; } = TimeSpan.FromDays(14);
    public bool CookieSecure { get; init; }
    public const string Cookie = "markai_sesiune";
    public const string Emitent = "markai-api";
    /// <summary>Claim with the account's <see cref="Utilizator.VersiuneSesiune"/> when the token was issued.</summary>
    public const string ClaimVersiune = "sv";
}

public sealed class Tokenuri(OptiuniJwt optiuni)
{
    public string Emite(Utilizator u, string rol)
    {
        var token = new JwtSecurityToken(
            issuer: OptiuniJwt.Emitent, audience: OptiuniJwt.Emitent,
            claims: [new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Name, u.Nume), new Claim(ClaimTypes.Role, rol),
                new Claim(OptiuniJwt.ClaimVersiune, u.VersiuneSesiune.ToString())],
            expires: DateTime.UtcNow.Add(optiuni.Durata),
            signingCredentials: new SigningCredentials(optiuni.Cheie, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // The site reaches the API through its own /api rewrite (same origin), so the cookie is never needed cross-site.
    // Secure is on whenever the site is served over HTTPS (the public tunnel).
    public void SeteazaCookie(HttpResponse r, string token) => r.Cookies.Append(OptiuniJwt.Cookie, token, new CookieOptions
    {
        HttpOnly = true, Secure = optiuni.CookieSecure, IsEssential = true, Path = "/", SameSite = SameSiteMode.Lax,
        MaxAge = optiuni.Durata,
    });

    public void StergeCookie(HttpResponse r) => r.Cookies.Delete(OptiuniJwt.Cookie, new CookieOptions
    {
        Path = "/", Secure = optiuni.CookieSecure, SameSite = SameSiteMode.Lax,
    });

    public static TokenValidationParameters Validare(SymmetricSecurityKey cheie) => new()
    {
        ValidIssuer = OptiuniJwt.Emitent, ValidAudience = OptiuniJwt.Emitent, IssuerSigningKey = cheie,
        ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role,
    };
}

public static class ExtensiiUtilizator
{
    public static int Id(this ClaimsPrincipal p) =>
        int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    public static bool EsteAdmin(this ClaimsPrincipal p) => p.IsInRole(Roluri.Admin);
}

/// <summary>
/// Session revocation: a token is valid only while its version claim equals the account's current
/// <see cref="Utilizator.VersiuneSesiune"/>. Versions are cached briefly; raising one here takes effect at once in this process.
/// </summary>
public sealed class VersiuniSesiune(IServiceScopeFactory scopes, IMemoryCache cache)
{
    private static readonly TimeSpan Durata = TimeSpan.FromSeconds(30);

    /// <summary>The current version, or null when the account no longer exists.</summary>
    public async Task<int?> CitesteAsync(int utilizatorId, CancellationToken ct)
    {
        if (cache.TryGetValue(("sv", utilizatorId), out int? v)) return v;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarkAiDbContext>();
        v = await db.Utilizatori.Where(u => u.Id == utilizatorId).Select(u => (int?)u.VersiuneSesiune).SingleOrDefaultAsync(ct);
        cache.Set(("sv", utilizatorId), v, Durata);
        return v;
    }

    /// <summary>Signs the account out on every device; returns the new version (for a fresh token on this device).</summary>
    public async Task<int> CresteAsync(MarkAiDbContext db, int utilizatorId, CancellationToken ct)
    {
        await db.Utilizatori.Where(u => u.Id == utilizatorId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.VersiuneSesiune, u => u.VersiuneSesiune + 1), ct);
        cache.Remove(("sv", utilizatorId));
        return await db.Utilizatori.Where(u => u.Id == utilizatorId).Select(u => u.VersiuneSesiune).SingleAsync(ct);
    }
}

/// <summary>
/// Approval can change after a token is issued, so it is checked against the database on every request.
/// </summary>
public sealed class CerintaProfesorAprobat : IAuthorizationRequirement
{
    public bool AcceptaAdmin { get; init; }
}

public sealed class VerificareProfesorAprobat(MarkAiDbContext db) : AuthorizationHandler<CerintaProfesorAprobat>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CerintaProfesorAprobat cerinta)
    {
        if (cerinta.AcceptaAdmin && context.User.IsInRole(Roluri.Admin)) { context.Succeed(cerinta); return; }
        if (!context.User.IsInRole(Roluri.Profesor)) return;
        var id = context.User.Id();
        if (await db.ProfiluriMentor.AnyAsync(m => m.UtilizatorId == id && m.StareVerificare == Data.Servicii.RezervareService.MentorAprobat))
            context.Succeed(cerinta);
    }
}

public static class ConfigurareAutentificare
{
    public static OptiuniJwt Optiuni(IConfiguration cfg, IHostEnvironment env)
    {
        var cheie = Environment.GetEnvironmentVariable("MARKAI_JWT_KEY") ?? cfg["Autentificare:CheieJwt"];
        if (string.IsNullOrEmpty(cheie))
        {
            if (!env.IsDevelopment()) throw new InvalidOperationException("Set MARKAI_JWT_KEY (at least 32 characters).");
            // Development only: sessions do not survive a restart.
            cheie = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        }
        if (cheie.Length < 32) throw new InvalidOperationException("MARKAI_JWT_KEY must be at least 32 characters.");
        return new OptiuniJwt
        {
            Cheie = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cheie)),
            Durata = TimeSpan.FromDays(cfg.GetValue("Autentificare:DurataZile", 14)),
            CookieSecure = cfg.GetValue("Autentificare:CookieSecure", false),
        };
    }

    public static void Adauga(IServiceCollection services, OptiuniJwt optiuni)
    {
        services.AddSingleton(optiuni);
        services.AddSingleton<Tokenuri>();
        services.AddMemoryCache();
        services.AddSingleton<VersiuniSesiune>();
        services.AddScoped<IAuthorizationHandler, VerificareProfesorAprobat>();
        services.AddAuthentication("Bearer").AddJwtBearer("Bearer", o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = Tokenuri.Validare(optiuni.Cheie);
            // The browser sends the httpOnly cookie (also on the SignalR connection); API clients may use a bearer header.
            o.Events = new()
            {
                OnMessageReceived = c =>
                {
                    if (string.IsNullOrEmpty(c.Token) && c.Request.Cookies.TryGetValue(OptiuniJwt.Cookie, out var t)) c.Token = t;
                    return Task.CompletedTask;
                },
                // Tokens issued before session versions existed have no claim and count as version 0.
                OnTokenValidated = async c =>
                {
                    var versiuni = c.HttpContext.RequestServices.GetRequiredService<VersiuniSesiune>();
                    var curenta = await versiuni.CitesteAsync(c.Principal!.Id(), c.HttpContext.RequestAborted);
                    var dinToken = int.TryParse(c.Principal!.FindFirstValue(OptiuniJwt.ClaimVersiune), out var v) ? v : 0;
                    if (curenta != dinToken) c.Fail("Sesiunea a fost închisă.");
                },
            };
        });
        services.AddAuthorizationBuilder()
            .AddPolicy(Politici.Elev, p => p.RequireRole(Roluri.Elev))
            .AddPolicy(Politici.Admin, p => p.RequireRole(Roluri.Admin))
            .AddPolicy(Politici.ProfesorAprobat, p => p.AddRequirements(new CerintaProfesorAprobat()))
            .AddPolicy(Politici.Continut, p => p.AddRequirements(new CerintaProfesorAprobat { AcceptaAdmin = true }));
    }
}
