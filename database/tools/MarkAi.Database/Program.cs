using Microsoft.EntityFrameworkCore;

if (args.Length < 1 || args[0] is not ("adopt-existing" or "migrate" or "verify" or "seed" or "seteaza-parola")
    || (args[0] == "seteaza-parola" ? args.Length != 3 : args.Length > (args[0] == "seed" ? 2 : 1)))
{
    Console.WriteLine("Usage: dotnet run --project tools/MarkAi.Database -- adopt-existing|migrate|verify|seed [frontend-data-dir]");
    Console.WriteLine("       dotnet run --project tools/MarkAi.Database -- seteaza-parola <email-or-username> <new-password>");
    Console.WriteLine("Set MARKAI_CONNECTION_STRING in your environment. Never put passwords in Git.");
    return 1;
}
try
{
    var connection = Environment.GetEnvironmentVariable("MARKAI_CONNECTION_STRING")
        ?? throw new InvalidOperationException("MARKAI_CONNECTION_STRING is required.");
    if (args[0] == "verify") await Verification.Run(connection);
    else if (args[0] == "adopt-existing") await DatabaseTools.Adopt(connection);
    else if (args[0] == "seteaza-parola") await SeteazaParola(connection, args[1], args[2]);
    else if (args[0] == "seed") await Seed.Run(connection, args.Length == 2 ? args[1] : Path.Combine("..", "frontend", "data"));
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
