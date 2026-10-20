using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class Parcursuri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parcurs",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    materie_id = table.Column<int>(type: "integer", nullable: false),
                    titlu = table.Column<string>(type: "text", nullable: false),
                    descriere = table.Column<string>(type: "text", nullable: true),
                    clasa = table.Column<string>(type: "text", nullable: false),
                    autor_id = table.Column<int>(type: "integer", nullable: true),
                    organizatie_id = table.Column<int>(type: "integer", nullable: true),
                    vizibilitate = table.Column<string>(type: "text", nullable: false, defaultValue: "public"),
                    publicat = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ordine = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    creat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("parcurs_pkey", x => x.id);
                    table.UniqueConstraint("parcurs_id_materie_id_key", x => new { x.id, x.materie_id });
                    table.CheckConstraint("parcurs_clasa_check", "clasa IN ('IX', 'X', 'XI', 'XII', 'Bac')");
                    table.CheckConstraint("parcurs_organizatie_check", "vizibilitate = 'public' OR organizatie_id IS NOT NULL");
                    table.CheckConstraint("parcurs_vizibilitate_check", "vizibilitate IN ('public', 'organizatie')");
                    table.ForeignKey(
                        name: "parcurs_autor_id_fkey",
                        column: x => x.autor_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "parcurs_materie_id_fkey",
                        column: x => x.materie_id,
                        principalSchema: "mark_ai",
                        principalTable: "materie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "parcurs_organizatie_id_fkey",
                        column: x => x.organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parcurs_organizatie",
                schema: "mark_ai",
                columns: table => new
                {
                    parcurs_id = table.Column<int>(type: "integer", nullable: false),
                    organizatie_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("parcurs_organizatie_pkey", x => new { x.parcurs_id, x.organizatie_id });
                    table.ForeignKey(
                        name: "parcurs_organizatie_organizatie_id_fkey",
                        column: x => x.organizatie_id,
                        principalSchema: "mark_ai",
                        principalTable: "organizatie",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "parcurs_organizatie_parcurs_id_fkey",
                        column: x => x.parcurs_id,
                        principalSchema: "mark_ai",
                        principalTable: "parcurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Existing chapters become platform Lecții: one per subject and chapter label, public, grade "Bac", authored by
            // the first platform admin (if any). Path order follows the first chapter of each group; chapters keep their relative order.
            migrationBuilder.AddColumn<int>(
                name: "parcurs_id",
                schema: "mark_ai",
                table: "curs",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO mark_ai.parcurs (materie_id, titlu, clasa, autor_id, organizatie_id, vizibilitate, publicat, ordine)
                SELECT c.materie_id, COALESCE(c.capitol, 'Lecții'), 'Bac',
                       (SELECT MIN(ur.utilizator_id) FROM mark_ai.utilizator_rol ur JOIN mark_ai.rol r ON r.id = ur.rol_id WHERE r.nume = 'admin'),
                       NULL, 'public', true, MIN(c.ordine)
                FROM mark_ai.curs c
                GROUP BY c.materie_id, COALESCE(c.capitol, 'Lecții')
                ORDER BY c.materie_id, MIN(c.ordine);

                UPDATE mark_ai.curs c SET parcurs_id = p.id
                FROM mark_ai.parcurs p
                WHERE p.materie_id = c.materie_id AND p.titlu = COALESCE(c.capitol, 'Lecții');

                -- The recommended order inside each Lecție: 1..n, ties broken by id.
                UPDATE mark_ai.curs c SET ordine = o.n
                FROM (SELECT id, ROW_NUMBER() OVER (PARTITION BY parcurs_id ORDER BY ordine, id) AS n FROM mark_ai.curs) o
                WHERE o.id = c.id;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "parcurs_id",
                schema: "mark_ai",
                table: "curs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_curs_parcurs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "curs",
                columns: new[] { "parcurs_id", "materie_id" });

            migrationBuilder.CreateIndex(
                name: "idx_parcurs_autor_id_fkey",
                schema: "mark_ai",
                table: "parcurs",
                column: "autor_id");

            migrationBuilder.CreateIndex(
                name: "idx_parcurs_materie_clasa",
                schema: "mark_ai",
                table: "parcurs",
                columns: new[] { "materie_id", "clasa" });

            migrationBuilder.CreateIndex(
                name: "idx_parcurs_organizatie_id_fkey",
                schema: "mark_ai",
                table: "parcurs",
                column: "organizatie_id");

            migrationBuilder.CreateIndex(
                name: "idx_parcurs_organizatie_organizatie_id_fkey",
                schema: "mark_ai",
                table: "parcurs_organizatie",
                column: "organizatie_id");

            migrationBuilder.AddForeignKey(
                name: "curs_parcurs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "curs",
                columns: new[] { "parcurs_id", "materie_id" },
                principalSchema: "mark_ai",
                principalTable: "parcurs",
                principalColumns: new[] { "id", "materie_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "curs_parcurs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropTable(
                name: "parcurs_organizatie",
                schema: "mark_ai");

            migrationBuilder.DropTable(
                name: "parcurs",
                schema: "mark_ai");

            migrationBuilder.DropIndex(
                name: "idx_curs_parcurs_id_materie_id_fkey",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropColumn(
                name: "parcurs_id",
                schema: "mark_ai",
                table: "curs");
        }
    }
}
