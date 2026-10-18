using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkIntalnire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "link_intalnire",
                schema: "mark_ai",
                table: "camera_tutoring",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "camera_tutoring_link_intalnire_check",
                schema: "mark_ai",
                table: "camera_tutoring",
                sql: "link_intalnire IS NULL OR link_intalnire ~ '^https://'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "camera_tutoring_link_intalnire_check",
                schema: "mark_ai",
                table: "camera_tutoring");

            migrationBuilder.DropColumn(
                name: "link_intalnire",
                schema: "mark_ai",
                table: "camera_tutoring");
        }
    }
}
