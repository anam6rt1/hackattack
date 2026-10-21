using Npgsql;
using Testcontainers.PostgreSql;

namespace MarkAi.Tests.Suport;

/// <summary>
/// The PostgreSQL server the tests run on, started once per process: a throwaway <c>postgres:18</c> container
/// (Testcontainers; on podman set DOCKER_HOST to its socket), or the server in <c>MARKAI_TEST_CONNECTION</c> when set
/// (a user with CREATEDB, never the real database). The schema is migrated and seeded once into a template database;
/// every <see cref="Harness"/> gets its own copy of it (<c>CREATE DATABASE … TEMPLATE</c>), which takes a fraction of a second.
/// </summary>
public static class BazaTest
{
    private static readonly SemaphoreSlim Blocaj = new(1, 1);
    private static PostgreSqlContainer? container;
    private static string? adminConexiune, sablon;

    /// <summary>A new database copied from the seeded template; returns its connection string and the admin one to drop it.</summary>
    public static async Task<(string Conexiune, string Admin, string Nume)> CopieNoua()
    {
        var (admin, numeSablon) = await Sablon();
        var nume = "markai_test_" + Guid.NewGuid().ToString("N");
        await using (var c = new NpgsqlConnection(admin))
        {
            await c.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{nume}\" TEMPLATE \"{numeSablon}\"", c).ExecuteNonQueryAsync();
        }
        return (new NpgsqlConnectionStringBuilder(admin) { Database = nume }.ConnectionString, admin, nume);
    }

    public static async Task Sterge(string admin, string nume)
    {
        await using var c = new NpgsqlConnection(admin); await c.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{nume}\" WITH (FORCE)", c).ExecuteNonQueryAsync();
    }

    private static async Task<(string Admin, string Sablon)> Sablon()
    {
        if (sablon is not null) return (adminConexiune!, sablon);
        await Blocaj.WaitAsync();
        try
        {
            if (sablon is not null) return (adminConexiune!, sablon);
            // Before the fixtures are hashed: every test account then signs in cheaply.
            MarkAi.Data.Servicii.Parole.PentruTeste(1000);
            var admin = Environment.GetEnvironmentVariable("MARKAI_TEST_CONNECTION");
            if (string.IsNullOrEmpty(admin))
            {
                // Throwaway data: no fsync; many connections, since every parallel test app has its own pool.
                container = new PostgreSqlBuilder("docker.io/library/postgres:18")
                    .WithCommand("-c", "fsync=off", "-c", "synchronous_commit=off", "-c", "full_page_writes=off", "-c", "max_connections=500")
                    .Build();
                await container.StartAsync();
                admin = container.GetConnectionString();
            }
            var nume = "markai_sablon_" + Guid.NewGuid().ToString("N");
            await using (var c = new NpgsqlConnection(admin))
            {
                await c.OpenAsync();
                await new NpgsqlCommand($"CREATE DATABASE \"{nume}\"", c).ExecuteNonQueryAsync();
            }
            await Fixturi.Populeaza(new NpgsqlConnectionStringBuilder(admin) { Database = nume }.ConnectionString);
            // A template cannot be copied while anyone is connected to it.
            NpgsqlConnection.ClearAllPools();
            (adminConexiune, sablon) = (admin, nume);
            return (admin, nume);
        }
        finally { Blocaj.Release(); }
    }

    /// <summary>Drops the template (on a shared server) or stops the container, at the end of the run.</summary>
    public static async Task Opreste()
    {
        if (container is not null) { await container.DisposeAsync(); container = null; }
        else if (sablon is not null) await Sterge(adminConexiune!, sablon);
        sablon = null;
    }
}
