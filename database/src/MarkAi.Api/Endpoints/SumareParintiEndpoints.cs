using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Api.Servicii.Mesagerie;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;
public record EmailParinte(string Email);
public record TokenParinte(string Token);
public static class SumareParintiEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("/utilizatori/me/sumar-parinte").RequireAuthorization(Politici.Elev);
        g.MapGet("", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            h.Response.Headers.CacheControl = "no-store";
            return (object?)await db.Set<SumarParinte>().Where(p => p.UtilizatorId == h.User.Id()).Select(p => new { p.Email, p.ConfirmatLa, p.CreatLa, p.ExpiraLa }).SingleOrDefaultAsync(ct) ?? new { Email = "", ConfirmatLa = (DateTime?)null, CreatLa = DateTime.MinValue, ExpiraLa = DateTime.MinValue };
        });
        g.MapPut("", async (EmailParinte c, HttpContext h, MarkAiDbContext db, Posta posta, IConfiguration cfg, IDataProtectionProvider dp, TimeProvider ceas, CancellationToken ct) =>
        {
            var email = Invitatii.Adresa(c.Email); var uid = h.User.Id(); var now = ceas.GetUtcNow().UtcDateTime;
            if (await db.Utilizatori.AnyAsync(u => u.Id == uid && u.Email.ToLower() == email, ct)) throw new CerereInvalida("Introdu adresa părintelui, diferită de adresa ta.");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7731026)", ct);
            var p = await db.Set<SumarParinte>().FindAsync([uid], ct);
            if (await db.Set<EmailSumar>().AnyAsync(e => e.UtilizatorId == uid && e.Confirmare && e.Mesaj.CreatLa > now.AddHours(-24), ct)) throw new Conflict("Poți trimite o nouă cerere de confirmare după 24 de ore.");
            await Invitatii.Buget(db, cfg, now, 1, ct);
            var token = Invitatii.Token(); var oprire = Invitatii.Token();
            p ??= db.Add(new SumarParinte { UtilizatorId = uid }).Entity;
            p.Email = email; p.TokenHash = Invitatii.Hash(token); p.OprireHash = Invitatii.Hash(oprire); p.OprireProtejat = dp.CreateProtector("sumar-parinte").Protect(oprire);
            p.CreatLa = now; p.ExpiraLa = now.AddDays(7); p.ConfirmatLa = null; p.UltimaSaptamana = null;
            var nume = await db.Utilizatori.Where(u => u.Id == uid).Select(u => u.Nume).SingleAsync(ct);
            var m = posta.Pune(Invitatii.Mesaj(email, "TeleBac · Confirmarea sumarului pentru părinte", $"{nume} a solicitat trimiterea unui sumar săptămânal despre lecții, teme și ședințe la această adresă. Confirmă doar dacă ești părintele sau tutorele său și dorești aceste emailuri. Linkul expiră în 7 zile. Poți opri oricând sumarele. Dacă nu recunoști cererea, ignoră mesajul.", Invitatii.Url(cfg, "/sumar-parinte", token)), "confirmare_sumar_parinte");
            db.Add(new EmailSumar { Mesaj = m, UtilizatorId = uid, Versiune = p.TokenHash, Confirmare = true });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
        });
        g.MapDelete("", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7731026)", ct);
            await db.Set<SumarParinte>().Where(p => p.UtilizatorId == h.User.Id()).ExecuteDeleteAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
        });
        app.MapPost("/sumar-parinte/confirmare", async (TokenParinte c, MarkAiDbContext db, TimeProvider ceas, CancellationToken ct) =>
        {
            var hash = Invitatii.Hash(c.Token); var now = ceas.GetUtcNow().UtcDateTime;
            var n = await db.Set<SumarParinte>().Where(p => p.TokenHash == hash && p.ExpiraLa > now && p.ConfirmatLa == null && db.Utilizatori.Any(u => u.Id == p.UtilizatorId && u.StergereCerutaLa == null))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ConfirmatLa, now), ct);
            if (n == 0) throw new CerereInvalida("Linkul a expirat sau a fost deja folosit.");
            return Results.Ok(new { Mesaj = "Adresa a fost confirmată. Vei primi sumarul săptămânal, fără să ai nevoie de cont." });
        }).RequireRateLimiting(LimiteCereri.Inregistrare);
        app.MapPost("/sumar-parinte/oprire", async (TokenParinte c, MarkAiDbContext db, CancellationToken ct) =>
        {
            var hash = Invitatii.Hash(c.Token); await db.Set<SumarParinte>().Where(p => p.OprireHash == hash).ExecuteDeleteAsync(ct);
            return Results.Ok(new { Mesaj = "Sumarele au fost oprite." });
        }).RequireRateLimiting(LimiteCereri.Inregistrare);
    }
}
