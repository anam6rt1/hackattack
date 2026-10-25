using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RapoarteInvitatiiParintiC1C4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_sumar",
                schema: "mark_ai",
                columns: table => new
                {
                    mesaj_id = table.Column<int>(type: "integer", nullable: false),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    versiune = table.Column<string>(type: "text", nullable: false),
                    confirmare = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_sumar", x => x.mesaj_id);
                    table.ForeignKey(
                        name: "FK_email_sumar_mesaj_iesire_mesaj_id",
                        column: x => x.mesaj_id,
                        principalSchema: "mark_ai",
                        principalTable: "mesaj_iesire",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invitatie_elev",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    organizatie_id = table.Column<int>(type: "integer", nullable: false),
                    nume = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    clasa = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    acceptata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitatie_elev", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitatie_elev_organizatie_organizatie_id",
                        column: x => x.organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "raspuns_statistica",
                schema: "mark_ai",
                columns: table => new
                {
                    incercare_id = table.Column<int>(type: "integer", nullable: false),
                    intrebare_id = table.Column<int>(type: "integer", nullable: false),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    corect = table.Column<bool>(type: "boolean", nullable: false),
                    raspuns_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raspuns_statistica", x => new { x.incercare_id, x.intrebare_id });
                    table.ForeignKey(
                        name: "FK_raspuns_statistica_incercare_quiz_incercare_id",
                        column: x => x.incercare_id,
                        principalSchema: "mark_ai",
                        principalTable: "incercare_quiz",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_raspuns_statistica_intrebare_intrebare_id",
                        column: x => x.intrebare_id,
                        principalSchema: "mark_ai",
                        principalTable: "intrebare",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_raspuns_statistica_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sumar_parinte",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    oprire_hash = table.Column<string>(type: "text", nullable: false),
                    oprire_protejat = table.Column<string>(type: "text", nullable: false),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    confirmat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultima_saptamana = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sumar_parinte", x => x.utilizator_id);
                    table.ForeignKey(
                        name: "FK_sumar_parinte_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_sumar_utilizator_id",
                schema: "mark_ai",
                table: "email_sumar",
                column: "utilizator_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitatie_elev_organizatie_id_email",
                schema: "mark_ai",
                table: "invitatie_elev",
                columns: new[] { "organizatie_id", "email" });

            migrationBuilder.CreateIndex(
                name: "IX_invitatie_elev_token_hash",
                schema: "mark_ai",
                table: "invitatie_elev",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_raspuns_statistica_intrebare_id_raspuns_la",
                schema: "mark_ai",
                table: "raspuns_statistica",
                columns: new[] { "intrebare_id", "raspuns_la" });

            migrationBuilder.CreateIndex(
                name: "IX_raspuns_statistica_utilizator_id_raspuns_la",
                schema: "mark_ai",
                table: "raspuns_statistica",
                columns: new[] { "utilizator_id", "raspuns_la" });

            migrationBuilder.CreateIndex(
                name: "IX_sumar_parinte_oprire_hash",
                schema: "mark_ai",
                table: "sumar_parinte",
                column: "oprire_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sumar_parinte_token_hash",
                schema: "mark_ai",
                table: "sumar_parinte",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_sumar",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "invitatie_elev",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "raspuns_statistica",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "sumar_parinte",
                schema: "mark_ai");
        }
    }
}
