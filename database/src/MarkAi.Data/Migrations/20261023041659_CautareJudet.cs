using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class CautareJudet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cautare",
                schema: "mark_ai",
                table: "judet",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(translate(nume, 'ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-', 'aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn '))",
                stored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cautare",
                schema: "mark_ai",
                table: "judet");
        }
    }
}
