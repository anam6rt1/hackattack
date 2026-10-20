using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data.Servicii;

public enum ActivitateCurs { Video, Materiale, Quiz }

public record RezultatProgres(bool VideoVazut, bool MaterialeCitite, bool QuizPromovat, DateTime? CompletatLa, int XpCastigat);

public record NivelCurent(int Nivel, string Titlu, int XpTotal, int XpNivel, int? XpUrmatorNivel, string? TitluUrmator);

/// <summary>
/// Course progress and XP. A course is completed when its quiz is passed; the XP for it is awarded once
/// per user and course, guarded by the unique reward key in the database (safe under concurrent requests).
/// </summary>
public sealed class ProgresService(MarkAiDbContext db)
{
    public const int XpCurs = 100;
    public const int PuncteMaterieCurs = 10;

    public static string CheieRecompensaCurs(int cursId, int utilizatorId) => $"curs:{cursId}:utilizator:{utilizatorId}";

    public async Task<RezultatProgres> MarcheazaAsync(int utilizatorId, int cursId, ActivitateCurs activitate, CancellationToken ct = default)
    {
        var materieId = await db.Curs.Where(c => c.Id == cursId && c.Publicat && c.Parcurs.Publicat).Select(c => (int?)c.MaterieId).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Cursul nu există.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var video = activitate == ActivitateCurs.Video;
        var materiale = activitate == ActivitateCurs.Materiale;
        var quiz = activitate == ActivitateCurs.Quiz;
        // Upsert: flags only ever turn on; completion time is kept from the first pass.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO mark_ai.progres_curs (utilizator_id, curs_id, video_vazut, materiale_citite, quiz_promovat, completat_la, ultima_accesare)
            VALUES ({utilizatorId}, {cursId}, {video}, {materiale}, {quiz}, CASE WHEN {quiz} THEN now() END, now())
            ON CONFLICT (utilizator_id, curs_id) DO UPDATE SET
                video_vazut = progres_curs.video_vazut OR excluded.video_vazut,
                materiale_citite = progres_curs.materiale_citite OR excluded.materiale_citite,
                quiz_promovat = progres_curs.quiz_promovat OR excluded.quiz_promovat,
                completat_la = COALESCE(progres_curs.completat_la, excluded.completat_la),
                ultima_accesare = now()
            """, ct);

        var xp = 0;
        if (quiz)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO mark_ai.utilizator_materie (utilizator_id, materie_id) VALUES ({utilizatorId}, {materieId})
                ON CONFLICT DO NOTHING
                """, ct);
            var cheie = CheieRecompensaCurs(cursId, utilizatorId);
            xp = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO mark_ai.eveniment_recompensa (utilizator_id, materie_id, xp_acordat, puncte_materie, tip_eveniment, cheie_unica)
                VALUES ({utilizatorId}, {materieId}, {XpCurs}, {PuncteMaterieCurs}, 'curs_completat', {cheie})
                ON CONFLICT (cheie_unica) DO NOTHING
                """, ct) == 1 ? XpCurs : 0;
        }
        await tx.CommitAsync(ct);

        var p = await db.Set<Entities.ProgresCurs>().AsNoTracking()
            .SingleAsync(x => x.UtilizatorId == utilizatorId && x.CursId == cursId, ct);
        return new RezultatProgres(p.VideoVazut, p.MaterialeCitite, p.QuizPromovat, p.CompletatLa, xp);
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
