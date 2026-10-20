using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class FisiereCapitol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fisier_capitol",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    curs_id = table.Column<int>(type: "integer", nullable: false),
                    nume = table.Column<string>(type: "text", nullable: false),
                    tip = table.Column<string>(type: "text", nullable: false),
                    cheie = table.Column<string>(type: "text", nullable: false),
                    marime_octeti = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    stare_text = table.Column<string>(type: "text", nullable: false, defaultValue: "in_asteptare"),
                    text_extras = table.Column<string>(type: "text", nullable: true),
                    incarcat_de_id = table.Column<int>(type: "integer", nullable: true),
                    incarcat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("fisier_capitol_pkey", x => x.id);
                    table.CheckConstraint("fisier_capitol_marime_check", "marime_octeti > 0");
                    table.CheckConstraint("fisier_capitol_stare_text_check", "stare_text IN ('in_asteptare', 'gata', 'fara_text', 'eroare')");
                    table.CheckConstraint("fisier_capitol_tip_check", "tip IN ('pdf', 'docx', 'pptx')");
                    table.ForeignKey(
                        name: "fisier_capitol_curs_id_fkey",
                        column: x => x.curs_id,
                        principalSchema: "mark_ai",
                        principalTable: "curs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fisier_capitol_incarcat_de_id_fkey",
                        column: x => x.incarcat_de_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "fisier_capitol_cheie_key",
                schema: "mark_ai",
                table: "fisier_capitol",
                column: "cheie",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_fisier_capitol_curs_id_fkey",
                schema: "mark_ai",
                table: "fisier_capitol",
                column: "curs_id");

            migrationBuilder.CreateIndex(
                name: "idx_fisier_capitol_incarcat_de_id_fkey",
                schema: "mark_ai",
                table: "fisier_capitol",
                column: "incarcat_de_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fisier_capitol",
                schema: "mark_ai");
        }
    }
}
