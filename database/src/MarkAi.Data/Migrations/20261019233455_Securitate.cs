using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class Securitate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "versiune_sesiune",
                schema: "mark_ai",
                table: "utilizator",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "cheie_expira_la",
                schema: "mark_ai",
                table: "sesiune",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cheie_hash",
                schema: "mark_ai",
                table: "sesiune",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "durata_secunde",
                schema: "mark_ai",
                table: "sesiune",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "generare_ai",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    curs_id = table.Column<int>(type: "integer", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("generare_ai_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "incercare_quiz",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    curs_id = table.Column<int>(type: "integer", nullable: false),
                    intrebari_ids = table.Column<int[]>(type: "integer[]", nullable: false),
                    raspunsuri = table.Column<int[]>(type: "integer[]", nullable: false),
                    corecte = table.Column<int>(type: "integer", nullable: false),
                    inceput_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    finalizata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("incercare_quiz_pkey", x => x.id);
                    table.ForeignKey(
                        name: "incercare_quiz_curs_id_fkey",
                        column: x => x.curs_id,
                        principalSchema: "mark_ai",
                        principalTable: "curs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "incercare_quiz_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_generare_ai_utilizator_data",
                schema: "mark_ai",
                table: "generare_ai",
                columns: new[] { "utilizator_id", "creat_la" });

            migrationBuilder.CreateIndex(
                name: "idx_incercare_quiz_curs_id_fkey",
                schema: "mark_ai",
                table: "incercare_quiz",
                column: "curs_id");

            migrationBuilder.CreateIndex(
                name: "idx_incercare_quiz_utilizator_curs",
                schema: "mark_ai",
                table: "incercare_quiz",
                columns: new[] { "utilizator_id", "curs_id", "inceput_la" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generare_ai",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "incercare_quiz",
                schema: "mark_ai");

            migrationBuilder.DropColumn(
                name: "versiune_sesiune",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "cheie_expira_la",
                schema: "mark_ai",
                table: "sesiune");

            migrationBuilder.DropColumn(
                name: "cheie_hash",
                schema: "mark_ai",
                table: "sesiune");

            migrationBuilder.DropColumn(
                name: "durata_secunde",
                schema: "mark_ai",
                table: "sesiune");
        }
    }
}
