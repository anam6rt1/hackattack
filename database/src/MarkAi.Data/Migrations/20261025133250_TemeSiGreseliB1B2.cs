using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class TemeSiGreseliB1B2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "greseala",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    intrebare_id = table.Column<int>(type: "integer", nullable: false),
                    gresit_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scadenta_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    nivel = table.Column<int>(type: "integer", nullable: false),
                    corecte_consecutive = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<Guid>(type: "uuid", nullable: true),
                    emisa_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_greseala", x => new { x.utilizator_id, x.intrebare_id });
                    table.CheckConstraint("greseala_nivel_check", "nivel BETWEEN 0 AND 3 AND corecte_consecutive BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_greseala_intrebare_intrebare_id",
                        column: x => x.intrebare_id,
                        principalSchema: "mark_ai",
                        principalTable: "intrebare",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_greseala_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grupa_elevi",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    organizatie_id = table.Column<int>(type: "integer", nullable: false),
                    profesor_id = table.Column<int>(type: "integer", nullable: true),
                    nume = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grupa_elevi", x => x.id);
                    table.ForeignKey(
                        name: "FK_grupa_elevi_organizatie_organizatie_id",
                        column: x => x.organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_grupa_elevi_utilizator_profesor_id",
                        column: x => x.profesor_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tema",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    organizatie_id = table.Column<int>(type: "integer", nullable: false),
                    profesor_id = table.Column<int>(type: "integer", nullable: true),
                    titlu = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descriere = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    termen_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    toata_organizatia = table.Column<bool>(type: "boolean", nullable: false),
                    anulata = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tema", x => x.id);
                    table.CheckConstraint("tema_termen_check", "termen_la > creat_la");
                    table.ForeignKey(
                        name: "FK_tema_organizatie_organizatie_id",
                        column: x => x.organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tema_utilizator_profesor_id",
                        column: x => x.profesor_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "grupa_membru",
                schema: "mark_ai",
                columns: table => new
                {
                    grupa_id = table.Column<int>(type: "integer", nullable: false),
                    elev_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grupa_membru", x => new { x.grupa_id, x.elev_id });
                    table.ForeignKey(
                        name: "FK_grupa_membru_grupa_elevi_grupa_id",
                        column: x => x.grupa_id,
                        principalSchema: "mark_ai",
                        principalTable: "grupa_elevi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_grupa_membru_utilizator_elev_id",
                        column: x => x.elev_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tema_grupa",
                schema: "mark_ai",
                columns: table => new
                {
                    tema_id = table.Column<int>(type: "integer", nullable: false),
                    grupa_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tema_grupa", x => new { x.tema_id, x.grupa_id });
                    table.ForeignKey(
                        name: "FK_tema_grupa_grupa_elevi_grupa_id",
                        column: x => x.grupa_id,
                        principalSchema: "mark_ai",
                        principalTable: "grupa_elevi",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tema_grupa_tema_tema_id",
                        column: x => x.tema_id,
                        principalSchema: "mark_ai",
                        principalTable: "tema",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tema_lectie",
                schema: "mark_ai",
                columns: table => new
                {
                    tema_id = table.Column<int>(type: "integer", nullable: false),
                    parcurs_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tema_lectie", x => new { x.tema_id, x.parcurs_id });
                    table.ForeignKey(
                        name: "FK_tema_lectie_parcurs_parcurs_id",
                        column: x => x.parcurs_id,
                        principalSchema: "mark_ai",
                        principalTable: "parcurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tema_lectie_tema_tema_id",
                        column: x => x.tema_id,
                        principalSchema: "mark_ai",
                        principalTable: "tema",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_greseala_intrebare_id",
                schema: "mark_ai",
                table: "greseala",
                column: "intrebare_id");

            migrationBuilder.CreateIndex(
                name: "IX_greseala_utilizator_id_scadenta_la",
                schema: "mark_ai",
                table: "greseala",
                columns: new[] { "utilizator_id", "scadenta_la" });

            migrationBuilder.CreateIndex(
                name: "IX_grupa_elevi_organizatie_id_profesor_id",
                schema: "mark_ai",
                table: "grupa_elevi",
                columns: new[] { "organizatie_id", "profesor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_grupa_elevi_profesor_id",
                schema: "mark_ai",
                table: "grupa_elevi",
                column: "profesor_id");

            migrationBuilder.CreateIndex(
                name: "IX_grupa_membru_elev_id",
                schema: "mark_ai",
                table: "grupa_membru",
                column: "elev_id");

            migrationBuilder.CreateIndex(
                name: "IX_tema_organizatie_id_termen_la",
                schema: "mark_ai",
                table: "tema",
                columns: new[] { "organizatie_id", "termen_la" });

            migrationBuilder.CreateIndex(
                name: "IX_tema_profesor_id",
                schema: "mark_ai",
                table: "tema",
                column: "profesor_id");

            migrationBuilder.CreateIndex(
                name: "IX_tema_grupa_grupa_id",
                schema: "mark_ai",
                table: "tema_grupa",
                column: "grupa_id");

            migrationBuilder.CreateIndex(
                name: "IX_tema_lectie_parcurs_id",
                schema: "mark_ai",
                table: "tema_lectie",
                column: "parcurs_id");
            // Seed previous wrong answers once; future reviews never get recreated by reads.
            migrationBuilder.Sql("""
                INSERT INTO mark_ai.greseala (utilizator_id, intrebare_id, gresit_la, scadenta_la, nivel, corecte_consecutive)
                SELECT i.utilizator_id, x.q, max(coalesce(i.finalizata_la, i.inceput_la)),
                       max(coalesce(i.finalizata_la, i.inceput_la)) + interval '1 day', 0, 0
                FROM mark_ai.incercare_quiz i
                CROSS JOIN LATERAL unnest(i.intrebari_ids, i.raspunsuri) AS x(q,v)
                JOIN mark_ai.intrebare_varianta v ON v.id=x.v AND v.intrebare_id=x.q
                JOIN mark_ai.utilizator u ON u.id=i.utilizator_id AND u.stergere_ceruta_la IS NULL
                WHERE NOT v.corecta
                GROUP BY i.utilizator_id, x.q
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "greseala",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "grupa_membru",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "tema_grupa",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "tema_lectie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "grupa_elevi",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "tema",
                schema: "mark_ai");
        }
    }
}
