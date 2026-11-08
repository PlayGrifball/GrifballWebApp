using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DiscordUserAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A blank username, which the old public setter allowed, goes by its ID (as DiscordUser.FromDiscord names one) before
            // the constraint refuses it
            migrationBuilder.Sql("""UPDATE "Discord" SET "DiscordUsername" = CAST("DiscordUserID" AS TEXT) WHERE TRIM("DiscordUsername") = '';""");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Discord_History_Update";
                DROP TRIGGER IF EXISTS "Discord_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_Discord_UsernameNotBlank",
                schema: "User",
                table: "Discord",
                sql: "TRIM(DiscordUsername) <> ''");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Discord_History_Update";
                DROP TRIGGER IF EXISTS "Discord_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_Discord_UsernameNotBlank",
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
