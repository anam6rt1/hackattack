using MarkAi.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static class DatabaseTools
{
    internal static DbContextOptions<MarkAiDbContext> Options(string connection) =>
        new DbContextOptionsBuilder<MarkAiDbContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", "mark_ai")).Options;
    internal static MarkAiDbContext Open(string connection) => new(Options(connection));

    internal static async Task<string> CreateTemporary(string connection)
    {
        var name = "markai_verify_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(connection) { Database = name }.ConnectionString;
    }

    internal static async Task DropTemporary(string connection, string original)
    {
        var name = new NpgsqlConnectionStringBuilder(connection).Database!;
        if (!name.StartsWith("markai_verify_", StringComparison.Ordinal)
            || name == new NpgsqlConnectionStringBuilder(original).Database)
            throw new InvalidOperationException("Refusing to drop a non-verification database.");
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(original);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }
}
