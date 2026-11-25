using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ProfilePictureChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Users_History_Update";
                DROP TRIGGER IF EXISTS "Users_History_Delete";
                """);

            migrationBuilder.AddColumn<int>(
                name: "ProfilePicture",
                schema: "Auth",
                table: "UsersHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfilePicture",
                schema: "Auth",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users",
                sql: "ProfilePicture IN (1, 2)");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Users_History_Update" AFTER UPDATE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "ProfilePicture", "RegionID", "RegionSource", "SecurityStamp", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."ProfilePicture", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Users" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Users_History_Delete" AFTER DELETE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "ProfilePicture", "RegionID", "RegionSource", "SecurityStamp", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."ProfilePicture", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Users_History_Update";
                DROP TRIGGER IF EXISTS "Users_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfilePicture",
                schema: "Auth",
                table: "UsersHistory");

            migrationBuilder.DropColumn(
                name: "ProfilePicture",
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
