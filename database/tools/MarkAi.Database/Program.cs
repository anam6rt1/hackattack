using Microsoft.EntityFrameworkCore;

if (args.Length < 1 || args[0] is not ("migrate" or "verify" or "seed" or "seteaza-parola" or "seed-localitati" or "organizatie-demo" or "date-test" or "date-volum")
    || (args[0] switch { "seteaza-parola" => args.Length != 3, "organizatie-demo" => args.Length is < 2 or > 5, "seed" or "seed-localitati" or "date-test" => args.Length > 2,
        "date-volum" => args.Length > 2 || (args.Length == 2 && args[1] != "--sterge"), _ => args.Length > 1 }))
{
    Console.WriteLine("Usage: dotnet run --project tools/MarkAi.Database -- migrate|verify|seed [frontend-data-dir]");
    Console.WriteLine("       dotnet run --project tools/MarkAi.Database -- seteaza-parola <email-or-username> <new-password>");
    Console.WriteLine("       dotnet run --project tools/MarkAi.Database -- seed-localitati [siruta.csv]   (counties and localities, SIRUTA)");
    Console.WriteLine("       dotnet run --project tools/MarkAi.Database -- organizatie-demo <admin-username> [name] [county-id] [locality-siruta]");
    Console.WriteLine("       dotnet run --project tools/MarkAi.Database -- date-test [api-storage-dir]   (test accounts and content; TEST databases only)");
    Console.WriteLine("       dotnet run --project tools/MarkAi.Database -- date-volum [--sterge]   (~51 000 marked accounts for the lists and the stress test; --sterge removes them)");
    Console.WriteLine("Set MARKAI_CONNECTION_STRING in your environment. Never put passwords in Git.");
    return 1;
}
try
{
    var connection = Environment.GetEnvironmentVariable("MARKAI_CONNECTION_STRING")
        ?? throw new InvalidOperationException("MARKAI_CONNECTION_STRING is required.");
    if (args[0] == "verify") await Verification.Run(connection);
    else if (args[0] == "seteaza-parola") await SeteazaParola(connection, args[1], args[2]);
    // Files go to the API's storage folder (src/MarkAi.Api/uploads when the API runs with `dotnet run` from database/).
    else if (args[0] == "date-test")
        await DateTest.Run(connection, Path.Combine("tools", "MarkAi.Database", "date"), args.Length == 2 ? args[1] : Path.Combine("src", "MarkAi.Api", "uploads"));
    else if (args[0] == "date-volum") await DateVolum.Run(connection, args.Length == 2);
    else if (args[0] == "seed") await Seed.Run(connection, args.Length == 2 ? args[1] : Path.Combine("..", "frontend", "data"));
    else if (args[0] == "seed-localitati")
    {
        await using var db = DatabaseTools.Open(connection);
        var (judete, localitati) = await MarkAi.Data.Servicii.ImportSiruta.ImportaAsync(db,
            args.Length == 2 ? args[1] : Path.Combine("tools", "MarkAi.Database", "date", "siruta_s1_2026.csv"));
        Console.WriteLine($"SIRUTA: {judete} counties, {localitati} localities.");
    }
    // Defaults: "Organizație Demo" in Bucharest (county 40), Sector 1 (SIRUTA 179141).
    else if (args[0] == "organizatie-demo")
        await OrganizatieDemo.Run(connection, args[1], args.Length > 2 ? args[2] : "Organizație Demo",
            args.Length > 3 ? int.Parse(args[3]) : 40, args.Length > 4 ? int.Parse(args[4]) : 179141);
    else
    {
        await using var db = DatabaseTools.Open(connection);
        await db.Database.MigrateAsync();
        Console.WriteLine("Database migrations applied.");
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    return 1;
}

// Sets a user's password, hashed exactly like the API does (there is no password-change page yet).
static async Task SeteazaParola(string connection, string cine, string parola)
{
    if (parola.Length < 8) throw new InvalidOperationException("The password must have at least 8 characters.");
    await using var db = DatabaseTools.Open(connection);
    var id = cine.ToLower();
    var u = await db.Utilizatori.SingleOrDefaultAsync(x => x.Email.ToLower() == id || x.Username.ToLower() == id)
        ?? throw new InvalidOperationException($"No user '{cine}'.");
    u.ParolaHash = MarkAi.Data.Servicii.Parole.Hash(u, parola);
    await db.SaveChangesAsync();
    Console.WriteLine($"Password updated for {u.Username} ({u.Email}).");
}
