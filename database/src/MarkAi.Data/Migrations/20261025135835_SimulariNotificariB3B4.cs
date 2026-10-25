using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimulariNotificariB3B4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "utilizator_clasa_check",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropCheckConstraint(
                name: "parcurs_clasa_check",
                schema: "mark_ai",
                table: "parcurs");

            migrationBuilder.CreateTable(
                name: "abonament_push",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    p256dh = table.Column<string>(type: "text", nullable: false),
                    auth = table.Column<string>(type: "text", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_abonament_push", x => x.id);
                    table.ForeignKey(
                        name: "FK_abonament_push_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notificare",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    cheie = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    tip = table.Column<string>(type: "text", nullable: false),
                    resursa_id = table.Column<int>(type: "integer", nullable: false),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    urmatoarea_incercare_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    incercari = table.Column<int>(type: "integer", nullable: false),
                    canal = table.Column<string>(type: "text", nullable: true),
                    mesaj_email_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificare", x => x.id);
                    table.ForeignKey(
                        name: "FK_notificare_mesaj_iesire_mesaj_email_id",
                        column: x => x.mesaj_email_id,
                        principalSchema: "mark_ai",
                        principalTable: "mesaj_iesire",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_notificare_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "preferinte_notificari",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    sedinte = table.Column<bool>(type: "boolean", nullable: false),
                    decizii = table.Column<bool>(type: "boolean", nullable: false),
                    teme_noi = table.Column<bool>(type: "boolean", nullable: false),
                    termene_teme = table.Column<bool>(type: "boolean", nullable: false),
                    actualizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preferinte_notificari", x => x.utilizator_id);
                    table.ForeignKey(
                        name: "FK_preferinte_notificari_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "simulare",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    tip = table.Column<string>(type: "text", nullable: false),
                    clasa = table.Column<string>(type: "text", nullable: false),
                    inceput_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    intrebari_json = table.Column<string>(type: "jsonb", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    corecte = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulare", x => x.id);
                    table.CheckConstraint("simulare_limite_check", "total BETWEEN 1 AND 50 AND corecte BETWEEN 0 AND total AND expira_la > inceput_la");
                    table.CheckConstraint("simulare_tip_check", "tip IN ('bac','evaluare_nationala')");
                    table.ForeignKey(
                        name: "FK_simulare_materie_materie_id",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_simulare_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "utilizator_clasa_check",
                schema: "mark_ai",
                table: "utilizator",
                sql: "clasa IS NULL OR clasa IN ('VIII', 'IX', 'X', 'XI', 'XII')");

            migrationBuilder.AddCheckConstraint(
                name: "parcurs_clasa_check",
                schema: "mark_ai",
                table: "parcurs",
                sql: "clasa IN ('VIII', 'IX', 'X', 'XI', 'XII', 'Bac')");

            migrationBuilder.CreateIndex(
                name: "IX_abonament_push_endpoint",
                schema: "mark_ai",
                table: "abonament_push",
                column: "endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_abonament_push_utilizator_id",
                schema: "mark_ai",
                table: "abonament_push",
                column: "utilizator_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificare_finalizat_la_urmatoarea_incercare_la",
                schema: "mark_ai",
                table: "notificare",
                columns: new[] { "finalizat_la", "urmatoarea_incercare_la" });

            migrationBuilder.CreateIndex(
                name: "IX_notificare_mesaj_email_id",
                schema: "mark_ai",
                table: "notificare",
                column: "mesaj_email_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificare_utilizator_id_cheie",
                schema: "mark_ai",
                table: "notificare",
                columns: new[] { "utilizator_id", "cheie" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notificare_utilizator_id_tip_resursa_id",
                schema: "mark_ai",
                table: "notificare",
                columns: new[] { "utilizator_id", "tip", "resursa_id" });

            migrationBuilder.CreateIndex(
                name: "IX_simulare_materie_id",
                schema: "mark_ai",
                table: "simulare",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "IX_simulare_utilizator_id_inceput_la",
                schema: "mark_ai",
                table: "simulare",
                columns: new[] { "utilizator_id", "inceput_la" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "abonament_push",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "notificare",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "preferinte_notificari",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "simulare",
                schema: "mark_ai");

            migrationBuilder.DropCheckConstraint(
                name: "utilizator_clasa_check",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropCheckConstraint(
                name: "parcurs_clasa_check",
                schema: "mark_ai",
                table: "parcurs");

            migrationBuilder.AddCheckConstraint(
                name: "utilizator_clasa_check",
                schema: "mark_ai",
                table: "utilizator",
                sql: "clasa IS NULL OR clasa IN ('IX', 'X', 'XI', 'XII')");

            migrationBuilder.AddCheckConstraint(
                name: "parcurs_clasa_check",
                schema: "mark_ai",
                table: "parcurs",
                sql: "clasa IN ('IX', 'X', 'XI', 'XII', 'Bac')");
        }
    }
}
