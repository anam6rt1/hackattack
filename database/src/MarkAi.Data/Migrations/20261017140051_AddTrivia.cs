using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrivia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Canonicalize historical, unreferenced UNIQUE constraints to EF's unique indexes.
            // Referenced alternate keys are preserved. No rows or uniqueness guarantees are removed.
            migrationBuilder.Sql("""
                DO $$
                DECLARE r record;
                BEGIN
                  FOR r IN
                    SELECT c.conrelid::regclass AS tbl, c.conname, pg_get_indexdef(c.conindid) AS ddl
                    FROM pg_constraint c
                    WHERE c.connamespace='mark_ai'::regnamespace AND c.contype='u'
                      AND NOT EXISTS (SELECT 1 FROM pg_constraint f WHERE f.contype='f' AND f.conindid=c.conindid)
                  LOOP
                    EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname);
                    EXECUTE r.ddl;
                  END LOOP;
                END $$;
                """);
            migrationBuilder.CreateTable(
                name: "trivia_camera",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cod = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    gazda_id = table.Column<int>(type: "integer", nullable: false),
                    materie_id = table.Column<int>(type: "integer", nullable: true),
                    capacitate = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    creata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    finalizata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trivia_camera", x => x.id);
                    table.CheckConstraint("ck_trivia_camera_capacitate", "capacitate BETWEEN 2 AND 16");
                    table.CheckConstraint("ck_trivia_camera_cod", "cod ~ '^[A-Z0-9]{6}$'");
                    table.CheckConstraint("ck_trivia_camera_final", "(status = 'finished') = (finalizata_la IS NOT NULL) AND (finalizata_la IS NULL OR finalizata_la >= creata_la)");
                    table.CheckConstraint("ck_trivia_camera_status", "status IN ('waiting','active','finished')");
                    table.ForeignKey(
                        name: "FK_trivia_camera_materie_materie_id",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trivia_camera_utilizator_gazda_id",
                        column: x => x.gazda_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trivia_intrebare",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trivia_intrebare", x => x.id);
                    table.CheckConstraint("ck_trivia_intrebare_text", "length(btrim(text)) > 0");
                    table.ForeignKey(
                        name: "FK_trivia_intrebare_materie_materie_id",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trivia_participant",
                schema: "mark_ai",
                columns: table => new
                {
                    camera_id = table.Column<int>(type: "integer", nullable: false),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    intrat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trivia_participant", x => new { x.camera_id, x.utilizator_id });
                    table.ForeignKey(
                        name: "FK_trivia_participant_trivia_camera_camera_id",
                        column: x => x.camera_id,
                        principalSchema: "mark_ai",
                        principalTable: "trivia_camera",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trivia_participant_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trivia_runda",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    camera_id = table.Column<int>(type: "integer", nullable: false),
                    numar = table.Column<int>(type: "integer", nullable: false),
                    intrebare_id = table.Column<int>(type: "integer", nullable: false),
                    text_intrebare = table.Column<string>(type: "text", nullable: false),
                    inceputa_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    termen_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    puncte = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trivia_runda", x => x.id);
                    table.UniqueConstraint("AK_trivia_runda_id_camera_id", x => new { x.id, x.camera_id });
                    table.CheckConstraint("ck_trivia_runda_interval", "termen_la > inceputa_la AND termen_la <= inceputa_la + interval '5 minutes'");
                    table.CheckConstraint("ck_trivia_runda_numar", "numar > 0");
                    table.CheckConstraint("ck_trivia_runda_puncte", "puncte > 0");
                    table.ForeignKey(
                        name: "FK_trivia_runda_trivia_camera_camera_id",
                        column: x => x.camera_id,
                        principalSchema: "mark_ai",
                        principalTable: "trivia_camera",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trivia_runda_trivia_intrebare_intrebare_id",
                        column: x => x.intrebare_id,
                        principalSchema: "mark_ai",
                        principalTable: "trivia_intrebare",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trivia_varianta",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    intrebare_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    corecta = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trivia_varianta", x => x.id);
                    table.CheckConstraint("ck_trivia_varianta_text", "length(btrim(text)) > 0");
                    table.ForeignKey(
                        name: "FK_trivia_varianta_trivia_intrebare_intrebare_id",
                        column: x => x.intrebare_id,
                        principalSchema: "mark_ai",
                        principalTable: "trivia_intrebare",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trivia_runda_varianta",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    runda_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    corecta = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trivia_runda_varianta", x => x.id);
                    table.UniqueConstraint("AK_trivia_runda_varianta_id_runda_id", x => new { x.id, x.runda_id });
                    table.ForeignKey(
                        name: "FK_trivia_runda_varianta_trivia_runda_runda_id",
                        column: x => x.runda_id,
                        principalSchema: "mark_ai",
                        principalTable: "trivia_runda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trivia_raspuns",
                schema: "mark_ai",
                columns: table => new
                {
                    runda_id = table.Column<int>(type: "integer", nullable: false),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    camera_id = table.Column<int>(type: "integer", nullable: false),
                    varianta_id = table.Column<int>(type: "integer", nullable: false),
                    trimis_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    scor = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trivia_raspuns", x => new { x.runda_id, x.utilizator_id });
                    table.CheckConstraint("ck_trivia_raspuns_scor", "scor >= 0");
                    table.ForeignKey(
                        name: "FK_trivia_raspuns_trivia_participant_camera_id_utilizator_id",
                        columns: x => new { x.camera_id, x.utilizator_id },
                        principalSchema: "mark_ai",
                        principalTable: "trivia_participant",
                        principalColumns: new[] { "camera_id", "utilizator_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trivia_raspuns_trivia_runda_runda_id_camera_id",
                        columns: x => new { x.runda_id, x.camera_id },
                        principalSchema: "mark_ai",
                        principalTable: "trivia_runda",
                        principalColumns: new[] { "id", "camera_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trivia_raspuns_trivia_runda_varianta_varianta_id_runda_id",
                        columns: x => new { x.varianta_id, x.runda_id },
                        principalSchema: "mark_ai",
                        principalTable: "trivia_runda_varianta",
                        principalColumns: new[] { "id", "runda_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trivia_camera_cod",
                schema: "mark_ai",
                table: "trivia_camera",
                column: "cod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trivia_camera_gazda_id",
                schema: "mark_ai",
                table: "trivia_camera",
                column: "gazda_id");

            migrationBuilder.CreateIndex(
                name: "IX_trivia_camera_materie_id",
                schema: "mark_ai",
                table: "trivia_camera",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "IX_trivia_intrebare_materie_id",
                schema: "mark_ai",
                table: "trivia_intrebare",
                column: "materie_id");

            migrationBuilder.CreateIndex(
                name: "IX_trivia_participant_utilizator_id",
                schema: "mark_ai",
                table: "trivia_participant",
                column: "utilizator_id");

            migrationBuilder.CreateIndex(
                name: "IX_trivia_raspuns_camera_id_utilizator_id",
                schema: "mark_ai",
                table: "trivia_raspuns",
                columns: new[] { "camera_id", "utilizator_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trivia_raspuns_runda_id_camera_id",
                schema: "mark_ai",
                table: "trivia_raspuns",
                columns: new[] { "runda_id", "camera_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trivia_raspuns_varianta_id_runda_id",
                schema: "mark_ai",
                table: "trivia_raspuns",
                columns: new[] { "varianta_id", "runda_id" });

            migrationBuilder.CreateIndex(
                name: "IX_trivia_runda_camera_id_numar",
                schema: "mark_ai",
                table: "trivia_runda",
                columns: new[] { "camera_id", "numar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trivia_runda_intrebare_id",
                schema: "mark_ai",
                table: "trivia_runda",
                column: "intrebare_id");

            migrationBuilder.CreateIndex(
                name: "IX_trivia_runda_varianta_runda_id",
                schema: "mark_ai",
                table: "trivia_runda_varianta",
                column: "runda_id",
                unique: true,
                filter: "corecta");

            migrationBuilder.CreateIndex(
                name: "IX_trivia_varianta_intrebare_id",
                schema: "mark_ai",
                table: "trivia_varianta",
                column: "intrebare_id",
                unique: true,
                filter: "corecta");
            migrationBuilder.Sql(MarkAi.Data.Trivia.TriviaDatabaseRules.Create);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MarkAi.Data.Trivia.TriviaDatabaseRules.Drop);
            migrationBuilder.DropTable(
                name: "trivia_raspuns",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "trivia_varianta",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "trivia_participant",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "trivia_runda_varianta",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "trivia_runda",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "trivia_camera",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "trivia_intrebare",
                schema: "mark_ai");
        }
    }
}


