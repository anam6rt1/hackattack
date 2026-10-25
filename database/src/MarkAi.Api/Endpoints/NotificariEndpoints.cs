using MarkAi.Api.Infrastructura;
using MarkAi.Api.Servicii;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;
public record OptiuniNotificari(bool Sedinte, bool Decizii, bool TemeNoi, bool TermeneTeme);
public record CheiAbonament(string P256dh, string Auth);
public record AbonamentNou(string Endpoint, CheiAbonament Keys);
public record DezabonarePush(string Endpoint);
public static class NotificariEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var g = app.MapGroup("/utilizatori/me/notificari").RequireAuthorization().WithTags("Notificări");
        g.MapGet("", async (HttpContext h, MarkAiDbContext db, Notificari coada, IExpeditorPush push, ExpeditorPush real, CancellationToken ct) =>
        {
            var p = await db.Set<PreferinteNotificari>().FindAsync([h.User.Id()], ct) ?? coada.Implicite(h.User.Id());
            h.Response.Headers.CacheControl = "no-store";
            return (object)new { p.Sedinte, p.Decizii, p.TemeNoi, p.TermeneTeme, PushDisponibil = push.Configurat,
                real.CheiePublica, Abonamente = await db.Set<AbonamentPush>().CountAsync(a => a.UtilizatorId == h.User.Id(), ct) };
        });
        g.MapPut("", async (OptiuniNotificari c, HttpContext h, MarkAiDbContext db, TimeProvider ceas, CancellationToken ct) =>
        {
            var p = await db.Set<PreferinteNotificari>().FindAsync([h.User.Id()], ct);
            if (p == null) { p = new() { UtilizatorId = h.User.Id() }; db.Add(p); }
            (p.Sedinte, p.Decizii, p.TemeNoi, p.TermeneTeme, p.ActualizatLa) = (c.Sedinte, c.Decizii, c.TemeNoi, c.TermeneTeme, ceas.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        g.MapPost("/abonamente", async (AbonamentNou c, HttpContext h, MarkAiDbContext db, IExpeditorPush push, TimeProvider ceas, CancellationToken ct) =>
        {
            if (!push.Configurat) throw new Conflict("Notificările browserului nu sunt configurate încă. Emailul rămâne disponibil.");
            if (c.Endpoint == null || c.Keys == null || !ExpeditorPush.EndpointPermis(c.Endpoint)) throw new CerereInvalida("Adresa serviciului push nu este validă.");
            try
            {
                var pub = WebEncoders.Base64UrlDecode(c.Keys.P256dh); var auth = WebEncoders.Base64UrlDecode(c.Keys.Auth);
                if (pub.Length != 65 || pub[0] != 4 || auth.Length != 16) throw new FormatException();
            }
            catch (Exception e) when (e is FormatException or ArgumentException) { throw new CerereInvalida("Cheile abonamentului nu sunt valide."); }
            var uid = h.User.Id();
            var a = await db.Set<AbonamentPush>().SingleOrDefaultAsync(a => a.Endpoint == c.Endpoint, ct);
            if (a != null && (a.P256dh != c.Keys.P256dh || a.Auth != c.Keys.Auth)) throw new Conflict("Abonamentul nu poate fi asociat acestui browser.");
            if (a == null)
            {
                if (await db.Set<AbonamentPush>().CountAsync(a => a.UtilizatorId == uid, ct) >= 10) throw new Conflict("Ai deja 10 browsere asociate. Dezactivează un abonament înainte.");
                a = new() { Endpoint = c.Endpoint, P256dh = c.Keys.P256dh, Auth = c.Keys.Auth }; db.Add(a);
            }
            a.UtilizatorId = uid; a.CreatLa = ceas.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        g.MapDelete("/abonamente", async ([Microsoft.AspNetCore.Mvc.FromBody] DezabonarePush c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            await db.Set<AbonamentPush>().Where(a => a.UtilizatorId == h.User.Id() && a.Endpoint == c.Endpoint).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
    }
}
