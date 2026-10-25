using MarkAi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MarkAi.Tests.Unit;

public class MigrareExcelTests
{
    [Fact]
    public void MigrareaExcelEsteDescoperitaSiModelulEsteSincronizat()
    {
        // SQL generation and model comparison only: never connects to a database.
        using var db = new MarkAiDbContext(new DbContextOptionsBuilder<MarkAiDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=nefolosita;Username=nefolosit").Options);
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        var sql = db.GetService<IMigrator>().GenerateScript("20261023041659_CautareJudet", "20261024083000_FisiereExcel");
        sql.ShouldContain("xlsx");
        sql.ShouldContain("fisier_lectie_tip_check");
        sql.ShouldNotContain("DELETE FROM");
    }
}
