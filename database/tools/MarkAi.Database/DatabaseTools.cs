using System.Reflection;
using MarkAi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

    private static async Task<string> Fingerprint(MarkAiDbContext db)
    {
        await using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("SchemaFingerprint.sql")!;
        var sql = await new StreamReader(resource).ReadToEndAsync();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    internal static async Task Adopt(string connection)
    {
        await using var target = Open(connection);
        var baseline = target.Database.GetMigrations().First();
        var applied = (await target.Database.GetAppliedMigrationsAsync()).ToArray();
        if (applied.Contains(baseline))
        {
            Console.WriteLine("Initial schema is already registered; no changes made.");
            return;
        }
        if (applied.Length != 0) throw new InvalidOperationException("Unknown migration history; refusing adoption.");
        var temporary = await CreateTemporary(connection);
        try
        {
            await using var reference = Open(temporary);
            await reference.GetService<IMigrator>().MigrateAsync(baseline);
            if (await Fingerprint(target) != await Fingerprint(reference))
                throw new InvalidOperationException("Existing schema differs from InitialSchema. No baseline registered.");
            await using var tx = await target.Database.BeginTransactionAsync();
            await target.Database.ExecuteSqlRawAsync(target.GetService<IHistoryRepository>().GetCreateIfNotExistsScript());
            var version = ProductInfo.GetVersion();
            await target.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO mark_ai."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ({baseline}, {version})
                """);
            await tx.CommitAsync();
            Console.WriteLine("Existing schema verified and registered. Application tables/data were preserved.");
        }
        finally { await DropTemporary(temporary, connection); }
    }
}
