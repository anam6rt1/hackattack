using MarkAi.Data;
using MarkAi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Api.Servicii;

/// <summary>
/// Hard deletion of chapters and Lecții, in one transaction. Deleting a Lecție cascades in the database to its chapters,
/// quiz questions, files, quiz attempts and students' progress; XP already awarded stays (the XP log does not point at
/// content). Phone-tutor sessions and tutoring rooms only lose their link to the deleted chapters. The AI generation log is
/// kept: it only counts the daily limit. Stored files are removed once the deletion is committed.
/// </summary>
public sealed class StergereContinut(MarkAiDbContext db, Stocare stocare, ILogger<StergereContinut> log)
{
    public async Task StergeCapitoleAsync(int[] cursIds, CancellationToken ct)
    {
        if (cursIds.Length == 0) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await DezleagaCapitoleAsync(cursIds, ct);
        await db.Curs.Where(c => cursIds.Contains(c.Id)).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
        log.LogInformation("Chapters {Ids} deleted", string.Join(",", cursIds));
    }

    public async Task StergeParcursAsync(int parcursId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var fisiere = await db.Set<FisierLectie>().Where(f => f.ParcursId == parcursId).Select(f => f.Cheie).ToListAsync(ct);
        var cursIds = await db.Curs.Where(c => c.ParcursId == parcursId).Select(c => c.Id).ToArrayAsync(ct);
        await DezleagaCapitoleAsync(cursIds, ct);
        await db.Set<Parcurs>().Where(p => p.Id == parcursId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
        foreach (var cheie in fisiere) stocare.Sterge(cheie);
        log.LogInformation("Lecție {Id} deleted with {Capitole} chapters and {Fisiere} files", parcursId, cursIds.Length, fisiere.Count);
    }

    // These links do not cascade: the sessions and rooms outlive the chapter (the room's key also holds its subject).
    private async Task DezleagaCapitoleAsync(int[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) return;
        var sql = db.Database;
        await sql.ExecuteSqlInterpolatedAsync($"UPDATE mark_ai.sesiune SET curs_id = NULL WHERE curs_id = ANY({ids})", ct);
        await sql.ExecuteSqlInterpolatedAsync($"UPDATE mark_ai.camera_tutoring SET curs_id = NULL WHERE curs_id = ANY({ids})", ct);
    }
}
