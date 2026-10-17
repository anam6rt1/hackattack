using Microsoft.EntityFrameworkCore;

if (args.Length is < 1 or > 2 || args[0] is not ("adopt-existing" or "migrate" or "verify" or "seed")
    || (args.Length == 2 && args[0] != "seed"))
{
    Console.WriteLine("Usage: dotnet run --project tools/MarkAi.Database -- adopt-existing|migrate|verify|seed [frontend-data-dir]");
    Console.WriteLine("Set MARKAI_CONNECTION_STRING in your environment. Never put passwords in Git.");
    return 1;
}
try
{
    var connection = Environment.GetEnvironmentVariable("MARKAI_CONNECTION_STRING")
        ?? throw new InvalidOperationException("MARKAI_CONNECTION_STRING is required.");
    if (args[0] == "verify") await Verification.Run(connection);
    else if (args[0] == "adopt-existing") await DatabaseTools.Adopt(connection);
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
