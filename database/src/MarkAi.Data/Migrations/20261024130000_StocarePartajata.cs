using Microsoft.EntityFrameworkCore.Migrations;

namespace MarkAi.Data.Migrations;

public partial class StocarePartajata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("fisier_lectie_cheie_key", "fisier_lectie", "mark_ai");
        migrationBuilder.CreateIndex("fisier_lectie_cheie_key", "fisier_lectie", "cheie", "mark_ai");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Refuse rollback while shared references exist; never delete lesson materials.
        migrationBuilder.DropIndex("fisier_lectie_cheie_key", "fisier_lectie", "mark_ai");
        migrationBuilder.CreateIndex("fisier_lectie_cheie_key", "fisier_lectie", "cheie", "mark_ai", unique: true);
    }
}
