using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class CautareInSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cautare_cont",
                schema: "mark_ai",
                table: "utilizator",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(translate(nume || ' ' || username || ' ' || email, 'ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-', 'aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn '))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "cautare_nume",
                schema: "mark_ai",
                table: "utilizator",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(translate(nume, 'ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-', 'aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn '))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "cautare",
                schema: "mark_ai",
                table: "organizatie",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(translate(nume, 'ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-', 'aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn '))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "cautare",
                schema: "mark_ai",
                table: "materie",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(translate(denumire, 'ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-', 'aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn '))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "idx_utilizator_cautare_cont_trgm",
                schema: "mark_ai",
                table: "utilizator",
                column: "cautare_cont")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_utilizator_cautare_nume_trgm",
                schema: "mark_ai",
                table: "utilizator",
                column: "cautare_nume")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_organizatie_cautare_trgm",
                schema: "mark_ai",
                table: "organizatie",
                column: "cautare")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_utilizator_cautare_cont_trgm",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropIndex(
                name: "idx_utilizator_cautare_nume_trgm",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropIndex(
                name: "idx_organizatie_cautare_trgm",
                schema: "mark_ai",
                table: "organizatie");

            migrationBuilder.DropColumn(
                name: "cautare_cont",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "cautare_nume",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "cautare",
                schema: "mark_ai",
                table: "organizatie");

            migrationBuilder.DropColumn(
                name: "cautare",
                schema: "mark_ai",
                table: "materie");
        }
    }
}
