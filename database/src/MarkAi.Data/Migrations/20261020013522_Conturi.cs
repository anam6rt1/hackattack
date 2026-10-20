using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class Conturi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "avertizat_la",
                schema: "mark_ai",
                table: "utilizator",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "email_confirmat_la",
                schema: "mark_ai",
                table: "utilizator",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "telefon_verificat_la",
                schema: "mark_ai",
                table: "utilizator",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cod_verificare",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    scop = table.Column<string>(type: "text", nullable: false),
                    destinatie = table.Column<string>(type: "text", nullable: false),
                    hash_cod = table.Column<string>(type: "text", nullable: false),
                    incercari = table.Column<int>(type: "integer", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    folosit_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cod_verificare_pkey", x => x.id);
                    table.ForeignKey(
                        name: "cod_verificare_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mesaj_iesire",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    canal = table.Column<string>(type: "text", nullable: false),
                    catre = table.Column<string>(type: "text", nullable: false),
                    subiect = table.Column<string>(type: "text", nullable: true),
                    html = table.Column<string>(type: "text", nullable: true),
                    text = table.Column<string>(type: "text", nullable: true),
                    tip = table.Column<string>(type: "text", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    trimis_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    furnizor = table.Column<string>(type: "text", nullable: true),
                    incercari = table.Column<int>(type: "integer", nullable: false),
                    urmatoarea_incercare_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    eroare = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mesaj_iesire_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_cod_verificare_utilizator_scop",
                schema: "mark_ai",
                table: "cod_verificare",
                columns: new[] { "utilizator_id", "scop" });

            migrationBuilder.CreateIndex(
                name: "idx_mesaj_iesire_canal_data",
                schema: "mark_ai",
                table: "mesaj_iesire",
                columns: new[] { "canal", "creat_la" });

            migrationBuilder.CreateIndex(
                name: "idx_mesaj_iesire_de_trimis",
                schema: "mark_ai",
                table: "mesaj_iesire",
                columns: new[] { "trimis_la", "urmatoarea_incercare_la" });

            // Accounts that existed before email confirmation count as confirmed (they were created by real people
            // during the hackathon demo); only new accounts must confirm.
            migrationBuilder.Sql("UPDATE mark_ai.utilizator SET email_confirmat_la = creat_la WHERE email_confirmat_la IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cod_verificare",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "mesaj_iesire",
                schema: "mark_ai");

            migrationBuilder.DropColumn(
                name: "avertizat_la",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "email_confirmat_la",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "telefon_verificat_la",
                schema: "mark_ai",
                table: "utilizator");
        }
    }
}
