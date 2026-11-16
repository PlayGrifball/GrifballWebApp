using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class VoiceOptIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Discord_History_Update";
                DROP TRIGGER IF EXISTS "Discord_History_Delete";
                """);

            migrationBuilder.AddColumn<bool>(
                name: "VoiceOptIn",
                schema: "User",
                table: "DiscordHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VoiceOptIn",
                schema: "User",
                table: "Discord",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Discord_History_Update" AFTER UPDATE ON "Discord" FOR EACH ROW BEGIN
                    INSERT INTO "DiscordHistory" ("CreatedAt", "CreatedByID", "DiscordUserID", "DiscordUsername", "ModifiedAt", "ModifiedByID", "PeriodStart", "VoiceOptIn", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DiscordUsername", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."VoiceOptIn", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Discord" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Discord_History_Delete" AFTER DELETE ON "Discord" FOR EACH ROW BEGIN
                    INSERT INTO "DiscordHistory" ("CreatedAt", "CreatedByID", "DiscordUserID", "DiscordUsername", "ModifiedAt", "ModifiedByID", "PeriodStart", "VoiceOptIn", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DiscordUsername", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."VoiceOptIn", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Discord_History_Update";
                DROP TRIGGER IF EXISTS "Discord_History_Delete";
                """);

            migrationBuilder.DropColumn(
                name: "VoiceOptIn",
                schema: "User",
                table: "DiscordHistory");

            migrationBuilder.DropColumn(
                name: "VoiceOptIn",
                schema: "User",
                table: "Discord");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Discord_History_Update" AFTER UPDATE ON "Discord" FOR EACH ROW BEGIN
                    INSERT INTO "DiscordHistory" ("CreatedAt", "CreatedByID", "DiscordUserID", "DiscordUsername", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DiscordUsername", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Discord" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Discord_History_Delete" AFTER DELETE ON "Discord" FOR EACH ROW BEGIN
                    INSERT INTO "DiscordHistory" ("CreatedAt", "CreatedByID", "DiscordUserID", "DiscordUsername", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DiscordUsername", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
