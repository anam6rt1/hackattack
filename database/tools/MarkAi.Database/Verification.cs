using MarkAi.Data.Entities;
using MarkAi.Data.Servicii;
using Microsoft.EntityFrameworkCore;
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
        catch (Conflict) { return true; }
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
            Check((await db.Database.GetAppliedMigrationsAsync()).Count() == db.Database.GetMigrations().Count(), "fresh database built from all C# migrations");
            Check(await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_extension WHERE extname = 'pg_trgm'").SingleAsync() == 1,
                "pg_trgm is installed for the search indexes");
            var users = Enumerable.Range(1, 4).Select(i => new Utilizator
            { Nume = "Test " + i, Username = "test" + i, Email = $"test{i}@example.invalid" }).ToArray();
            var subject = new Materie { Denumire = "Test subject" };
            db.AddRange(users);
            db.Add(subject);
            await db.SaveChangesAsync();
            var question = new Intrebare { MaterieId = subject.Id, Text = "2 + 2?",
                Variante = [new VariantaIntrebare { Text = "4", Corecta = true }, new VariantaIntrebare { Text = "5" }] };
            db.Add(question);
            await db.SaveChangesAsync();
            Check(await Rejected(async () =>
            {
                db.Add(new VariantaIntrebare { IntrebareId = question.Id, Text = "Four", Corecta = true });
                await db.SaveChangesAsync();
            }), "a question has at most one correct option");
            db.ChangeTracker.Clear();
            var path = new Parcurs { MaterieId = subject.Id, Titlu = "Test path", CreatLa = DateTime.UtcNow };
            db.Add(path);
            await db.SaveChangesAsync();
            var course = new Curs { MaterieId = subject.Id, ParcursId = path.Id, Titlu = "Test course", Capitol = "Test",
                Materiale = [new SectiuneMaterial { Titlu = "Intro", Text = "Text" }] };
            var otherSubject = new Materie { Denumire = "Other subject" };
            db.AddRange(course, otherSubject);
            await db.SaveChangesAsync();
            Check(await Rejected(async () =>
            {
                db.Add(new Curs { MaterieId = otherSubject.Id, ParcursId = path.Id, Titlu = "Wrong subject" });
                await db.SaveChangesAsync();
            }), "a chapter's subject must match its Lecție's subject");
            db.ChangeTracker.Clear();
            Check(await Rejected(async () =>
            {
                db.Add(new Parcurs { MaterieId = subject.Id, Titlu = "Org-only without organisation", Vizibilitate = Parcurs.DoarOrganizatie, CreatLa = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }), "an organisation-only Lecție needs an organisation");
            db.ChangeTracker.Clear();
            Check((await db.Curs.SingleAsync(c => c.Id == course.Id)).Materiale.Single().Titlu == "Intro", "course note sections roundtrip as JSON");
            db.Add(new Intrebare { MaterieId = subject.Id, ParcursId = path.Id, Text = "Lecție question?", Sursa = Intrebare.SursaAi,
                Variante = [new VariantaIntrebare { Text = "Da", Corecta = true }, new VariantaIntrebare { Text = "Nu" }] });
            await db.SaveChangesAsync();
            Check(await Rejected(async () =>
            {
                db.Add(new Intrebare { MaterieId = otherSubject.Id, ParcursId = path.Id, Text = "Wrong subject?" });
                await db.SaveChangesAsync();
            }), "a question's Lecție must belong to the question's subject");
            db.ChangeTracker.Clear();
            Check(await Rejected(async () =>
            {
                db.Add(new Intrebare { MaterieId = subject.Id, Tip = Intrebare.Estimare, Text = "Year?" });
                await db.SaveChangesAsync();
            }), "estimate questions require a numeric answer");
            db.ChangeTracker.Clear();
            FisierLectie Fisier(string tip) => new() { ParcursId = path.Id, Nume = "a." + tip, Tip = tip, Cheie = $"lectii/{Guid.NewGuid():N}.{tip}",
                MarimeOcteti = 10, Sha256 = "0", IncarcatLa = DateTime.UtcNow };
            db.AddRange(Fisier(FisierLectie.Pdf), Fisier(FisierLectie.Docx));
            await db.SaveChangesAsync();
            Check(await Rejected(async () => { db.Add(Fisier(FisierLectie.Pdf)); await db.SaveChangesAsync(); }), "a Lecție has at most one file of each type");
            db.ChangeTracker.Clear();

            // Lecție completion: concurrent quiz passes award the Lecție's XP exactly once and keep the first completion time.
            var xpAwards = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
            {
                await using var ctx = DatabaseTools.Open(connection);
                return (await new ProgresService(ctx).PromoveazaAsync(users[1].Id, path.Id)).XpCastigat;
            }));
            Check(xpAwards.Sum() == ProgresService.XpLectie, "concurrent Lecție completions award XP once");
            var primaData = (await db.Set<ProgresLectie>().AsNoTracking().SingleAsync(p => p.UtilizatorId == users[1].Id && p.ParcursId == path.Id)).PromovatLa;
            await new ProgresService(db).AcceseazaAsync(users[1].Id, course.Id);
            await new ProgresService(db).PromoveazaAsync(users[1].Id, path.Id);
            var progress = await db.Set<ProgresLectie>().AsNoTracking().SingleAsync(p => p.UtilizatorId == users[1].Id && p.ParcursId == path.Id);
            Check(progress.PromovatLa == primaData && progress.UltimulCursId == course.Id, "progress keeps the first completion time and the last chapter opened");

            // Tutoring: a full slot rejects extra concurrent bookings; pro bono slots need no tariff.
            var price = new TarifFix { Suma = 80, Moneda = "RON", DurataMinute = 60, Activ = true };
            db.Add(price);
            db.Add(new ProfilMentor { UtilizatorId = users[0].Id, StareVerificare = RezervareService.MentorAprobat, OnorariuNegociat = 50 });
            await db.SaveChangesAsync();
            var slot = new CameraTutoring { MentorId = users[0].Id, MaterieId = subject.Id, TarifId = price.Id, Titlu = "Slot", Capacitate = 1,
                Status = RezervareService.SlotDeschis, InceputLa = DateTime.UtcNow.AddDays(1), FinalizatLa = DateTime.UtcNow.AddDays(1).AddHours(1) };
            db.Add(slot);
            await db.SaveChangesAsync();
            var bookings = await Task.WhenAll(users.Skip(1).Take(3).Select(async u =>
            {
                await using var ctx = DatabaseTools.Open(connection);
                return !await Rejected(async () => { await new RezervareService(ctx).RezervaAsync(u.Id, slot.Id); });
            }));
            Check(bookings.Count(x => x) == 1, "concurrent bookings cannot exceed slot capacity");
            var payout = await db.ComisioaneMentor.SingleAsync(c => c.Rezervare.CameraId == slot.Id);
            Check(payout.SumaComision == 50 && payout.TarifBaza == 80, "mentor payout is the negotiated fee");
            Check(await Rejected(async () =>
            {
                db.Add(new CameraTutoring { MentorId = users[0].Id, MaterieId = subject.Id, Titlu = "Bad", Capacitate = 1, Status = "deschisa", ProBono = false });
                await db.SaveChangesAsync();
            }), "paid slots require a tariff");
            db.ChangeTracker.Clear();
            var proBono = new CameraTutoring { MentorId = users[0].Id, MaterieId = subject.Id, Titlu = "Pro bono", Capacitate = 2, ProBono = true,
                Status = RezervareService.SlotDeschis, InceputLa = DateTime.UtcNow.AddDays(2) };
            db.Add(proBono);
            await db.SaveChangesAsync();
            var free = await new RezervareService(db).RezervaAsync(users[1].Id, proBono.Id);
            Check(free.TarifAchitat == 0 && free.ComisionMentor is null, "pro bono booking is free and has no payout");
            db.ChangeTracker.Clear();

            // Deleting a Lecție cascades to its chapters, questions (with options), files, progress and quiz attempts; XP stays.
            db.Add(new IncercareQuiz { UtilizatorId = users[1].Id, ParcursId = path.Id, IntrebariIds = [], Raspunsuri = [], InceputLa = DateTime.UtcNow });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM mark_ai.parcurs WHERE id = {path.Id}");
            Check(!await db.Curs.AnyAsync(c => c.ParcursId == path.Id) && !await db.Set<Intrebare>().AnyAsync(q => q.ParcursId == path.Id)
                && !await db.Set<FisierLectie>().AnyAsync() && !await db.Set<ProgresLectie>().AnyAsync() && !await db.Set<IncercareQuiz>().AnyAsync()
                && await db.Set<VariantaIntrebare>().CountAsync() == 2, "deleting a Lecție removes its chapters, questions, files, progress and attempts");
            Check(await db.EvenimenteRecompensa.AnyAsync(e => e.UtilizatorId == users[1].Id), "XP already awarded stays after the Lecție is deleted");

            Console.WriteLine("All integration checks passed against PostgreSQL.");
        }
        finally { await DatabaseTools.DropTemporary(connection, original); }
    }
}
