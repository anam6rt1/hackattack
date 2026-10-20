// Integration checks against the real API pipeline on throwaway PostgreSQL databases (one per group).
// Usage: MARKAI_TEST_CONNECTION=... dotnet run -- [grup ...]   (no group = all)
//        dotnet run -- --preview   keeps a seeded API running on 127.0.0.1:5081 for manual testing.
// Each phase of the expansion plan adds its own group (Verificari*.cs) to this list.
using Microsoft.Extensions.Hosting;

var grupuri = new Dictionary<string, Grup>
{
    ["infrastructura"] = new(VerificariInfrastructura.Ruleaza),
    ["twilio"] = new(VerificariTwilio.Ruleaza, CuTwilio: true, VerificariTwilio.Setari),
    ["setari"] = new(VerificariSetari.Ruleaza),
    ["securitate"] = new(VerificariSecuritate.Ruleaza),
    ["quiz"] = new(VerificariQuiz.Ruleaza),
};

if (args.Contains("--preview"))
{
    await using var previzualizare = await Harness.Porneste(ceasReal: true);
    Console.WriteLine($"Preview fixtures only: George/Irina/Admin; usernames george, irina, admin; password {Harness.ParolaTest}; API {Harness.Adresa}.");
    await previzualizare.App.WaitForShutdownAsync();
    return 0;
}

var alese = args.Where(a => !a.StartsWith("--")).ToList();
var necunoscute = alese.Where(a => !grupuri.ContainsKey(a)).ToList();
if (necunoscute.Count > 0)
{
    Console.Error.WriteLine($"Unknown group(s): {string.Join(", ", necunoscute)}. Available: {string.Join(", ", grupuri.Keys)}.");
    return 2;
}

var total = 0;
foreach (var (nume, grup) in grupuri.Where(g => alese.Count == 0 || alese.Contains(g.Key)))
{
    Console.WriteLine($"== {nume}");
    await using var h = await Harness.Porneste(cuTwilio: grup.CuTwilio, setari: grup.Setari);
    await grup.Ruleaza(h);
    total += h.Numar;
}
Console.WriteLine($"All {total} checks passed on isolated PostgreSQL. No real emails, calls or AI requests were made.");
return 0;

/// <summary>A group of checks; each runs on its own fresh database and app.</summary>
record Grup(Func<Harness, Task> Ruleaza, bool CuTwilio = false, IReadOnlyDictionary<string, string?>? Setari = null);
