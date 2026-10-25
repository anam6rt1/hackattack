using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record RaspunsRecapitulare(Guid Token, int VariantaId);
public static class GreseliEndpoints
{
    public static int IntervalZile(int nivel) => new[] { 1, 3, 7, 14 }[Math.Clamp(nivel, 0, 3)];
    public static async Task NoteazaAsync(MarkAiDbContext db, int uid, int intrebare, DateTime acum, CancellationToken ct)
    {
        var g = await db.Set<Greseala>().FindAsync([uid, intrebare], ct);
        if (g is null) { g = new Greseala { UtilizatorId = uid, IntrebareId = intrebare }; db.Add(g); }
        g.GresitLa = acum; g.ScadentaLa = acum.AddDays(1); g.Nivel = 0; g.CorecteConsecutive = 0; g.Token = null; g.EmisaLa = null;
    }
    private static async Task<IQueryable<Greseala>> Vizibile(MarkAiDbContext db, AccesContinut acces, HttpContext h, CancellationToken ct)
    {
        var a = await acces.PentruAsync(h.User, ct);
        var lectii = acces.Parcursuri(a).Where(p => p.Publicat);
        var uid = h.User.Id();
        return db.Set<Greseala>().Where(g => g.UtilizatorId == uid && g.CorecteConsecutive < 2 && g.Intrebare.Activa
            && g.Intrebare.Tip == Intrebare.Grila && g.Intrebare.Variante.Count >= 2 && g.Intrebare.Variante.Count(v => v.Corecta) == 1
            && lectii.Any(p => p.Id == g.Intrebare.ParcursId));
    }
    public static void Mapeaza(WebApplication app)
    {
        var e = app.MapGroup("/greseli").RequireAuthorization(Politici.Elev).WithTags("Greșelile mele");
        e.MapGet("", async (int? pagina, bool? doarScadente, HttpContext h, MarkAiDbContext db, AccesContinut acces, TimeProvider ceas, CancellationToken ct) =>
        {
            var toate = await Vizibile(db, acces, h, ct); var acum = ceas.GetUtcNow().UtcDateTime;
            var lista = doarScadente == true ? toate.Where(g => g.ScadentaLa <= acum) : toate;
            return (object)new { Total = await lista.CountAsync(ct), Scadente = await toate.CountAsync(g => g.ScadentaLa <= acum, ct),
                Elemente = await lista.OrderBy(g => g.ScadentaLa).ThenBy(g => g.IntrebareId).Pagina(new Pagini.Marime(10, 50), pagina, 10)
                    .Select(g => new { g.IntrebareId, g.Intrebare.Text, ParcursId = g.Intrebare.ParcursId, Lectie = g.Intrebare.Parcurs!.Titlu, g.ScadentaLa, g.CorecteConsecutive }).ToListAsync(ct) };
        });
        e.MapPost("/{id:int}/incepe", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, TimeProvider ceas, CancellationToken ct) =>
        {
            var g = await (await Vizibile(db, acces, h, ct)).SingleOrDefaultAsync(g => g.IntrebareId == id, ct) ?? throw new KeyNotFoundException("Întrebarea nu există.");
            var acum = ceas.GetUtcNow().UtcDateTime;
            if (g.ScadentaLa > acum) throw new Conflict("Întrebarea va reveni la data programată.");
            g.Token = Guid.NewGuid(); g.EmisaLa = acum;
            await db.SaveChangesAsync(ct);
            return (object)new { g.Token, Intrebare = await db.Set<Intrebare>().Where(q => q.Id == id).Select(q => new IntrebarePublica(q.Id, q.Text,
                q.Variante.OrderBy(v => v.Id).Select(v => new VariantaPublica(v.Id, v.Text)).ToList())).SingleAsync(ct) };
        });
        e.MapPost("/{id:int}/raspuns", async (int id, RaspunsRecapitulare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, TimeProvider ceas, CancellationToken ct) =>
        {
            var g = await (await Vizibile(db, acces, h, ct)).SingleOrDefaultAsync(g => g.IntrebareId == id, ct) ?? throw new KeyNotFoundException("Întrebarea nu există.");
            var acum = ceas.GetUtcNow().UtcDateTime;
            if (g.Token != c.Token || g.Token is null || g.EmisaLa is null || acum - g.EmisaLa.Value > TimeSpan.FromMinutes(10) || g.ScadentaLa > acum)
                throw new Conflict("Reîncepe întrebarea; această încercare nu mai este valabilă.");
            var variante = await db.Set<VariantaIntrebare>().Where(v => v.IntrebareId == id).ToListAsync(ct);
            if (variante.All(v => v.Id != c.VariantaId)) throw new CerereInvalida("Varianta nu aparține întrebării.");
            var corect = variante.Single(v => v.Id == c.VariantaId).Corecta;
            // One review per due interval, never XP or lesson completion. A wrong quiz answer reopens the item.
            g.CorecteConsecutive = corect ? g.CorecteConsecutive + 1 : 0;
            g.Nivel = corect ? Math.Min(g.Nivel + 1, 3) : 0;
            g.ScadentaLa = acum.AddDays(IntervalZile(g.Nivel)); g.Token = null; g.EmisaLa = null;
            await db.SaveChangesAsync(ct);
            return (object)new { Corect = corect, VariantaCorectaId = variante.Single(v => v.Corecta).Id,
                Explicatie = await db.Set<Intrebare>().Where(q => q.Id == id).Select(q => q.Explicatie).SingleAsync(ct),
                Rezolvata = g.CorecteConsecutive == 2, g.ScadentaLa, g.CorecteConsecutive };
        });
    }
}
