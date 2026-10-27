using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;
public record FeedbackNou(string Text, string? Pagina);
public static class FeedbackEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        app.MapPost("/feedback", async (FeedbackNou c, HttpContext h, MarkAiDbContext db, TimeProvider ceas, CancellationToken ct) =>
        {
            var now = ceas.GetUtcNow().UtcDateTime; var uid = h.User.Id();
            if (await db.Set<FeedbackPilot>().CountAsync(f => f.UtilizatorId == uid && f.CreatLa > now.AddDays(-1), ct) >= 5) throw new Conflict("Ai trimis deja 5 mesaje astăzi. Mulțumim, le vom verifica.");
            var pagina = (c.Pagina ?? "").Split('?', '#')[0];
            if (pagina.Length > 200 || !pagina.StartsWith('/') || pagina.StartsWith("//")) pagina = "/";
            db.Add(new FeedbackPilot { UtilizatorId = uid, Text = Validare.Obligatoriu(c.Text, "Mesaj", 2000), Pagina = pagina, CreatLa = now });
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization();
        var a = app.MapGroup("/admin/feedback").RequireAuthorization(Politici.Admin);
        a.MapGet("", async (int? pagina, bool? rezolvate, MarkAiDbContext db, CancellationToken ct) =>
        {
            var q = db.Set<FeedbackPilot>().Where(f => (f.RezolvatLa != null) == (rezolvate ?? false));
            return (object)new { Total = await q.CountAsync(ct), Elemente = await q.OrderByDescending(f => f.Id).Skip((Math.Clamp(pagina ?? 1, 1, 10000) - 1) * 20).Take(20).ToListAsync(ct) };
        });
        a.MapPost("/{id:int}/rezolvat", async (int id, MarkAiDbContext db, TimeProvider ceas, CancellationToken ct) =>
        {
            await db.Set<FeedbackPilot>().Where(f => f.Id == id).ExecuteUpdateAsync(s => s.SetProperty(f => f.RezolvatLa, ceas.GetUtcNow().UtcDateTime), ct); return Results.NoContent();
        });
    }
}
