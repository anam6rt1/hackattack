using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record MesajTrimis(int Id, string Canal, string Catre, string? Subiect, string Tip, DateTime CreatLa, DateTime? TrimisLa,
    string? Furnizor, int Incercari, string? Eroare, string? Continut);

public static class AdminEndpoints
{
    public static void Mapeaza(WebApplication app)
    {
        var admin = app.MapGroup("/admin").WithTags("Administrare").RequireAuthorization(Politici.Admin);

        // The latest emails and SMS. The content is shown only for messages handled by the "jurnal" (development) provider,
        // which delivers nothing: it is how sign-up codes are read before a real provider is configured.
        admin.MapGet("/mesaje", async (string? canal, MarkAiDbContext db, CancellationToken ct) =>
        {
            var q = db.Set<MesajIesire>().AsNoTracking();
            if (!string.IsNullOrEmpty(canal)) q = q.Where(m => m.Canal == canal);
            var mesaje = await q.OrderByDescending(m => m.Id).Take(100)
                .Select(m => new MesajTrimis(m.Id, m.Canal, m.Catre, m.Subiect, m.Tip, m.CreatLa, m.TrimisLa, m.Furnizor, m.Incercari, m.Eroare,
                    m.Furnizor == "jurnal" ? m.Text : null))
                .ToListAsync(ct);
            return Lista<MesajTrimis>.Din(mesaje);
        });
    }
}
