using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

/// <summary>
/// Hard deletion of chapters and Lecții, in one transaction. Students' progress and quiz attempts on them go too; XP already
/// awarded stays (the XP log does not point at chapters). Links that only referenced the chapter are cleared: phone-tutor
/// sessions and tutoring rooms keep existing without it, learning-graph nodes are removed. The question options must go
/// before their questions (that key does not cascade). The AI generation log is kept: it only counts the daily limit.
/// Chapter files: their rows cascade with the chapter, and the stored files are removed once the deletion is committed.
/// </summary>
public sealed class StergereContinut(MarkAiDbContext db, Stocare stocare, ILogger<StergereContinut> log)
{
    public async Task StergeCapitoleAsync(int[] cursIds, CancellationToken ct)
    {
        if (cursIds.Length == 0) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var fisiere = await StergeInTranzactieAsync(cursIds, ct);
        await tx.CommitAsync(ct);
        foreach (var cheie in fisiere) stocare.Sterge(cheie);
        log.LogInformation("Chapters {Ids} deleted with {Fisiere} files", string.Join(",", cursIds), fisiere.Count);
    }

    public async Task StergeParcursAsync(int parcursId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var cursIds = await db.Curs.Where(c => c.ParcursId == parcursId).Select(c => c.Id).ToArrayAsync(ct);
        var fisiere = await StergeInTranzactieAsync(cursIds, ct);
        // Its view-only shares cascade.
        await db.Set<Parcurs>().Where(p => p.Id == parcursId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
        foreach (var cheie in fisiere) stocare.Sterge(cheie);
        log.LogInformation("Lecție {Id} deleted with {Count} chapters", parcursId, cursIds.Length);
    }

    /// <returns>The storage keys of the deleted chapters' files.</returns>
    private async Task<List<string>> StergeInTranzactieAsync(int[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return [];
        var fisiere = await db.Set<FisierCapitol>().Where(f => ids.Contains(f.CursId)).Select(f => f.Cheie).ToListAsync(ct);
        var sql = db.Database;
        await sql.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.trivia_varianta WHERE intrebare_id IN (SELECT id FROM mark_ai.trivia_intrebare WHERE curs_id = ANY({ids}))", ct);
        await sql.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.trivia_intrebare WHERE curs_id = ANY({ids})", ct);
        await sql.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.incercare_quiz WHERE curs_id = ANY({ids})", ct);
        await sql.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.progres_curs WHERE curs_id = ANY({ids})", ct);
        await sql.ExecuteSqlInterpolatedAsync($"UPDATE mark_ai.sesiune SET curs_id = NULL WHERE curs_id = ANY({ids})", ct);
        await sql.ExecuteSqlInterpolatedAsync($"UPDATE mark_ai.camera_tutoring SET curs_id = NULL WHERE curs_id = ANY({ids})", ct);
        await sql.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.muchie_graf WHERE curs_sursa_id = ANY({ids}) OR curs_destinatie_id = ANY({ids})", ct);
        await sql.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.graf_curs WHERE curs_id = ANY({ids})", ct);
        await sql.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.curs WHERE id = ANY({ids})", ct);
        return fisiere;
    }
}
