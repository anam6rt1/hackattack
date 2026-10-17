using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace MarkAi.Data.Trivia;

public record PublicOption(int Id, string Text);
public record PublicRound(int Id, int Number, string Question, DateTime StartedAt, DateTime DeadlineAt,
    IReadOnlyList<PublicOption> Options);
public record LeaderboardEntry(int UserId, long Score);

/// <summary>
/// Call from an authenticated backend, using the authenticated user id (never a client-supplied identity).
/// Each operation owns a short-lived context; PostgreSQL owns the clock and validates answers.
/// </summary>
public sealed class TriviaService(DbContextOptions<MarkAiDbContext> options)
{
    private MarkAiDbContext Open() => new(options);
    private static Task<DateTime> Now(MarkAiDbContext db, CancellationToken ct) =>
        db.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);

    private static async Task<TriviaRoom> LockRoom(MarkAiDbContext db, int roomId, CancellationToken ct) =>
        await db.Set<TriviaRoom>().FromSqlInterpolated(
            $"SELECT * FROM mark_ai.trivia_camera WHERE id = {roomId} FOR UPDATE").SingleAsync(ct);

    public async Task<TriviaRoom> CreateRoomAsync(int hostId, int? subjectId = null, int capacity = 8, CancellationToken ct = default)
    {
        if (capacity is < 2 or > 16) throw new ArgumentOutOfRangeException(nameof(capacity));
        // The unique database index handles the unlikely invitation-code collision.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var db = Open();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var room = new TriviaRoom
            {
                Code = string.Concat(Enumerable.Range(0, 6).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)])),
                HostId = hostId,
                SubjectId = subjectId,
                Capacity = capacity
            };
            try
            {
                db.Add(room);
                await db.SaveChangesAsync(ct);
                db.Add(new TriviaParticipant { RoomId = room.Id, UserId = hostId });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return room;
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                await tx.RollbackAsync(ct);
            }
        }
        throw new InvalidOperationException("Could not allocate a unique room code. Retry.");
    }

    public async Task<int> JoinRoomAsync(string code, int userId, CancellationToken ct = default)
    {
        code = code.Trim().ToUpperInvariant();
        await using var db = Open();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await db.Set<TriviaRoom>().FromSqlInterpolated(
            $"SELECT * FROM mark_ai.trivia_camera WHERE cod = {code} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Room not found.");
        if (await db.Set<TriviaParticipant>().AnyAsync(x => x.RoomId == room.Id && x.UserId == userId, ct))
            return room.Id; // Retrying a successful join is harmless.
        if (room.Status != "waiting") throw new InvalidOperationException("The room has already started or finished.");
        if (await db.Set<TriviaParticipant>().CountAsync(x => x.RoomId == room.Id, ct) >= room.Capacity)
            throw new InvalidOperationException("Room is full.");
        db.Add(new TriviaParticipant { RoomId = room.Id, UserId = userId });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return room.Id;
    }

    public async Task<PublicRound> StartRoundAsync(int roomId, int hostId, int questionId, int seconds = 30,
        int points = 100, CancellationToken ct = default)
    {
        if (seconds is < 1 or > 300) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (points is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(points));
        await using var db = Open();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(db, roomId, ct);
        if (room.HostId != hostId) throw new UnauthorizedAccessException("Only the host may start rounds.");
        if (room.Status == "finished") throw new InvalidOperationException("Room is finished.");
        if (await db.Set<TriviaParticipant>().CountAsync(x => x.RoomId == roomId, ct) < 2)
            throw new InvalidOperationException("At least two participants are required.");
        var now = await Now(db, ct);
        if (await db.Set<TriviaRound>().AnyAsync(x => x.RoomId == roomId && x.DeadlineAt > now, ct))
            throw new InvalidOperationException("The previous round is still open.");
        var question = await db.Set<TriviaQuestion>().SingleAsync(x => x.Id == questionId && x.Active, ct);
        if (room.SubjectId is not null && room.SubjectId != question.SubjectId)
            throw new InvalidOperationException("Question belongs to a different subject.");
        var choices = await db.Set<TriviaOption>().Where(x => x.QuestionId == questionId).OrderBy(x => x.Id).ToListAsync(ct);
        if (choices.Count < 2 || choices.Count(x => x.IsCorrect) != 1)
            throw new InvalidOperationException("Question needs at least two options and exactly one correct option.");
        var number = (await db.Set<TriviaRound>().Where(x => x.RoomId == roomId).MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;
        var round = new TriviaRound
        {
            RoomId = roomId,
            Number = number,
            QuestionId = questionId,
            QuestionText = question.Text,
            StartedAt = await Now(db, ct),
            Points = points
        };
        round.DeadlineAt = round.StartedAt.AddSeconds(seconds);
        db.Add(round);
        room.Status = "active";
        await db.SaveChangesAsync(ct);
        var snapshots = choices.Select(x => new TriviaRoundOption
        { RoundId = round.Id, Text = x.Text, IsCorrect = x.IsCorrect }).ToArray();
        db.AddRange(snapshots);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new PublicRound(round.Id, number, round.QuestionText, round.StartedAt, round.DeadlineAt,
            snapshots.Select(x => new PublicOption(x.Id, x.Text)).ToArray());
    }

    public async Task SubmitAnswerAsync(int roomId, int roundId, int userId, int optionId, CancellationToken ct = default)
    {
        await using var db = Open();
        // The trigger replaces SubmittedAt and Score, checks the server clock, and rejects late/duplicate answers.
        db.Add(new TriviaAnswer { RoomId = roomId, RoundId = roundId, UserId = userId, OptionId = optionId });
        await db.SaveChangesAsync(ct);
    }

    public async Task FinishRoomAsync(int roomId, int hostId, CancellationToken ct = default)
    {
        await using var db = Open();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var room = await LockRoom(db, roomId, ct);
        if (room.HostId != hostId) throw new UnauthorizedAccessException("Only the host may finish the room.");
        if (room.Status != "finished")
        {
            room.Status = "finished";
            room.FinishedAt = await Now(db, ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(int roomId, int requestingUserId, CancellationToken ct = default)
    {
        await using var db = Open();
        if (!await db.Set<TriviaParticipant>().AnyAsync(x => x.RoomId == roomId && x.UserId == requestingUserId, ct))
            throw new UnauthorizedAccessException("Only room participants may view its leaderboard.");
        var rows = await db.Set<TriviaParticipant>().Where(x => x.RoomId == roomId)
            .Select(p => new
            {
                p.UserId,
                Score =
                db.Set<TriviaAnswer>().Where(a => a.RoomId == roomId && a.UserId == p.UserId).Sum(a => (long?)a.Score) ?? 0
            })
            .OrderByDescending(x => x.Score).ThenBy(x => x.UserId).ToListAsync(ct);
        return rows.Select(x => new LeaderboardEntry(x.UserId, x.Score)).ToArray();
    }
}
