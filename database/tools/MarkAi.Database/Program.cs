using Microsoft.EntityFrameworkCore;

if (args.Length != 1 || args[0] is not ("adopt-existing" or "migrate" or "verify"))
{
    Console.WriteLine("Usage: dotnet run --project tools/MarkAi.Database -- adopt-existing|migrate|verify");
    Console.WriteLine("Set MARKAI_CONNECTION_STRING in your environment. Never put passwords in Git.");
    return 1;
}
try
{
    var connection = Environment.GetEnvironmentVariable("MARKAI_CONNECTION_STRING")
        ?? throw new InvalidOperationException("MARKAI_CONNECTION_STRING is required.");
    if (args[0] == "verify") await Verification.Run(connection);
    else if (args[0] == "adopt-existing") await DatabaseTools.Adopt(connection);
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
