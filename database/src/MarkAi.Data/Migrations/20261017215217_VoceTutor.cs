using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class VoceTutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ton_tutor",
                schema: "mark_ai",
                table: "preferinte_utilizator",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "viteza_tutor",
                schema: "mark_ai",
                table: "preferinte_utilizator",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<string>(
                name: "voce_tutor",
                schema: "mark_ai",
                table: "preferinte_utilizator",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "preferinte_utilizator_ton_tutor_check",
                schema: "mark_ai",
                table: "preferinte_utilizator",
                sql: "ton_tutor BETWEEN -20 AND 20");

            migrationBuilder.AddCheckConstraint(
                name: "preferinte_utilizator_viteza_tutor_check",
                schema: "mark_ai",
                table: "preferinte_utilizator",
                sql: "viteza_tutor BETWEEN 70 AND 130");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "preferinte_utilizator_ton_tutor_check",
                schema: "mark_ai",
                table: "preferinte_utilizator");

            migrationBuilder.DropCheckConstraint(
                name: "preferinte_utilizator_viteza_tutor_check",
                schema: "mark_ai",
                table: "preferinte_utilizator");

            migrationBuilder.DropColumn(
                name: "ton_tutor",
                schema: "mark_ai",
                table: "preferinte_utilizator");

            migrationBuilder.DropColumn(
                name: "viteza_tutor",
                schema: "mark_ai",
                table: "preferinte_utilizator");

            migrationBuilder.DropColumn(
                name: "voce_tutor",
                schema: "mark_ai",
                table: "preferinte_utilizator");
        }
    }
}
