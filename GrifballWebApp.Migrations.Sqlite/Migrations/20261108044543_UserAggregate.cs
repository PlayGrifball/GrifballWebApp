using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UserAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the rows the constraints would refuse (the old model's public setters took any state). A region with no
            // chooser (set before RegionSource was added) or an unknown one is the user's choice, which the import keeps; a
            // chooser of no region goes. In the Late League record nothing is below zero; two streaks at once, neither knowable,
            // both end; a streak longer than its wins or losses (a merge's source kept its streak) is cut to them. Each row is
            // otherwise as it was.
            migrationBuilder.Sql("UPDATE \"Users\" SET \"RegionSource\" = NULL WHERE \"RegionID\" IS NULL AND \"RegionSource\" IS NOT NULL;");
            migrationBuilder.Sql("UPDATE \"Users\" SET \"RegionSource\" = 1 WHERE \"RegionID\" IS NOT NULL AND (\"RegionSource\" IS NULL OR \"RegionSource\" NOT IN (1, 2, 3));");
            migrationBuilder.Sql("UPDATE \"Users\" SET \"MMR\" = CASE WHEN \"MMR\" < 0 THEN 0 ELSE \"MMR\" END, \"Wins\" = CASE WHEN \"Wins\" < 0 THEN 0 ELSE \"Wins\" END, \"Losses\" = CASE WHEN \"Losses\" < 0 THEN 0 ELSE \"Losses\" END, \"WinStreak\" = CASE WHEN \"WinStreak\" < 0 THEN 0 ELSE \"WinStreak\" END, \"LossStreak\" = CASE WHEN \"LossStreak\" < 0 THEN 0 ELSE \"LossStreak\" END WHERE \"MMR\" < 0 OR \"Wins\" < 0 OR \"Losses\" < 0 OR \"WinStreak\" < 0 OR \"LossStreak\" < 0;");
            migrationBuilder.Sql("UPDATE \"Users\" SET \"WinStreak\" = 0, \"LossStreak\" = 0 WHERE \"WinStreak\" > 0 AND \"LossStreak\" > 0;");
            migrationBuilder.Sql("UPDATE \"Users\" SET \"WinStreak\" = \"Wins\" WHERE \"WinStreak\" > \"Wins\";");
            migrationBuilder.Sql("UPDATE \"Users\" SET \"LossStreak\" = \"Losses\" WHERE \"LossStreak\" > \"Losses\";");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Users_History_Update";
                DROP TRIGGER IF EXISTS "Users_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_KnownRegionSource",
                schema: "Auth",
                table: "Users",
                sql: "RegionSource IS NULL OR RegionSource IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_LateLeagueRecord",
                schema: "Auth",
                table: "Users",
                sql: "MMR >= 0 AND Wins >= 0 AND Losses >= 0 AND WinStreak >= 0 AND LossStreak >= 0 AND (WinStreak = 0 OR LossStreak = 0) AND WinStreak <= Wins AND LossStreak <= Losses");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_RegionHasSource",
                schema: "Auth",
                table: "Users",
                sql: "(RegionID IS NULL AND RegionSource IS NULL) OR (RegionID IS NOT NULL AND RegionSource IS NOT NULL)");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Users_History_Update" AFTER UPDATE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "RegionID", "RegionSource", "SecurityStamp", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Users" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Users_History_Delete" AFTER DELETE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "RegionID", "RegionSource", "SecurityStamp", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled rows stay settled: they were in no state a user can have
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Users_History_Update";
                DROP TRIGGER IF EXISTS "Users_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_Users_KnownRegionSource",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_Users_LateLeagueRecord",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_Users_RegionHasSource",
                schema: "Auth",
                table: "Users");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Users_History_Update" AFTER UPDATE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "RegionID", "RegionSource", "SecurityStamp", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Users" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Users_History_Delete" AFTER DELETE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "RegionID", "RegionSource", "SecurityStamp", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
