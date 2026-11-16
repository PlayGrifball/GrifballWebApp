using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class VoiceConsents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoiceConsents",
                schema: "User",
                columns: table => new
                {
                    DiscordUserID = table.Column<long>(type: "INTEGER", nullable: false),
                    GivenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceConsents", x => x.DiscordUserID);
                });

            migrationBuilder.CreateTable(
                name: "VoiceConsentsHistory",
                schema: "User",
                columns: table => new
                {
                    DiscordUserID = table.Column<long>(type: "INTEGER", nullable: true),
                    GivenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceConsentsHistory_DiscordUserID_PeriodEnd",
                schema: "User",
                table: "VoiceConsentsHistory",
                columns: new[] { "DiscordUserID", "PeriodEnd" });

            migrationBuilder.Sql("""
                CREATE TRIGGER "VoiceConsents_History_Update" AFTER UPDATE ON "VoiceConsents" FOR EACH ROW BEGIN
                    INSERT INTO "VoiceConsentsHistory" ("DiscordUserID", "GivenAt", "PeriodStart", "PeriodEnd") VALUES (OLD."DiscordUserID", OLD."GivenAt", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "VoiceConsents" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "VoiceConsents_History_Delete" AFTER DELETE ON "VoiceConsents" FOR EACH ROW BEGIN
                    INSERT INTO "VoiceConsentsHistory" ("DiscordUserID", "GivenAt", "PeriodStart", "PeriodEnd") VALUES (OLD."DiscordUserID", OLD."GivenAt", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "VoiceConsents_History_Update";
                DROP TRIGGER IF EXISTS "VoiceConsents_History_Delete";
                """);

            migrationBuilder.DropTable(
                name: "VoiceConsents",
                schema: "User");

            migrationBuilder.DropTable(
                name: "VoiceConsentsHistory",
                schema: "User");
        }
    }
}
