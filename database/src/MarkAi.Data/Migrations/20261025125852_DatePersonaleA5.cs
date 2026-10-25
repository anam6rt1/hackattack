using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class DatePersonaleA5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "acord_parinte_declarat_la",
                schema: "mark_ai",
                table: "utilizator",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "stergere_ceruta_la",
                schema: "mark_ai",
                table: "utilizator",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sub_16_ani_declarat",
                schema: "mark_ai",
                table: "utilizator",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "versiune_informare",
                schema: "mark_ai",
                table: "utilizator",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stergere_date_cont",
                schema: "mark_ai",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    cerut_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finalizat_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    chei_fisiere = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stergere_date_cont", x => x.id);
                    table.ForeignKey(
                        name: "FK_stergere_date_cont_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stergere_date_cont_utilizator_id",
                schema: "mark_ai",
                table: "stergere_date_cont",
                column: "utilizator_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stergere_date_cont",
                schema: "mark_ai");

            migrationBuilder.DropColumn(
                name: "acord_parinte_declarat_la",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "stergere_ceruta_la",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "sub_16_ani_declarat",
                schema: "mark_ai",
                table: "utilizator");

            migrationBuilder.DropColumn(
                name: "versiune_informare",
                schema: "mark_ai",
                table: "utilizator");
        }
    }
}
