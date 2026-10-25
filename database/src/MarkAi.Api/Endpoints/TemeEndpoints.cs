using MarkAi.Api.Infrastructura;
using MarkAi.Data;
using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Endpoints;

public record GrupaNoua(string Nume, int[] EleviIds);
public record TemaNoua(string Titlu, string? Descriere, DateTimeOffset TermenLa, int[] ParcursuriIds, bool ToataOrganizatia, int[] GrupeIds);
public class SituatieTema
{
    public int ElevId { get; set; }
    public string Nume { get; set; } = "";
    public int Total { get; set; }
    public int Terminate { get; set; }
    public bool Inceputa { get; set; }
    public DateTime? PredatLa { get; set; }
    public string Stare => Total > 0 && Terminate == Total ? "predat" : Inceputa ? "inceput" : "nedeschis";
}

public static class TemeEndpoints
{
    private static readonly Pagini.Marime Marime = new(20, 100);
    public static void Mapeaza(WebApplication app)
    {
        var p = app.MapGroup("/organizatii/{org:int}/teme").RequireAuthorization(Politici.ProfesorAprobat).WithTags("Teme");
        app.MapGet("/teme/organizatii", async (HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            return (object)await db.Set<Organizatie>().Where(o => o.Stare == Organizatie.Aprobata && (o.AdminId == uid
                || o.Membri.Any(m => m.UtilizatorId == uid && m.Rol == MembruOrganizatie.RolProfesor && m.Stare == MembruOrganizatie.Aprobat)))
                .OrderBy(o => o.Nume).Select(o => new { o.Id, o.Nume, Admin = o.AdminId == uid }).ToListAsync(ct);
        }).RequireAuthorization(Politici.ProfesorAprobat);

        p.MapGet("/lectii", async (int org, string? q, int? pagina, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            await Profesor(db, org, h.User.Id(), ct);
            var lectii = PentruOrganizatie(db, org);
            foreach (var t in TermeniCautare.Optionali(q)) lectii = lectii.Where(l => EF.Functions.ILike(l.Cautare, t, "\\"));
            return (object)new { Total = await lectii.CountAsync(ct), Elemente = await lectii.OrderBy(l => l.Titlu).ThenBy(l => l.Id).Pagina(Marime, pagina, 20)
                .Select(l => new { l.Id, l.Titlu, l.Clasa }).ToListAsync(ct) };
        });
        p.MapGet("/elevi", async (int org, string? q, int? pagina, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            await Profesor(db, org, h.User.Id(), ct);
            var elevi = Elevi(db, org);
            foreach (var t in TermeniCautare.Optionali(q)) elevi = elevi.Where(m => EF.Functions.ILike(m.Utilizator.CautareNume, t, "\\"));
            return (object)new { Total = await elevi.CountAsync(ct), Elemente = await elevi.OrderBy(m => m.Utilizator.Nume).ThenBy(m => m.UtilizatorId).Pagina(Marime, pagina, 20)
                .Select(m => new { Id = m.UtilizatorId, m.Utilizator.Nume, m.Utilizator.Clasa }).ToListAsync(ct) };
        });
        p.MapGet("/grupe", async (int org, int? pagina, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var admin = await Profesor(db, org, h.User.Id(), ct);
            var grupe = db.Set<GrupaElevi>().Where(g => g.OrganizatieId == org && (admin || g.ProfesorId == h.User.Id()));
            return (object)new { Total = await grupe.CountAsync(ct), Elemente = await grupe.OrderBy(g => g.Nume).ThenBy(g => g.Id).Pagina(Marime, pagina, 20)
                .Select(g => new { g.Id, g.Nume, Elevi = g.Membri.Count(m => db.Set<MembruOrganizatie>().Any(x => x.OrganizatieId == org && x.UtilizatorId == m.ElevId && x.Rol == "elev" && x.Stare == "aprobat")) }).ToListAsync(ct) };
        });
        p.MapGet("/grupe/{id:int}", async (int org, int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var g = await Grupa(db, org, id, h.User.Id(), ct);
            return (object)new { g.Id, g.Nume, EleviIds = await db.Set<GrupaMembru>().Where(m => m.GrupaId == id && Elevi(db, org).Any(e => e.UtilizatorId == m.ElevId)).Select(m => m.ElevId).ToArrayAsync(ct) };
        });
        p.MapPost("/grupe", async (int org, GrupaNoua c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            await Profesor(db, org, h.User.Id(), ct);
            var ids = await ValideazaElevi(db, org, c.EleviIds, ct);
            var g = new GrupaElevi { OrganizatieId = org, ProfesorId = h.User.Id(), Nume = Validare.Obligatoriu(c.Nume, "Numele grupei", 100), Membri = ids.Select(id => new GrupaMembru { ElevId = id }).ToList() };
            db.Add(g); await db.SaveChangesAsync(ct);
            return Results.Created($"/organizatii/{org}/teme/grupe/{g.Id}", new { g.Id, g.Nume });
        });
        p.MapPut("/grupe/{id:int}", async (int org, int id, GrupaNoua c, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var g = await Grupa(db, org, id, h.User.Id(), ct);
            var ids = await ValideazaElevi(db, org, c.EleviIds, ct);
            g.Nume = Validare.Obligatoriu(c.Nume, "Numele grupei", 100);
            var vechi = await db.Set<GrupaMembru>().Where(m => m.GrupaId == id).ToListAsync(ct);
            db.RemoveRange(vechi.Where(m => !ids.Contains(m.ElevId)));
            db.AddRange(ids.Where(e => vechi.All(m => m.ElevId != e)).Select(e => new GrupaMembru { GrupaId = id, ElevId = e }));
            await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        p.MapDelete("/grupe/{id:int}", async (int org, int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var g = await Grupa(db, org, id, h.User.Id(), ct);
            if (await db.Set<TemaGrupa>().AnyAsync(x => x.GrupaId == id && !x.Tema.Anulata, ct)) throw new Conflict("Anulează temele acestei grupe înainte de a o șterge.");
            db.Remove(g); await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        p.MapPost("", async (int org, TemaNoua c, HttpContext h, MarkAiDbContext db, TimeProvider ceas, CancellationToken ct) =>
        {
            var uid = h.User.Id(); var admin = await Profesor(db, org, uid, ct);
            var acum = ceas.GetUtcNow().UtcDateTime;
            var lectii = (c.ParcursuriIds ?? []).Distinct().ToArray(); var grupe = (c.GrupeIds ?? []).Distinct().ToArray();
            if (lectii.Length is < 1 or > 10 || grupe.Length > 10) throw new CerereInvalida("Alege între 1 și 10 lecții și cel mult 10 grupe.");
            if (c.TermenLa.UtcDateTime <= acum || c.TermenLa.UtcDateTime > acum.AddYears(1)) throw new CerereInvalida("Termenul trebuie să fie în viitor, în următorul an.");
            if (c.ToataOrganizatia == (grupe.Length > 0)) throw new CerereInvalida("Alege toată organizația sau cel puțin o grupă.");
            if (await PentruOrganizatie(db, org).CountAsync(l => lectii.Contains(l.Id), ct) != lectii.Length)
                throw new CerereInvalida("Alege lecții publicate, vizibile organizației și cu întrebări de test.");
            if (await db.Set<GrupaElevi>().CountAsync(g => grupe.Contains(g.Id) && g.OrganizatieId == org && (admin || g.ProfesorId == uid), ct) != grupe.Length)
                throw new CerereInvalida("O grupă nu îți aparține sau nu este în această organizație.");
            var tema = new Tema { OrganizatieId = org, ProfesorId = uid, Titlu = Validare.Obligatoriu(c.Titlu, "Titlul", 200),
                Descriere = string.IsNullOrWhiteSpace(c.Descriere) ? null : Validare.Obligatoriu(c.Descriere, "Instrucțiunile", 2000),
                CreatLa = acum, TermenLa = c.TermenLa.UtcDateTime, ToataOrganizatia = c.ToataOrganizatia,
                Lectii = lectii.Select(id => new TemaLectie { ParcursId = id }).ToList(), Grupe = grupe.Select(id => new TemaGrupa { GrupaId = id }).ToList() };
            db.Add(tema); await db.SaveChangesAsync(ct); return Results.Created($"/organizatii/{org}/teme/{tema.Id}", new { tema.Id });
        });
        p.MapGet("", async (int org, int? pagina, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var admin = await Profesor(db, org, h.User.Id(), ct);
            var teme = db.Set<Tema>().Where(t => t.OrganizatieId == org && (admin || t.ProfesorId == h.User.Id()));
            return (object)new { Total = await teme.CountAsync(ct), Elemente = await teme.OrderByDescending(t => t.CreatLa).ThenByDescending(t => t.Id).Pagina(Marime, pagina, 20)
                .Select(t => new { t.Id, t.Titlu, t.Descriere, t.TermenLa, t.Anulata, t.ToataOrganizatia, Profesor = t.Profesor == null ? "Profesor" : t.Profesor.Nume,
                    Lectii = t.Lectii.Select(l => new { Id = l.ParcursId, l.Parcurs.Titlu }).ToList() }).ToListAsync(ct) };
        });
        p.MapDelete("/{id:int}", async (int org, int id, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var t = await TemaProfesor(db, org, id, h.User.Id(), ct); t.Anulata = true;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        p.MapGet("/{id:int}/rezultate", async (int org, int id, string? stare, string? ordine, int? pagina, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var t = await TemaProfesor(db, org, id, h.User.Id(), ct);
            var raport = Situatii(db, t);
            raport = stare switch { null or "" => raport, "predat" => raport.Where(x => x.Total > 0 && x.Terminate == x.Total),
                "inceput" => raport.Where(x => x.Inceputa && x.Terminate < x.Total), "nedeschis" => raport.Where(x => !x.Inceputa && (x.Total == 0 || x.Terminate < x.Total)),
                _ => throw new CerereInvalida("Stare necunoscută.") };
            var sortat = ordine switch { null or "nume" => raport.OrderBy(x => x.Nume).ThenBy(x => x.ElevId), "progres" => raport.OrderByDescending(x => x.Terminate).ThenBy(x => x.Nume).ThenBy(x => x.ElevId), _ => throw new CerereInvalida("Ordine necunoscută.") };
            return (object)new { t.Id, t.Titlu, t.TermenLa, t.Anulata, Total = await raport.CountAsync(ct), Elemente = await sortat.Pagina(Marime, pagina, 20).ToListAsync(ct) };
        });
        app.MapGet("/teme", async (int? pagina, HttpContext h, MarkAiDbContext db, CancellationToken ct) =>
        {
            var uid = h.User.Id();
            var teme = db.Set<Tema>().Where(t => !t.Anulata && t.Lectii.Any() && t.Organizatie.Stare == Organizatie.Aprobata
                && db.Set<MembruOrganizatie>().Any(m => m.UtilizatorId == uid && m.OrganizatieId == t.OrganizatieId && m.Rol == "elev" && m.Stare == "aprobat")
                && (t.ToataOrganizatia || t.Grupe.Any(g => g.Grupa.Membri.Any(m => m.ElevId == uid)))
                && t.Lectii.All(l => l.Parcurs.Publicat && (l.Parcurs.Vizibilitate == Parcurs.Public || l.Parcurs.OrganizatieId == t.OrganizatieId || l.Parcurs.Partajari.Any(s => s.OrganizatieId == t.OrganizatieId))));
            return (object)new { Total = await teme.CountAsync(ct), Elemente = await teme.OrderBy(t => t.TermenLa).ThenBy(t => t.Id).Pagina(Marime, pagina, 10)
                .Select(t => new { t.Id, t.Titlu, t.Descriere, t.TermenLa, Organizatie = t.Organizatie.Nume,
                    Lectii = t.Lectii.OrderBy(l => l.ParcursId).Select(l => new { Id = l.ParcursId, l.Parcurs.Titlu,
                        PromovatLa = l.Parcurs.Progrese.Where(p => p.UtilizatorId == uid).Select(p => p.PromovatLa).FirstOrDefault() }).ToList() }).ToListAsync(ct) };
        }).RequireAuthorization(Politici.Elev);
    }

    private static IQueryable<Parcurs> PentruOrganizatie(MarkAiDbContext db, int org) => db.Set<Parcurs>().Where(l => l.Publicat
        && (l.Vizibilitate == Parcurs.Public || l.OrganizatieId == org || l.Partajari.Any(s => s.OrganizatieId == org))
        && l.Intrebari.Any(q => q.Activa && q.Tip == Intrebare.Grila && q.Variante.Count >= 2 && q.Variante.Count(v => v.Corecta) == 1));
    private static IQueryable<MembruOrganizatie> Elevi(MarkAiDbContext db, int org) => db.Set<MembruOrganizatie>().Where(m => m.OrganizatieId == org && m.Rol == "elev" && m.Stare == "aprobat" && m.Utilizator.StergereCerutaLa == null);
    private static async Task<bool> Profesor(MarkAiDbContext db, int org, int uid, CancellationToken ct)
    {
        var o = await db.Set<Organizatie>().AsNoTracking().SingleOrDefaultAsync(o => o.Id == org && o.Stare == Organizatie.Aprobata, ct);
        if (o == null) throw new KeyNotFoundException("Organizația nu există.");
        if (o.AdminId == uid) return true;
        if (!await db.Set<MembruOrganizatie>().AnyAsync(m => m.OrganizatieId == org && m.UtilizatorId == uid && m.Rol == "profesor" && m.Stare == "aprobat", ct)) throw new UnauthorizedAccessException();
        return false;
    }
    private static async Task<GrupaElevi> Grupa(MarkAiDbContext db, int org, int id, int uid, CancellationToken ct)
    {
        var admin = await Profesor(db, org, uid, ct);
        return await db.Set<GrupaElevi>().SingleOrDefaultAsync(g => g.Id == id && g.OrganizatieId == org && (admin || g.ProfesorId == uid), ct) ?? throw new KeyNotFoundException("Grupa nu există.");
    }
    private static async Task<int[]> ValideazaElevi(MarkAiDbContext db, int org, int[]? ids, CancellationToken ct)
    {
        var alesi = (ids ?? []).Distinct().ToArray();
        if (alesi.Length > 200 || await Elevi(db, org).CountAsync(e => alesi.Contains(e.UtilizatorId), ct) != alesi.Length) throw new CerereInvalida("Alege cel mult 200 de elevi aprobați din această organizație.");
        return alesi;
    }
    private static async Task<Tema> TemaProfesor(MarkAiDbContext db, int org, int id, int uid, CancellationToken ct)
    {
        var admin = await Profesor(db, org, uid, ct);
        return await db.Set<Tema>().SingleOrDefaultAsync(t => t.Id == id && t.OrganizatieId == org && (admin || t.ProfesorId == uid), ct) ?? throw new KeyNotFoundException("Tema nu există.");
    }
    private static IQueryable<SituatieTema> Situatii(MarkAiDbContext db, Tema t) => Elevi(db, t.OrganizatieId)
        .Where(m => t.ToataOrganizatia || db.Set<TemaGrupa>().Any(g => g.TemaId == t.Id && g.Grupa.Membri.Any(e => e.ElevId == m.UtilizatorId)))
        .Select(m => new SituatieTema { ElevId = m.UtilizatorId, Nume = m.Utilizator.Nume, Total = db.Set<TemaLectie>().Count(l => l.TemaId == t.Id),
            Terminate = db.Set<TemaLectie>().Count(l => l.TemaId == t.Id && l.Parcurs.Progrese.Any(p => p.UtilizatorId == m.UtilizatorId && p.PromovatLa != null)),
            Inceputa = db.Set<ProgresLectie>().Any(p => p.UtilizatorId == m.UtilizatorId && db.Set<TemaLectie>().Any(l => l.TemaId == t.Id && l.ParcursId == p.ParcursId))
                || db.Set<IncercareQuiz>().Any(i => i.UtilizatorId == m.UtilizatorId && db.Set<TemaLectie>().Any(l => l.TemaId == t.Id && l.ParcursId == i.ParcursId)),
            PredatLa = db.Set<ProgresLectie>().Where(p => p.UtilizatorId == m.UtilizatorId && db.Set<TemaLectie>().Any(l => l.TemaId == t.Id && l.ParcursId == p.ParcursId)).Max(p => p.PromovatLa) });
}
