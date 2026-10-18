using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegrareFrontend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "parola_hash",
                schema: "mark_ai",
                table: "utilizator",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "creata_la",
                schema: "mark_ai",
                table: "trivia_intrebare",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "curs_id",
                schema: "mark_ai",
                table: "trivia_intrebare",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "explicatie",
                schema: "mark_ai",
                table: "trivia_intrebare",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "raspuns_numeric",
                schema: "mark_ai",
                table: "trivia_intrebare",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sursa",
                schema: "mark_ai",
                table: "trivia_intrebare",
                type: "text",
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<string>(
                name: "tip",
                schema: "mark_ai",
                table: "trivia_intrebare",
                type: "text",
                nullable: false,
                defaultValue: "grila");

            migrationBuilder.AddColumn<string>(
                name: "dovada_calificare_url",
                schema: "mark_ai",
                table: "profil_mentor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moneda",
                schema: "mark_ai",
                table: "profil_mentor",
                type: "text",
                nullable: false,
                defaultValue: "RON");

            migrationBuilder.AddColumn<string>(
                name: "motiv_respingere",
                schema: "mark_ai",
                table: "profil_mentor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "onorariu_negociat",
                schema: "mark_ai",
                table: "profil_mentor",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "poza_url",
                schema: "mark_ai",
                table: "profil_mentor",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "verificat_de_id",
                schema: "mark_ai",
                table: "profil_mentor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cod",
                schema: "mark_ai",
                table: "materie",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ordine",
                schema: "mark_ai",
                table: "materie",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "capitol",
                schema: "mark_ai",
                table: "curs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "creat_de_id",
                schema: "mark_ai",
                table: "curs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "creat_la",
                schema: "mark_ai",
                table: "curs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "materiale",
                schema: "mark_ai",
                table: "curs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ordine",
                schema: "mark_ai",
                table: "curs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "publicat",
                schema: "mark_ai",
                table: "curs",
                type: "boolean",
                nullable: false,
                // Existing courses stay visible after the upgrade.
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "video_url",
                schema: "mark_ai",
                table: "curs",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "tarif_id",
                schema: "mark_ai",
                table: "camera_tutoring",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "pro_bono",
                schema: "mark_ai",
                table: "camera_tutoring",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "mentor_materie",
                schema: "mark_ai",
                columns: table => new
                {
                    mentor_id = table.Column<int>(type: "integer", nullable: false),
                    materie_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("mentor_materie_pkey", x => new { x.mentor_id, x.materie_id });
                    table.ForeignKey(
                        name: "mentor_materie_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "mentor_materie_mentor_id_fkey",
                        column: x => x.mentor_id,
                        principalSchema: "mark_ai",
                        principalTable: "profil_mentor",
                        principalColumn: "utilizator_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "progres_curs",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    curs_id = table.Column<int>(type: "integer", nullable: false),
                    video_vazut = table.Column<bool>(type: "boolean", nullable: false),
                    materiale_citite = table.Column<bool>(type: "boolean", nullable: false),
                    quiz_promovat = table.Column<bool>(type: "boolean", nullable: false),
                    completat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultima_accesare = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("progres_curs_pkey", x => new { x.utilizator_id, x.curs_id });
                    table.ForeignKey(
                        name: "progres_curs_curs_id_fkey",
                        column: x => x.curs_id,
                        principalSchema: "mark_ai",
                        principalTable: "curs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "progres_curs_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trivia_intrebare_curs_id_materie_id",
                schema: "mark_ai",
                table: "trivia_intrebare",
                columns: new[] { "curs_id", "materie_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_trivia_intrebare_estimare",
                schema: "mark_ai",
                table: "trivia_intrebare",
                sql: "(tip = 'estimare') = (raspuns_numeric IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_trivia_intrebare_sursa",
                schema: "mark_ai",
                table: "trivia_intrebare",
                sql: "sursa IN ('ai','manual')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_trivia_intrebare_tip",
                schema: "mark_ai",
                table: "trivia_intrebare",
                sql: "tip IN ('grila','estimare')");

            migrationBuilder.CreateIndex(
                name: "IX_profil_mentor_verificat_de_id",
                schema: "mark_ai",
                table: "profil_mentor",
                column: "verificat_de_id");

            migrationBuilder.AddCheckConstraint(
                name: "profil_mentor_moneda_check",
                schema: "mark_ai",
                table: "profil_mentor",
                sql: "moneda ~ '^[A-Z]{3}$'::text");

            migrationBuilder.AddCheckConstraint(
                name: "profil_mentor_onorariu_negociat_check",
                schema: "mark_ai",
                table: "profil_mentor",
                sql: "onorariu_negociat >= (0)::numeric");

            migrationBuilder.CreateIndex(
                name: "materie_cod_key",
                schema: "mark_ai",
                table: "materie",
                column: "cod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_curs_materie_id_ordine",
                schema: "mark_ai",
                table: "curs",
                columns: new[] { "materie_id", "ordine" });

            migrationBuilder.CreateIndex(
                name: "IX_curs_creat_de_id",
                schema: "mark_ai",
                table: "curs",
                column: "creat_de_id");

            migrationBuilder.AddCheckConstraint(
                name: "camera_tutoring_pro_bono_check",
                schema: "mark_ai",
                table: "camera_tutoring",
                sql: "pro_bono = (tarif_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "idx_mentor_materie_materie_id_fkey",
                schema: "mark_ai",
                table: "mentor_materie",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "idx_progres_curs_curs_id_fkey",
                schema: "mark_ai",
                table: "progres_curs",
                column: "curs_id");

            migrationBuilder.AddForeignKey(
                name: "curs_creat_de_id_fkey",
                schema: "mark_ai",
                table: "curs",
                column: "creat_de_id",
                principalSchema: "mark_ai",
                principalTable: "utilizator",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "profil_mentor_verificat_de_id_fkey",
                schema: "mark_ai",
                table: "profil_mentor",
                column: "verificat_de_id",
                principalSchema: "mark_ai",
                principalTable: "utilizator",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "trivia_intrebare_curs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "trivia_intrebare",
                columns: new[] { "curs_id", "materie_id" },
                principalSchema: "mark_ai",
                principalTable: "curs",
                principalColumns: new[] { "id", "materie_id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "curs_creat_de_id_fkey",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropForeignKey(
                name: "profil_mentor_verificat_de_id_fkey",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropForeignKey(
                name: "trivia_intrebare_curs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropTable(
                name: "mentor_materie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "progres_curs",
                schema: "mark_ai");

            migrationBuilder.DropIndex(
                name: "IX_trivia_intrebare_curs_id_materie_id",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropCheckConstraint(
                name: "ck_trivia_intrebare_estimare",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropCheckConstraint(
                name: "ck_trivia_intrebare_sursa",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropCheckConstraint(
                name: "ck_trivia_intrebare_tip",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropIndex(
                name: "IX_profil_mentor_verificat_de_id",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropCheckConstraint(
                name: "profil_mentor_moneda_check",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropCheckConstraint(
                name: "profil_mentor_onorariu_negociat_check",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropIndex(
                name: "materie_cod_key",
                schema: "mark_ai",
                table: "materie");

            migrationBuilder.DropIndex(
                name: "idx_curs_materie_id_ordine",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropIndex(
                name: "IX_curs_creat_de_id",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropCheckConstraint(
                name: "camera_tutoring_pro_bono_check",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropColumn(
                name: "parola_hash",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "creata_la",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropColumn(
                name: "curs_id",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropColumn(
                name: "explicatie",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropColumn(
                name: "raspuns_numeric",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropColumn(
                name: "sursa",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropColumn(
                name: "tip",
                schema: "mark_ai",
                table: "trivia_intrebare");

            migrationBuilder.DropColumn(
                name: "dovada_calificare_url",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropColumn(
                name: "moneda",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropColumn(
                name: "motiv_respingere",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropColumn(
                name: "onorariu_negociat",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropColumn(
                name: "poza_url",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropColumn(
                name: "verificat_de_id",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropColumn(
                name: "cod",
                schema: "mark_ai",
                table: "materie");

            migrationBuilder.DropColumn(
                name: "ordine",
                schema: "mark_ai",
                table: "materie");

            migrationBuilder.DropColumn(
                name: "capitol",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropColumn(
                name: "creat_de_id",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropColumn(
                name: "creat_la",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropColumn(
                name: "materiale",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropColumn(
                name: "ordine",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropColumn(
                name: "publicat",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropColumn(
                name: "video_url",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropColumn(
                name: "pro_bono",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.AlterColumn<int>(
                name: "tarif_id",
                schema: "mark_ai",
                table: "camera_tutoring",
                type: "integer",
                nullable: false,
                // No default: restoring NOT NULL fails loudly if pro bono slots (null tarif) still exist.
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
