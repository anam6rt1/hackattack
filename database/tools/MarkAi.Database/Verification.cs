using MarkAi.Data.Entities;
using MarkAi.Data.Trivia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

internal static class Verification
{
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }

    private static async Task<bool> Rejected(Func<Task> operation)
    {
        try { await operation(); return false; }
        catch (InvalidOperationException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
        catch (DbUpdateException) { return true; }
        catch (PostgresException) { return true; }
    }

    internal static async Task Run(string original)
    {
        var connection = await DatabaseTools.CreateTemporary(original);
        try
        {
            await using var db = DatabaseTools.Open(connection);
            await db.Database.MigrateAsync();
            Check(!db.Database.HasPendingModelChanges(), "model matches migration snapshot");
            Check((await db.Database.GetAppliedMigrationsAsync()).Count() == 2, "fresh database built from both C# migrations");
            var users = Enumerable.Range(1, 4).Select(i => new Utilizator
            { Nume = "Test " + i, Username = "test" + i, Email = $"test{i}@example.invalid" }).ToArray();
            var subject = new Materie { Denumire = "Test subject" };
            db.AddRange(users);
            db.Add(subject);
            await db.SaveChangesAsync();
            var question = new TriviaQuestion { SubjectId = subject.Id, Text = "2 + 2?" };
            db.Add(question);
            await db.SaveChangesAsync();
            var correct = new TriviaOption { QuestionId = question.Id, Text = "4", IsCorrect = true };
            var wrong = new TriviaOption { QuestionId = question.Id, Text = "5" };
            db.AddRange(correct, wrong);
            await db.SaveChangesAsync();
            var game = new TriviaService(DatabaseTools.Options(connection));

            var limited = await game.CreateRoomAsync(users[0].Id, subject.Id, 2);
            Check(limited.Code.Length == 6, "room code generated");
            var contenders = await Task.WhenAll(users.Skip(1).Take(2).Select(async u =>
                !await Rejected(async () => { await game.JoinRoomAsync(limited.Code, u.Id); })));
            Check(contenders.Count(x => x) == 1, "concurrent joins cannot exceed capacity");
            Check(await db.Set<TriviaParticipant>().CountAsync(p => p.RoomId == limited.Id) == 2, "host occupies one seat");

            var room = await game.CreateRoomAsync(users[0].Id, subject.Id, 4);
            await game.JoinRoomAsync(room.Code.ToLowerInvariant(), users[1].Id);
            await game.JoinRoomAsync(room.Code, users[3].Id);
            await game.JoinRoomAsync(room.Code, users[1].Id);
            Check(await db.Set<TriviaParticipant>().CountAsync(p => p.RoomId == room.Id) == 3, "join is idempotent");
            Check(await Rejected(async () => { await game.StartRoundAsync(room.Id, users[1].Id, question.Id); }), "only host starts rounds");
            var round = await game.StartRoundAsync(room.Id, users[0].Id, question.Id, 120);
            Check(await Rejected(async () => { await game.JoinRoomAsync(room.Code, users[2].Id); }), "late joining rejected");
            Check(await Rejected(async () => { await game.StartRoundAsync(room.Id, users[0].Id, question.Id); }), "overlapping rounds rejected");
            var roundChoices = await db.Set<TriviaRoundOption>().Where(x => x.RoundId == round.Id).ToListAsync();
            var yes = roundChoices.Single(x => x.IsCorrect).Id;
            var no = roundChoices.Single(x => !x.IsCorrect).Id;
            var answers = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
                !await Rejected(() => game.SubmitAnswerAsync(room.Id, round.Id, users[0].Id, yes))));
            Check(answers.Count(x => x) == 1, "concurrent duplicate answers: only one accepted");
            await game.SubmitAnswerAsync(room.Id, round.Id, users[1].Id, no);
            Check(await Rejected(() => game.SubmitAnswerAsync(room.Id, round.Id, users[2].Id, yes)), "nonparticipant answer rejected");
            var scores = await game.GetLeaderboardAsync(room.Id, users[0].Id);
            Check(scores.Single(x => x.UserId == users[0].Id).Score == 100
                && scores.Single(x => x.UserId == users[1].Id).Score == 0
                && scores.Single(x => x.UserId == users[3].Id).Score == 0, "server scores correct, incorrect and unanswered participants");
            Check(await Rejected(async () => { await game.GetLeaderboardAsync(room.Id, users[2].Id); }), "leaderboard access requires membership");

            correct.Text = "Changed bank option";
            question.Text = "Changed bank question";
            await db.SaveChangesAsync();
            Check((await db.Set<TriviaRound>().SingleAsync(x => x.Id == round.Id)).QuestionText == "2 + 2?"
                && (await db.Set<TriviaRoundOption>().SingleAsync(x => x.Id == yes)).Text == "4", "round snapshots survive question bank edits");
            Check(await Rejected(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE mark_ai.trivia_raspuns SET scor=99999 WHERE runda_id={round.Id}");
            }), "answer score cannot be edited");
            Check(await Rejected(async () =>
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE mark_ai.trivia_runda_varianta SET corecta=false WHERE id={yes}");
            }), "round answer key cannot be edited");

            // Database clock test: even a forged timestamp and score cannot bypass an expired deadline.
            var timedRoom = await game.CreateRoomAsync(users[0].Id, subject.Id);
            await game.JoinRoomAsync(timedRoom.Code, users[1].Id);
            var timed = await game.StartRoundAsync(timedRoom.Id, users[0].Id, question.Id, 1);
            var timedOption = await db.Set<TriviaRoundOption>().Where(x => x.RoundId == timed.Id && x.IsCorrect).SingleAsync();
            Check(await Rejected(() => game.SubmitAnswerAsync(room.Id, round.Id, users[3].Id, timedOption.Id)), "option from another round rejected");
            await db.Database.ExecuteSqlRawAsync("SELECT pg_sleep(1.2)");
            Check(await Rejected(async () => { await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO mark_ai.trivia_raspuns(runda_id,utilizator_id,camera_id,varianta_id,trimis_la,scor)
                VALUES ({timed.Id},{users[0].Id},{timedRoom.Id},{timedOption.Id},{timed.StartedAt},{99999})
                """); }), "late answer rejected despite forged timestamp and score");
            var next = await game.StartRoundAsync(timedRoom.Id, users[0].Id, question.Id, 120);
            Check(next.Number == 2, "next round allowed after deadline");
            await game.FinishRoomAsync(room.Id, users[0].Id);
            Check(await Rejected(() => game.SubmitAnswerAsync(room.Id, round.Id, users[3].Id, yes)), "finished rooms reject answers");

            // Roundtrip only on the disposable database, never on the real application database.
            await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().First());
            await db.Database.MigrateAsync();
            Check(await db.Utilizators.CountAsync() == 4, "trivia rollback/reapply preserves original application entities");

            // Exercise adoption with real application data and an absent EF history table.
            await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().First());
            // Reproduce the original SQL schema's UNIQUE constraint representation.
            await db.Database.ExecuteSqlRawAsync("DROP INDEX mark_ai.utilizator_username_key; ALTER TABLE mark_ai.utilizator ADD CONSTRAINT utilizator_username_key UNIQUE(username)");
            await db.Database.ExecuteSqlRawAsync("DROP TABLE mark_ai.\"__EFMigrationsHistory\"");
            await DatabaseTools.Adopt(connection);
            await DatabaseTools.Adopt(connection);
            await db.Database.MigrateAsync();
            Check(await db.Utilizators.CountAsync() == 4, "adopt existing schema and upgrade preserves data; adoption is repeatable");
            Check(await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_constraint WHERE connamespace='mark_ai'::regnamespace AND conname='utilizator_username_key'").SingleAsync() == 0,
                "legacy unique constraint normalized to EF unique index");
            Console.WriteLine("All integration checks passed against PostgreSQL.");
        }
        finally { await DatabaseTools.DropTemporary(connection, original); }
    }
}
