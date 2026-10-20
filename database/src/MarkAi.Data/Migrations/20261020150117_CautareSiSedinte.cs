using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class CautareSiSedinte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "participare",
                schema: "mark_ai",
                table: "rezervare",
                type: "text",
                nullable: false,
                defaultValue: "online");

            migrationBuilder.AddColumn<int>(
                name: "loc_organizatie_id",
                schema: "mark_ai",
                table: "profil_mentor",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "loc_judet_id",
                schema: "mark_ai",
                table: "camera_tutoring",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "loc_localitate_id",
                schema: "mark_ai",
                table: "camera_tutoring",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "loc_organizatie_id",
                schema: "mark_ai",
                table: "camera_tutoring",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nota_loc",
                schema: "mark_ai",
                table: "camera_tutoring",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tip",
                schema: "mark_ai",
                table: "camera_tutoring",
                type: "text",
                nullable: false,
                defaultValue: "online");

            migrationBuilder.AddColumn<string>(
                name: "cautare",
                schema: "mark_ai",
                table: "parcurs",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(translate(titlu || ' ' || coalesce(descriere, ''), 'ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-', 'aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn '))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "cautare",
                schema: "mark_ai",
                table: "curs",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(translate(titlu || ' ' || coalesce(capitol, '') || ' ' || coalesce(descriere, ''), 'ĂÂÎȘŞȚŢăâîșşțţÁÀÄÉÈËÍÌÏÓÒÖÚÙÜÇÑáàäéèëíìïóòöúùüçñ-', 'aaissttaaissttaaaeeeiiiooouuucnaaaeeeiiiooouuucn '))",
                stored: true);

            migrationBuilder.AddCheckConstraint(
                name: "rezervare_participare_check",
                schema: "mark_ai",
                table: "rezervare",
                sql: "participare IN ('online', 'la_sala')");

            migrationBuilder.CreateIndex(
                name: "idx_profil_mentor_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "profil_mentor",
                column: "loc_organizatie_id");

            migrationBuilder.CreateIndex(
                name: "idx_camera_tutoring_loc_judet_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "loc_judet_id");

            migrationBuilder.CreateIndex(
                name: "idx_camera_tutoring_loc_localitate_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "loc_localitate_id");

            migrationBuilder.CreateIndex(
                name: "idx_camera_tutoring_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "loc_organizatie_id");

            migrationBuilder.AddCheckConstraint(
                name: "camera_tutoring_loc_check",
                schema: "mark_ai",
                table: "camera_tutoring",
                sql: "tip = 'online' OR loc_judet_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "camera_tutoring_tip_check",
                schema: "mark_ai",
                table: "camera_tutoring",
                sql: "tip IN ('online', 'fata_in_fata', 'hibrid')");

            migrationBuilder.AddForeignKey(
                name: "camera_tutoring_loc_judet_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "loc_judet_id",
                principalSchema: "mark_ai",
                principalTable: "judet",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "camera_tutoring_loc_localitate_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "loc_localitate_id",
                principalSchema: "mark_ai",
                principalTable: "localitate",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "camera_tutoring_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring",
                column: "loc_organizatie_id",
                principalSchema: "mark_ai",
                principalTable: "organizatie",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "profil_mentor_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "profil_mentor",
                column: "loc_organizatie_id",
                principalSchema: "mark_ai",
                principalTable: "organizatie",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "camera_tutoring_loc_judet_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropForeignKey(
                name: "camera_tutoring_loc_localitate_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropForeignKey(
                name: "camera_tutoring_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropForeignKey(
                name: "profil_mentor_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropCheckConstraint(
                name: "rezervare_participare_check",
                schema: "mark_ai",
                table: "rezervare");

            migrationBuilder.DropIndex(
                name: "idx_profil_mentor_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropIndex(
                name: "idx_camera_tutoring_loc_judet_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropIndex(
                name: "idx_camera_tutoring_loc_localitate_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropIndex(
                name: "idx_camera_tutoring_loc_organizatie_id_fkey",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropCheckConstraint(
                name: "camera_tutoring_loc_check",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropCheckConstraint(
                name: "camera_tutoring_tip_check",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropColumn(
                name: "cautare",
                schema: "mark_ai",
                table: "parcurs");

            migrationBuilder.DropColumn(
                name: "cautare",
                schema: "mark_ai",
                table: "curs");

            migrationBuilder.DropColumn(
                name: "participare",
                schema: "mark_ai",
                table: "rezervare");

            migrationBuilder.DropColumn(
                name: "loc_organizatie_id",
                schema: "mark_ai",
                table: "profil_mentor");

            migrationBuilder.DropColumn(
                name: "loc_judet_id",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropColumn(
                name: "loc_localitate_id",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropColumn(
                name: "loc_organizatie_id",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropColumn(
                name: "nota_loc",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropColumn(
                name: "tip",
                schema: "mark_ai",
                table: "camera_tutoring");
        }
    }
}
