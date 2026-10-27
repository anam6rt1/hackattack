using System.Text.Json;
using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MarkAi.Api.Endpoints;

public record SimulareNoua(int MaterieId, string Tip, string Clasa, int NumarIntrebari, int DurataMinute);
public record OptiuneSimulare(int MaterieId, string Clasa, string Materie, int Disponibile);
public record RaspunsSimulare(int IntrebareId, int? VariantaId, int? Valoare);
public class IntrebareSimulare
{
    public int Id { get; set; }
    public int ParcursId { get; set; }
    public string Lectie { get; set; } = "";
    public string Text { get; set; } = "";
    public string Tip { get; set; } = "";
    public List<VariantaPublica> Variante { get; set; } = [];
    public int? VariantaCorectaId { get; set; }
    public int? RaspunsNumeric { get; set; }
    public string? Explicatie { get; set; }
    public bool Raspunsa { get; set; }
    public int? VariantaAleasaId { get; set; }
    public int? ValoareAleasa { get; set; }
    public bool Corect => Raspunsa && (Tip == Intrebare.Grila ? VariantaAleasaId == VariantaCorectaId : ValoareAleasa == RaspunsNumeric);
}

public static class SimulariEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static List<IntrebareSimulare> Citeste(Simulare s) => JsonSerializer.Deserialize<List<IntrebareSimulare>>(s.IntrebariJson, Json)!;
    private static void Inchide(Simulare s, List<IntrebareSimulare> q, DateTime acum)
    {
        if (s.FinalizatLa != null) return;
        s.Corecte = q.Count(x => x.Corect); s.FinalizatLa = acum > s.ExpiraLa ? s.ExpiraLa : acum;
    }
    private static object Dto(Simulare s, DateTime acum)
    {
        var q = Citeste(s); var gata = s.FinalizatLa != null;
        return new { s.Id, s.MaterieId, s.Tip, s.Clasa, s.InceputLa, s.ExpiraLa, s.FinalizatLa, s.Total, ServerLa = acum,
            Corecte = gata ? (int?)s.Corecte : null, NotaOrientativa = gata ? Math.Round(1m + 9m * s.Corecte / s.Total, 2) : (decimal?)null,
            Intrebari = q.Select(x => new { x.Id, x.ParcursId, x.Lectie, x.Text, x.Tip, x.Variante, x.Raspunsa, x.VariantaAleasaId, x.ValoareAleasa,
                VariantaCorectaId = gata ? x.VariantaCorectaId : null, RaspunsNumeric = gata ? x.RaspunsNumeric : null,
                Explicatie = gata ? x.Explicatie : null, Corect = gata ? (bool?)x.Corect : null }).ToList(),
            DeRecapitulat = gata ? q.Where(x => !x.Corect).GroupBy(x => new { x.ParcursId, x.Lectie }).Select(g => new { g.Key.ParcursId, g.Key.Lectie, Greseli = g.Count() }).ToArray() : null };
    }
    private static IQueryable<Intrebare> Disponibile(MarkAiDbContext db, AccesContinut acces, ContextAcces a) => db.Set<Intrebare>()
        .Where(q => q.Activa && q.ParcursId != null && acces.Parcursuri(a).Any(p => p.Id == q.ParcursId && p.Publicat)
            && ((q.Tip == Intrebare.Grila && q.Variante.Any(v => v.Corecta) && q.Variante.Any(v => !v.Corecta)) || (q.Tip == Intrebare.Estimare && q.RaspunsNumeric != null)));
    private static async Task<Simulare> Gaseste(int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, CancellationToken ct)
    {
        var s = await db.Set<Simulare>().SingleOrDefaultAsync(s => s.Id == id && s.UtilizatorId == h.User.Id(), ct) ?? throw new KeyNotFoundException("Simularea nu există.");
        var ids = Citeste(s).Select(x => x.ParcursId).Distinct().ToArray(); var a = await acces.PentruAsync(h.User, ct);
        if (await acces.Parcursuri(a).CountAsync(p => ids.Contains(p.Id) && p.Publicat, ct) != ids.Length)
            throw new KeyNotFoundException("Unele materiale ale simulării nu mai sunt disponibile.");
        return s;
    }
    public static void Mapeaza(WebApplication app)
    {
        var e = app.MapGroup("/simulari").RequireAuthorization(Politici.Elev).WithTags("Simulări");
        // The same for everyone who sees the same Lecții (their organisations), so kept for a minute per such group: counting every
        // question takes ~300 ms. Starting a simulation counts again, so a number a minute old never yields a short exam.
        e.MapGet("/optiuni", async (HttpContext h, MarkAiDbContext db, AccesContinut acces, IMemoryCache cache, CancellationToken ct) =>
        {
            var a = await acces.PentruAsync(h.User, ct);
            var autor = a.Admin || h.User.IsInRole(Roluri.Profesor) || h.User.IsInRole(Roluri.Organizatie) ? a.UtilizatorId : 0;
            var cheie = $"simulari-optiuni:{a.Admin}:{autor}:{string.Join(',', a.Organizatii.Order())}:{string.Join(',', a.Administrate.Order())}";
            return (object)(await cache.GetOrCreateAsync(cheie, async intrare =>
            {
                intrare.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return (await Disponibile(db, acces, a).GroupBy(q => new { q.MaterieId, q.Parcurs!.Clasa, Materie = q.Parcurs.Materie.Denumire })
                    .Select(g => new { g.Key.MaterieId, g.Key.Clasa, g.Key.Materie, Disponibile = g.Count() }).OrderBy(x => x.Materie).ThenBy(x => x.Clasa).ToListAsync(ct))
                    .Select(x => new OptiuneSimulare(x.MaterieId, x.Clasa, x.Materie, x.Disponibile)).ToList();
            }))!;
        });
        e.MapGet("", async (int? pagina, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var q = db.Set<Simulare>().Where(s => s.UtilizatorId == h.User.Id());
            return (object)new { Total = await q.CountAsync(ct), Elemente = await q.OrderByDescending(s => s.InceputLa).ThenByDescending(s => s.Id).Pagina(new Pagini.Marime(10, 50), pagina, 10)
                .Select(s => new { s.Id, s.MaterieId, s.Tip, s.Clasa, s.InceputLa, s.ExpiraLa, s.FinalizatLa, s.Total }).ToListAsync(ct) };
        });
        e.MapPost("", async (SimulareNoua c, HttpContext h, MarkAiDbContext db, AccesContinut acces, TimeProvider ceas, CancellationToken ct) =>
        {
            if (c.Tip is not ("bac" or "evaluare_nationala") || !Parcurs.Clase.Contains(c.Clasa) || (c.Tip == "evaluare_nationala") != (c.Clasa == "VIII")) throw new CerereInvalida("Alege examenul și o clasă potrivită.");
            if (c.NumarIntrebari is < 5 or > 50 || c.DurataMinute is < 5 or > 180) throw new CerereInvalida("Alege 5–50 de întrebări și 5–180 de minute.");
            var uid = h.User.Id(); var acum = ceas.GetUtcNow().UtcDateTime;
            var activa = await db.Set<Simulare>().Where(s => s.UtilizatorId == uid && s.FinalizatLa == null && s.ExpiraLa > acum).OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
            if (activa != null) throw new Conflict($"Ai deja o simulare începută (#{activa.Id}). Reia-o din istoric.");
            var a = await acces.PentruAsync(h.User, ct);
            var ids = await Disponibile(db, acces, a).Where(q => q.MaterieId == c.MaterieId && q.Parcurs!.Clasa == c.Clasa)
                .OrderBy(q => EF.Functions.Random()).Select(q => q.Id).Take(c.NumarIntrebari).ToArrayAsync(ct);
            if (ids.Length < c.NumarIntrebari) throw new Conflict($"Sunt disponibile doar {ids.Length} întrebări pentru această materie și clasă.");
            var intrebari = (await db.Set<Intrebare>().Include(q => q.Variante).Include(q => q.Parcurs).Where(q => ids.Contains(q.Id)).ToListAsync(ct)).ToDictionary(q => q.Id);
            var lista = ids.Select(id => { var q = intrebari[id]; return new IntrebareSimulare { Id = id, ParcursId = q.ParcursId!.Value, Lectie = q.Parcurs!.Titlu, Text = q.Text, Tip = q.Tip,
                Variante = q.Variante.Select(v => new VariantaPublica(v.Id, v.Text)).ToList(), VariantaCorectaId = q.Variante.SingleOrDefault(v => v.Corecta)?.Id,
                RaspunsNumeric = q.RaspunsNumeric, Explicatie = q.Explicatie }; }).ToList();
            var s = new Simulare { UtilizatorId = uid, MaterieId = c.MaterieId, Tip = c.Tip, Clasa = c.Clasa, InceputLa = acum, ExpiraLa = acum.AddMinutes(c.DurataMinute), Total = ids.Length, IntrebariJson = JsonSerializer.Serialize(lista, Json) };
            db.Add(s); await db.SaveChangesAsync(ct); return Dto(s, acum);
        });
        e.MapGet("/{id:int}", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, MutatiiCont mutatii, TimeProvider ceas, CancellationToken ct) =>
        {
            var blocare = mutatii.Pentru(h.User.Id()); await blocare.WaitAsync(ct);
            try {
            var s = await Gaseste(id, h, db, acces, ct); var acum = ceas.GetUtcNow().UtcDateTime;
            if (acum >= s.ExpiraLa && s.FinalizatLa == null) { Inchide(s, Citeste(s), acum); await db.SaveChangesAsync(ct); }
            h.Response.Headers.CacheControl = "no-store"; return Dto(s, acum);
            } finally { blocare.Release(); }
        });
        e.MapPost("/{id:int}/raspuns", async (int id, RaspunsSimulare c, HttpContext h, MarkAiDbContext db, AccesContinut acces, TimeProvider ceas, CancellationToken ct) =>
        {
            var s = await Gaseste(id, h, db, acces, ct); var acum = ceas.GetUtcNow().UtcDateTime;
            if (s.FinalizatLa != null) throw new Conflict("Simularea s-a încheiat.");
            var lista = Citeste(s);
            if (acum >= s.ExpiraLa) { Inchide(s, lista, acum); await db.SaveChangesAsync(ct); return Dto(s, acum); }
            var q = lista.SingleOrDefault(x => x.Id == c.IntrebareId) ?? throw new CerereInvalida("Întrebarea nu aparține simulării.");
            if (q.Tip == Intrebare.Grila ? (c.Valoare != null || c.VariantaId == null || q.Variante.All(v => v.Id != c.VariantaId)) : (c.VariantaId != null || c.Valoare == null)) throw new CerereInvalida("Răspunsul nu este valid pentru această întrebare.");
            q.Raspunsa = true; q.VariantaAleasaId = c.VariantaId; q.ValoareAleasa = c.Valoare;
            s.IntrebariJson = JsonSerializer.Serialize(lista, Json); await db.SaveChangesAsync(ct); return Dto(s, acum);
        });
        e.MapPost("/{id:int}/finalizare", async (int id, HttpContext h, MarkAiDbContext db, AccesContinut acces, TimeProvider ceas, CancellationToken ct) =>
        {
            var s = await Gaseste(id, h, db, acces, ct); var acum = ceas.GetUtcNow().UtcDateTime;
            Inchide(s, Citeste(s), acum); await db.SaveChangesAsync(ct); return Dto(s, acum);
        });
    }
}
