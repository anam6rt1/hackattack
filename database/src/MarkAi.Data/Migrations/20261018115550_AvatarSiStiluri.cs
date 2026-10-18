using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvatarSiStiluri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar",
                schema: "mark_ai",
                table: "preferinte_utilizator",
                type: "text",
                nullable: true);

            // Stilurile desenelor din profil (Acvatic = interfața „insule” existentă). Sunt date, nu schemă;
            // ON CONFLICT le lasă neatinse dacă există deja.
            migrationBuilder.Sql("""
                INSERT INTO mark_ai.interfata (cod, nume, descriere, activa) VALUES
                    ('realist', 'Realist', 'Apă, lumină și nori ca în realitate', true),
                    ('pixel', 'Pixel', 'Joc retro pe 16 biți', true)
                ON CONFLICT (cod) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE mark_ai.preferinte_utilizator SET interfata_id = NULL
                    WHERE interfata_id IN (SELECT id FROM mark_ai.interfata WHERE cod IN ('realist', 'pixel'));
                DELETE FROM mark_ai.interfata i WHERE i.cod IN ('realist', 'pixel')
                    AND NOT EXISTS (SELECT 1 FROM mark_ai.regula_background r WHERE r.interfata_id = i.id);
                """);

            migrationBuilder.DropColumn(
                name: "avatar",
                schema: "mark_ai",
                table: "preferinte_utilizator");
        }
    }
}
