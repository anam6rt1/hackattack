using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarkAi.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContactSiIndexMembri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The pilot's feedback box becomes the contact page: the same table, renamed and widened, so earlier messages stay.
            migrationBuilder.RenameTable(name: "feedback_pilot", schema: "mark_ai", newName: "mesaj_contact", newSchema: "mark_ai");
            migrationBuilder.Sql("ALTER TABLE mark_ai.mesaj_contact RENAME CONSTRAINT \"PK_feedback_pilot\" TO \"PK_mesaj_contact\";");
            migrationBuilder.Sql("ALTER TABLE mark_ai.mesaj_contact RENAME CONSTRAINT \"FK_feedback_pilot_utilizator_utilizator_id\" TO \"FK_mesaj_contact_utilizator_utilizator_id\";");
            migrationBuilder.RenameIndex(name: "IX_feedback_pilot_utilizator_id_creat_la", schema: "mark_ai", table: "mesaj_contact",
                newName: "IX_mesaj_contact_utilizator_id_creat_la");

            migrationBuilder.AddColumn<string>(name: "nume", schema: "mark_ai", table: "mesaj_contact",
                type: "character varying(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "email", schema: "mark_ai", table: "mesaj_contact",
                type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "subiect", schema: "mark_ai", table: "mesaj_contact",
                type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "altceva");
            migrationBuilder.AlterColumn<string>(name: "text", schema: "mark_ai", table: "mesaj_contact",
                type: "character varying(4000)", maxLength: 4000, nullable: false,
                oldClrType: typeof(string), oldType: "character varying(2000)", oldMaxLength: 2000);

            migrationBuilder.CreateIndex(
                name: "IX_mesaj_contact_email_creat_la",
                schema: "mark_ai",
                table: "mesaj_contact",
                columns: new[] { "email", "creat_la" });

            migrationBuilder.CreateIndex(
                name: "idx_membru_organizatie_utilizator",
                schema: "mark_ai",
                table: "membru_organizatie",
                column: "utilizator_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "idx_membru_organizatie_utilizator", schema: "mark_ai", table: "membru_organizatie");
            migrationBuilder.DropIndex(name: "IX_mesaj_contact_email_creat_la", schema: "mark_ai", table: "mesaj_contact");
            // Messages longer than the old limit are cut, so the column can narrow again.
            migrationBuilder.Sql("UPDATE mark_ai.mesaj_contact SET text = left(text, 2000) WHERE length(text) > 2000;");
            migrationBuilder.AlterColumn<string>(name: "text", schema: "mark_ai", table: "mesaj_contact",
                type: "character varying(2000)", maxLength: 2000, nullable: false,
                oldClrType: typeof(string), oldType: "character varying(4000)", oldMaxLength: 4000);
            migrationBuilder.DropColumn(name: "subiect", schema: "mark_ai", table: "mesaj_contact");
            migrationBuilder.DropColumn(name: "email", schema: "mark_ai", table: "mesaj_contact");
            migrationBuilder.DropColumn(name: "nume", schema: "mark_ai", table: "mesaj_contact");
            migrationBuilder.RenameIndex(name: "IX_mesaj_contact_utilizator_id_creat_la", schema: "mark_ai", table: "mesaj_contact",
                newName: "IX_feedback_pilot_utilizator_id_creat_la");
            migrationBuilder.Sql("ALTER TABLE mark_ai.mesaj_contact RENAME CONSTRAINT \"FK_mesaj_contact_utilizator_utilizator_id\" TO \"FK_feedback_pilot_utilizator_utilizator_id\";");
            migrationBuilder.Sql("ALTER TABLE mark_ai.mesaj_contact RENAME CONSTRAINT \"PK_mesaj_contact\" TO \"PK_feedback_pilot\";");
            migrationBuilder.RenameTable(name: "mesaj_contact", schema: "mark_ai", newName: "feedback_pilot", newSchema: "mark_ai");
        }
    }
}
