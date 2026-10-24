using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations;

public partial class FisiereExcel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("fisier_lectie_tip_check", "fisier_lectie", "mark_ai");
        migrationBuilder.AddCheckConstraint("fisier_lectie_tip_check", "fisier_lectie", "tip IN ('pdf', 'docx', 'pptx', 'xlsx')", "mark_ai");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback is refused if XLSX rows exist, rather than deleting uploaded materials.
        migrationBuilder.DropCheckConstraint("fisier_lectie_tip_check", "fisier_lectie", "mark_ai");
        migrationBuilder.AddCheckConstraint("fisier_lectie_tip_check", "fisier_lectie", "tip IN ('pdf', 'docx', 'pptx')", "mark_ai");
    }
}
