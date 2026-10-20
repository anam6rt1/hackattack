using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class Organizatii : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "avertizat_aprobare_la",
                schema: "mark_ai",
                table: "utilizator",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clasa",
                schema: "mark_ai",
                table: "utilizator",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "judet_id",
                schema: "mark_ai",
                table: "utilizator",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "localitate_id",
                schema: "mark_ai",
                table: "utilizator",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "judet",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    nume = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("judet_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "localitate",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    judet_id = table.Column<int>(type: "integer", nullable: false),
                    nume = table.Column<string>(type: "text", nullable: false),
                    uat_id = table.Column<int>(type: "integer", nullable: false),
                    uat_nume = table.Column<string>(type: "text", nullable: false),
                    urban = table.Column<bool>(type: "boolean", nullable: false),
                    cautare = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("localitate_pkey", x => x.id);
                    table.ForeignKey(
                        name: "localitate_judet_id_fkey",
                        column: x => x.judet_id,
                        principalSchema: "mark_ai",
                        principalTable: "judet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organizatie",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nume = table.Column<string>(type: "text", nullable: false),
                    adresa = table.Column<string>(type: "text", nullable: false),
                    judet_id = table.Column<int>(type: "integer", nullable: false),
                    localitate_id = table.Column<int>(type: "integer", nullable: true),
                    stare = table.Column<string>(type: "text", nullable: false),
                    admin_id = table.Column<int>(type: "integer", nullable: false),
                    motiv_respingere = table.Column<string>(type: "text", nullable: true),
                    decis_de_id = table.Column<int>(type: "integer", nullable: true),
                    decis_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("organizatie_pkey", x => x.id);
                    table.CheckConstraint("organizatie_stare_check", "stare IN ('in_asteptare', 'aprobata', 'respinsa')");
                    table.ForeignKey(
                        name: "organizatie_admin_id_fkey",
                        column: x => x.admin_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "organizatie_decis_de_id_fkey",
                        column: x => x.decis_de_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "organizatie_judet_id_fkey",
                        column: x => x.judet_id,
                        principalSchema: "mark_ai",
                        principalTable: "judet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "organizatie_localitate_id_fkey",
                        column: x => x.localitate_id,
                        principalSchema: "mark_ai",
                        principalTable: "localitate",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "membru_organizatie",
                schema: "mark_ai",
                columns: table => new
                {
                    organizatie_id = table.Column<int>(type: "integer", nullable: false),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    rol = table.Column<string>(type: "text", nullable: false),
                    stare = table.Column<string>(type: "text", nullable: false),
                    poate_publica_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    cerut_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    amintit_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decis_de_id = table.Column<int>(type: "integer", nullable: true),
                    decis_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motiv_respingere = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("membru_organizatie_pkey", x => new { x.organizatie_id, x.utilizator_id });
                    table.CheckConstraint("membru_organizatie_rol_check", "rol IN ('profesor', 'elev')");
                    table.CheckConstraint("membru_organizatie_stare_check", "stare IN ('in_asteptare', 'aprobat', 'respins', 'expirat')");
                    table.ForeignKey(
                        name: "membru_organizatie_decis_de_id_fkey",
                        column: x => x.decis_de_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "membru_organizatie_organizatie_id_fkey",
                        column: x => x.organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "membru_organizatie_utilizator_id_fkey",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_utilizator_judet_id",
                schema: "mark_ai",
                table: "utilizator",
                column: "judet_id");

            migrationBuilder.CreateIndex(
                name: "IX_utilizator_localitate_id",
                schema: "mark_ai",
                table: "utilizator",
                column: "localitate_id");

            migrationBuilder.AddCheckConstraint(
                name: "utilizator_clasa_check",
                schema: "mark_ai",
                table: "utilizator",
                sql: "clasa IS NULL OR clasa IN ('IX', 'X', 'XI', 'XII')");

            migrationBuilder.CreateIndex(
                name: "idx_localitate_judet_cautare",
                schema: "mark_ai",
                table: "localitate",
                columns: new[] { "judet_id", "cautare" });

            migrationBuilder.CreateIndex(
                name: "idx_membru_organizatie_org_rol_stare",
                schema: "mark_ai",
                table: "membru_organizatie",
                columns: new[] { "organizatie_id", "rol", "stare" });

            migrationBuilder.CreateIndex(
                name: "IX_membru_organizatie_decis_de_id",
                schema: "mark_ai",
                table: "membru_organizatie",
                column: "decis_de_id");

            migrationBuilder.CreateIndex(
                name: "membru_organizatie_un_elev_key",
                schema: "mark_ai",
                table: "membru_organizatie",
                column: "utilizator_id",
                unique: true,
                filter: "rol = 'elev' AND stare IN ('in_asteptare', 'aprobat')");

            migrationBuilder.CreateIndex(
                name: "idx_organizatie_stare_judet",
                schema: "mark_ai",
                table: "organizatie",
                columns: new[] { "stare", "judet_id" });

            migrationBuilder.CreateIndex(
                name: "IX_organizatie_decis_de_id",
                schema: "mark_ai",
                table: "organizatie",
                column: "decis_de_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizatie_judet_id",
                schema: "mark_ai",
                table: "organizatie",
                column: "judet_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizatie_localitate_id",
                schema: "mark_ai",
                table: "organizatie",
                column: "localitate_id");

            migrationBuilder.CreateIndex(
                name: "organizatie_admin_id_key",
                schema: "mark_ai",
                table: "organizatie",
                column: "admin_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "utilizator_judet_id_fkey",
                schema: "mark_ai",
                table: "utilizator",
                column: "judet_id",
                principalSchema: "mark_ai",
                principalTable: "judet",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "utilizator_localitate_id_fkey",
                schema: "mark_ai",
                table: "utilizator",
                column: "localitate_id",
                principalSchema: "mark_ai",
                principalTable: "localitate",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // The role of organisation admins. Counties, localities and the demo organisation are loaded by the
            // MarkAi.Database tool (seed-localitati, organizatie-demo), since they depend on data files and live accounts.
            migrationBuilder.Sql("INSERT INTO mark_ai.rol (nume) SELECT 'organizatie' WHERE NOT EXISTS (SELECT 1 FROM mark_ai.rol WHERE nume = 'organizatie');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "utilizator_judet_id_fkey",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropForeignKey(
                name: "utilizator_localitate_id_fkey",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropTable(
                name: "membru_organizatie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "organizatie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "localitate",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "judet",
                schema: "mark_ai");

            migrationBuilder.DropIndex(
                name: "IX_utilizator_judet_id",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropIndex(
                name: "IX_utilizator_localitate_id",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropCheckConstraint(
                name: "utilizator_clasa_check",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "avertizat_aprobare_la",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "clasa",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "judet_id",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "localitate_id",
                schema: "mark_ai",
                table: "utilizator");
        }
    }
}
