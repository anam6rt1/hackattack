using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

public class TotalTeme { public int Total { get; set; } public int Predate { get; set; } }
public class GresealaLectie { public string Titlu { get; set; } = ""; public int Gresite { get; set; } }
public record RaportLunar(string Luna, DateTime GeneratLa, int EleviActivi, int LectiiTerminate, TotalTeme Teme, int SedinteTinute, List<GresealaLectie> LectiiCuGreseli);

public static class Rapoarte
{
    // Current approved students form the reporting cohort. Historical membership is not stored.
    public static async Task<TotalTeme> Teme(MarkAiDbContext db, int org, int uid, DateTime start, DateTime end, CancellationToken ct) =>
        await db.Database.SqlQuery<TotalTeme>($"""
        SELECT count(*)::int AS "Total", count(*) FILTER (WHERE NOT EXISTS (
            SELECT 1 FROM mark_ai.tema_lectie l WHERE l.tema_id=t.id AND NOT EXISTS (
                SELECT 1 FROM mark_ai.progres_lectie p WHERE p.parcurs_id=l.parcurs_id AND p.utilizator_id=m.utilizator_id AND p.promovat_la < {end})))::int AS "Predate"
        FROM mark_ai.tema t JOIN mark_ai.membru_organizatie m ON m.organizatie_id=t.organizatie_id
        JOIN mark_ai.utilizator u ON u.id=m.utilizator_id
        WHERE ({org}=0 OR t.organizatie_id={org}) AND ({uid}=0 OR m.utilizator_id={uid})
          AND m.rol='elev' AND m.stare='aprobat' AND u.stergere_ceruta_la IS NULL
          AND NOT t.anulata AND t.termen_la >= {start} AND t.termen_la < {end}
          AND EXISTS (SELECT 1 FROM mark_ai.tema_lectie l WHERE l.tema_id=t.id)
          AND (t.toata_organizatia OR EXISTS (SELECT 1 FROM mark_ai.tema_grupa g
              JOIN mark_ai.grupa_membru gm ON gm.grupa_id=g.grupa_id WHERE g.tema_id=t.id AND gm.elev_id=m.utilizator_id))
        """).SingleAsync(ct);

    public static async Task<RaportLunar> Lunar(MarkAiDbContext db, int org, DateTime start, DateTime end, DateTime now, CancellationToken ct)
    {
        var elevi = db.Set<MembruOrganizatie>().Where(m => m.OrganizatieId == org && m.Rol == "elev" && m.Stare == "aprobat" && m.Utilizator.StergereCerutaLa == null).Select(m => m.UtilizatorId);
        var activi = await db.Set<ProgresLectie>().Where(p => elevi.Contains(p.UtilizatorId) && p.UltimaAccesare >= start && p.UltimaAccesare < end).Select(p => p.UtilizatorId)
            .Union(db.Set<RaspunsStatistica>().Where(r => elevi.Contains(r.UtilizatorId) && r.RaspunsLa >= start && r.RaspunsLa < end).Select(r => r.UtilizatorId))
            .Union(db.Rezervari.Where(r => elevi.Contains(r.ElevId) && r.Camera.FinalizatLa >= start && r.Camera.FinalizatLa < end && r.Status != "anulata").Select(r => r.ElevId)).CountAsync(ct);
        var lectii = await db.Set<ProgresLectie>().CountAsync(p => elevi.Contains(p.UtilizatorId) && p.PromovatLa >= start && p.PromovatLa < end, ct);
        var teme = await Teme(db, org, 0, start, end, ct);
        var sedinte = await db.CamereTutoring.CountAsync(s => s.FinalizatLa >= start && s.FinalizatLa < end && s.Status != "anulata" && s.Rezervari.Any(r => elevi.Contains(r.ElevId) && r.Status != "anulata"), ct);
        var greseli = await db.Set<RaspunsStatistica>().Where(r => elevi.Contains(r.UtilizatorId) && !r.Corect && r.RaspunsLa >= start && r.RaspunsLa < end)
            .Join(db.Set<Intrebare>().Where(q => q.ParcursId != null), r => r.IntrebareId, q => q.Id, (r, q) => q.Parcurs!)
            .GroupBy(p => new { p.Id, p.Titlu }).Select(g => new GresealaLectie { Titlu = g.Key.Titlu, Gresite = g.Count() })
            .OrderByDescending(g => g.Gresite).ThenBy(g => g.Titlu).Take(10).ToListAsync(ct);
        return new(TimeZoneInfo.ConvertTimeFromUtc(start, TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest")).ToString("yyyy-MM"), now, activi, lectii, teme, sedinte, greseli);
    }
}
