using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MarkAi.Api.Infrastructura;

/// <summary>List envelope used by every collection endpoint, as in api-mock/openapi.yaml.</summary>
public record Lista<T>(IReadOnlyList<T> Elemente, int Total)
{
    public static Lista<T> Din(IReadOnlyList<T> elemente) => new(elemente, elemente.Count);
}

/// <summary>Thrown for invalid client input; becomes a 400 with the message shown to the user.</summary>
public sealed class CerereInvalida(string mesaj) : Exception(mesaj);

/// <summary>Maps domain exceptions to problem responses with Romanian, user-facing messages.</summary>
public sealed class TratareErori(RequestDelegate next, ILogger<TratareErori> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex) when (!ctx.Response.HasStarted && Mapare(ex) is var (status, mesaj) && status != 500)
        {
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new { status, mesaj });
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            log.LogError(ex, "Unhandled error on {Path}", ctx.Request.Path);
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsJsonAsync(new { status = 500, mesaj = "A apărut o eroare. Încearcă din nou." });
        }
    }

    private static (int, string) Mapare(Exception ex) => ex switch
    {
        CerereInvalida e => (400, e.Message),
        KeyNotFoundException e => (404, e.Message),
        UnauthorizedAccessException => (403, "Nu ai acces la această resursă."),
        DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => (409, "Există deja o înregistrare cu aceste date."),
        Conflict e => (409, e.Message),
        _ => (500, ""),
    };
}

public static class Validare
{
    public static string Obligatoriu(string? valoare, string camp, int max = 200)
    {
        var v = valoare?.Trim();
        if (string.IsNullOrEmpty(v)) throw new CerereInvalida($"Câmpul „{camp}” este obligatoriu.");
        if (v.Length > max) throw new CerereInvalida($"Câmpul „{camp}” are cel mult {max} caractere.");
        return v;
    }

    public static string? Optional(string? valoare, int max = 4000)
    {
        var v = valoare?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > max) throw new CerereInvalida($"Textul are cel mult {max} caractere.");
        return v;
    }

    /// <summary>Normalises Romanian numbers to E.164 (07xx xxx xxx → +407xxxxxxxx) so callers can be matched by phone.</summary>
    public static string? Telefon(string? valoare)
    {
        if (string.IsNullOrWhiteSpace(valoare)) return null;
        var cifre = new string(valoare.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (cifre.StartsWith("00")) cifre = "+" + cifre[2..];
        else if (cifre.StartsWith('0')) cifre = "+40" + cifre[1..];
        else if (!cifre.StartsWith('+')) cifre = "+" + cifre;
        if (cifre.Length is < 9 or > 16 || cifre.Skip(1).Any(c => !char.IsDigit(c)))
            throw new CerereInvalida("Numărul de telefon nu este valid.");
        return cifre;
    }
}
