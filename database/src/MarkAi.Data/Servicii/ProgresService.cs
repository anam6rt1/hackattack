using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data.Servicii;

public record RezultatProgres(DateTime? PromovatLa, int XpCastigat);

public record NivelCurent(int Nivel, string Titlu, int XpTotal, int XpNivel, int? XpUrmatorNivel, string? TitluUrmator);

/// <summary>
/// Lecție progress and XP. A Lecție is completed only by passing its quiz; the XP for it is awarded once per user and Lecție,
/// guarded by the unique reward key in the database (safe under concurrent requests). Reading a chapter only records where
/// the student left off.
/// </summary>
public sealed class ProgresService(MarkAiDbContext db)
{
    public const int XpLectie = 100;
    public const int PuncteMaterieLectie = 10;

    public static string CheieRecompensaLectie(int parcursId, int utilizatorId) => $"lectie:{parcursId}:utilizator:{utilizatorId}";

    /// <summary>Records that the student read this chapter (the map recommends the one after the last read; "continue" leads there).</summary>
    public async Task AcceseazaAsync(int utilizatorId, int cursId, CancellationToken ct = default)
    {
        var parcursId = await db.Curs.Where(c => c.Id == cursId).Select(c => (int?)c.ParcursId).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Capitolul nu există.");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO mark_ai.progres_lectie (utilizator_id, parcurs_id, ultimul_curs_id, ultima_accesare)
            VALUES ({utilizatorId}, {parcursId}, {cursId}, now())
            ON CONFLICT (utilizator_id, parcurs_id) DO UPDATE SET ultimul_curs_id = excluded.ultimul_curs_id, ultima_accesare = now()
            """, ct);
    }

    /// <summary>The student passed the Lecție's quiz: completion time kept from the first pass, XP awarded once.</summary>
    public async Task<RezultatProgres> PromoveazaAsync(int utilizatorId, int parcursId, CancellationToken ct = default)
    {
        var materieId = await db.Set<Entities.Parcurs>().Where(p => p.Id == parcursId && p.Publicat).Select(p => (int?)p.MaterieId).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Lecția nu există.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO mark_ai.progres_lectie (utilizator_id, parcurs_id, promovat_la, ultima_accesare)
            VALUES ({utilizatorId}, {parcursId}, now(), now())
            ON CONFLICT (utilizator_id, parcurs_id) DO UPDATE SET
                promovat_la = COALESCE(progres_lectie.promovat_la, excluded.promovat_la),
                ultima_accesare = now()
            """, ct);
        var cheie = CheieRecompensaLectie(parcursId, utilizatorId);
        var xp = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO mark_ai.eveniment_recompensa (utilizator_id, materie_id, xp_acordat, puncte_materie, tip_eveniment, cheie_unica)
            VALUES ({utilizatorId}, {materieId}, {XpLectie}, {PuncteMaterieLectie}, 'lectie_completata', {cheie})
            ON CONFLICT (cheie_unica) DO NOTHING
            """, ct) == 1 ? XpLectie : 0;
        await tx.CommitAsync(ct);

        var promovatLa = await db.Set<Entities.ProgresLectie>().Where(x => x.UtilizatorId == utilizatorId && x.ParcursId == parcursId)
            .Select(x => x.PromovatLa).SingleAsync(ct);
        return new RezultatProgres(promovatLa, xp);
    }

    public async Task<NivelCurent> NivelAsync(int utilizatorId, CancellationToken ct = default)
    {
        var xp = await db.EvenimenteRecompensa.Where(e => e.UtilizatorId == utilizatorId).SumAsync(e => (int?)e.XpAcordat, ct) ?? 0;
        var niveluri = await db.NiveluriXp.AsNoTracking().OrderBy(n => n.XpNecesar).ToListAsync(ct);
        var curent = niveluri.LastOrDefault(n => n.XpNecesar <= xp);
        var urmator = niveluri.FirstOrDefault(n => n.XpNecesar > xp);
        return new NivelCurent(curent?.Nivel ?? 0, curent?.Titlu ?? "Începător", xp, curent?.XpNecesar ?? 0,
            urmator?.XpNecesar, urmator?.Titlu);
    }
}
