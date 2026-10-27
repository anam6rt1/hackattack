using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SincronizareOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operatie_sincronizata",
                schema: "mark_ai",
                columns: table => new
                {
                    utilizator_id = table.Column<int>(type: "integer", nullable: false),
                    id_client = table.Column<Guid>(type: "uuid", nullable: false),
                    tip = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    aplicata_la = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rezultat_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operatie_sincronizata", x => new { x.utilizator_id, x.id_client });
                    table.ForeignKey(
                        name: "FK_operatie_sincronizata_utilizator_utilizator_id",
                        column: x => x.utilizator_id,
                        principalSchema: "mark_ai",
                        principalTable: "utilizator",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operatie_sincronizata",
                schema: "mark_ai");
        }
    }
}
