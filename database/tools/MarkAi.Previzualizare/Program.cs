// A seeded test API on 127.0.0.1:5081 for manual testing and the Playwright tests: the same app and fixtures as the
// integration tests, with the real clock and background jobs, the "jurnal" mail/SMS provider (codes on the admin page
// "Mesaje trimise") and the fake AI. The database is a throwaway copy (Testcontainers, or MARKAI_TEST_CONNECTION).
// Usage: dotnet run --project tools/MarkAi.Previzualizare [-- --port 5081] [--e2e]
// --e2e (the Playwright tests): no practical rate limits (every test signs in from the same address) and no pause between quizzes.
using MarkAi.Tests.Suport;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

var port = args.SkipWhile(a => a != "--port").Skip(1).Select(int.Parse).DefaultIfEmpty(5081).First();
var e2e = args.Contains("--e2e");
var setari = e2e ? new Dictionary<string, string?>
{
    ["Limite:AutentificariPeMinut"] = "10000", ["Limite:InregistrariPeOra"] = "10000", ["Limite:EmailuriPeOra"] = "10000",
    ["Limite:SmsPeOra"] = "10000", ["Limite:CereriPeMinut"] = "100000", ["Limite:CereriAiPeMinut"] = "10000", ["Sms:MaxPeZi"] = "10000",
    ["Limite:PauzaQuizSecunde"] = "0", ["Limite:ContactPeOra"] = "10000",
} : null;
var conexiuneExistenta = Environment.GetEnvironmentVariable("MARKAI_PREVIEW_CONNECTION");
var directorExistent = Environment.GetEnvironmentVariable("MARKAI_PREVIEW_STORAGE");
var previzualizare = await Harness.Porneste(ceasReal: true, port: port, setari: setari,
    existenta: conexiuneExistenta is not null && directorExistent is not null ? (conexiuneExistenta, directorExistent) : null);
// What `seed` sets up for the price and the appearance (styles, themes), so sessions can be paid and a style can be saved;
// then scheduled sessions (the same as date-test), so Profesori, bookings and Acasă have something to show. director teaches
// no subject, so she stays a professor without sessions.
await previzualizare.Db(async db =>
{
    await db.Database.MigrateAsync();
    await Seed.ReferintaAsync(db);
    await SedinteTest.AdaugaAsync(db, ["irina", "director"], ["george"], DateTime.UtcNow);
});
Console.WriteLine($"Preview fixtures only: George/Irina/Admin/Director (+ scheduled sessions); usernames george, irina, admin, director; password {Harness.ParolaTest}; API {previzualizare.Adresa}.");
// The host stops on Ctrl+C or SIGTERM; then the database copy and the container go too.
await previzualizare.App.WaitForShutdownAsync();
await previzualizare.DisposeAsync();
await BazaTest.Opreste();
