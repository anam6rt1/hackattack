using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MarkAi.Data;

public sealed class MarkAiDbContextFactory : IDesignTimeDbContextFactory<MarkAiDbContext>
{
    public MarkAiDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("MARKAI_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Set MARKAI_CONNECTION_STRING before running database tools.");
        return new MarkAiDbContext(new DbContextOptionsBuilder<MarkAiDbContext>()
            .UseNpgsql(connection, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "mark_ai"))
            .Options);
    }
}

