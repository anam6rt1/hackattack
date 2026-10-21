// A seeded test API on 127.0.0.1:5081 for manual testing and the Playwright tests: the same app and fixtures as the
// integration tests, with the real clock and background jobs, the "jurnal" mail/SMS provider (codes on the admin page
// "Mesaje trimise") and the fake AI. The database is a throwaway copy (Testcontainers, or MARKAI_TEST_CONNECTION).
// Usage: dotnet run --project tools/MarkAi.Previzualizare [-- --port 5081]
using MarkAi.Tests.Suport;
using Microsoft.Extensions.Hosting;

var port = args.SkipWhile(a => a != "--port").Skip(1).Select(int.Parse).DefaultIfEmpty(5081).First();
var previzualizare = await Harness.Porneste(ceasReal: true, port: port);
Console.WriteLine($"Preview fixtures only: George/Irina/Admin/Director; usernames george, irina, admin, director; password {Harness.ParolaTest}; API {previzualizare.Adresa}.");
// The host stops on Ctrl+C or SIGTERM; then the database copy and the container go too.
await previzualizare.App.WaitForShutdownAsync();
await previzualizare.DisposeAsync();
await BazaTest.Opreste();
