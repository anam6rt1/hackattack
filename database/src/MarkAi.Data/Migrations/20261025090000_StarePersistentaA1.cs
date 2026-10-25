using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class StarePersistentaA1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ultima_cerere_apel",
                schema: "mark_ai",
                table: "utilizator",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "actualizat",
                schema: "mark_ai",
                table: "generare_ai",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "intrebari_generate",
                schema: "mark_ai",
                table: "generare_ai",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "mesaj",
                schema: "mark_ai",
                table: "generare_ai",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stare",
                schema: "mark_ai",
                table: "generare_ai",
                type: "text",
                nullable: false,
                defaultValue: "gata");

            migrationBuilder.CreateIndex(
                name: "IX_generare_ai_stare",
                schema: "mark_ai",
                table: "generare_ai",
                column: "stare",
                filter: "stare IN ('in_asteptare', 'in_lucru')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_generare_ai_stare",
                schema: "mark_ai",
                table: "generare_ai");

            migrationBuilder.DropColumn(
                name: "ultima_cerere_apel",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "actualizat",
                schema: "mark_ai",
                table: "generare_ai");

            migrationBuilder.DropColumn(
                name: "intrebari_generate",
                schema: "mark_ai",
                table: "generare_ai");

            migrationBuilder.DropColumn(
                name: "mesaj",
                schema: "mark_ai",
                table: "generare_ai");

            migrationBuilder.DropColumn(
                name: "stare",
                schema: "mark_ai",
                table: "generare_ai");
        }
    }
}
