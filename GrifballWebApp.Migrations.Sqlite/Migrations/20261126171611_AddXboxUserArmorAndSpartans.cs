using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddXboxUserArmorAndSpartans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Users_History_Update";
                DROP TRIGGER IF EXISTS "Users_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "ProfilePicture",
                schema: "Auth",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserSpartanRenders",
                schema: "Xbox",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RenderedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LoadoutKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    Error = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpartanRenders", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSpartanRenders_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSpartans",
                schema: "Xbox",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChoiceJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false, collation: "NOCASE"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpartans", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSpartans_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSpartansHistory",
                schema: "Xbox",
                columns: table => new
                {
                    ChoiceJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "XboxUserArmor",
                schema: "Xbox",
                columns: table => new
                {
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CoreId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    CorePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    ThemePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    CoatingPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    HelmetPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    VisorPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    GlovePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    HelmetAttachmentPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    ChestAttachmentPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    KneePadPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    LeftShoulderPadPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    RightShoulderPadPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    HipAttachmentPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    WristAttachmentPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    ArmorFxPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    MythicFxPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    ActionPosePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    StancePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    BodyType = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    LeftArm = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    RightArm = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    LeftLeg = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    RightLeg = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XboxUserArmor", x => x.XboxUserID);
                    table.ForeignKey(
                        name: "FK_XboxUserArmor_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSpartanFiles",
                schema: "Xbox",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false, collation: "NOCASE"),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpartanFiles", x => new { x.UserId, x.Name });
                    table.ForeignKey(
                        name: "FK_UserSpartanFiles_UserSpartanRenders_UserId",
                        column: x => x.UserId,
                        principalSchema: "Xbox",
                        principalTable: "UserSpartanRenders",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users",
                sql: "ProfilePicture IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_UserSpartanRenders_State_RequestedAt",
                schema: "Xbox",
                table: "UserSpartanRenders",
                columns: new[] { "State", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSpartansHistory_UserId_PeriodEnd",
                schema: "Xbox",
                table: "UserSpartansHistory",
                columns: new[] { "UserId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_XboxUserArmor_CheckedAt",
                schema: "Xbox",
                table: "XboxUserArmor",
                column: "CheckedAt");

            migrationBuilder.Sql("""
                CREATE TRIGGER "UserSpartans_History_Update" AFTER UPDATE ON "UserSpartans" FOR EACH ROW BEGIN
                    INSERT INTO "UserSpartansHistory" ("ChoiceJson", "PeriodStart", "UpdatedAt", "UserId", "PeriodEnd") VALUES (OLD."ChoiceJson", OLD."PeriodStart", OLD."UpdatedAt", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "UserSpartans" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "UserSpartans_History_Delete" AFTER DELETE ON "UserSpartans" FOR EACH ROW BEGIN
                    INSERT INTO "UserSpartansHistory" ("ChoiceJson", "PeriodStart", "UpdatedAt", "UserId", "PeriodEnd") VALUES (OLD."ChoiceJson", OLD."PeriodStart", OLD."UpdatedAt", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Users_History_Update" AFTER UPDATE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "ProfilePicture", "RegionID", "RegionSource", "SecurityStamp", "Theme", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."ProfilePicture", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."Theme", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Users" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Users_History_Delete" AFTER DELETE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "ProfilePicture", "RegionID", "RegionSource", "SecurityStamp", "Theme", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."ProfilePicture", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."Theme", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("UPDATE \"Users\" SET \"ProfilePicture\" = 2 WHERE \"ProfilePicture\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "UserSpartans_History_Update";
                DROP TRIGGER IF EXISTS "UserSpartans_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Users_History_Update";
                DROP TRIGGER IF EXISTS "Users_History_Delete";
                """);

            migrationBuilder.DropTable(
                name: "UserSpartanFiles",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "UserSpartans",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "UserSpartansHistory",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "XboxUserArmor",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "UserSpartanRenders",
                schema: "Xbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "ProfilePicture",
                schema: "Auth",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users",
                sql: "ProfilePicture IN (0, 1)");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Users_History_Update" AFTER UPDATE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "ProfilePicture", "RegionID", "RegionSource", "SecurityStamp", "Theme", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."ProfilePicture", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."Theme", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Users" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Users_History_Delete" AFTER DELETE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "ProfilePicture", "RegionID", "RegionSource", "SecurityStamp", "Theme", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."ProfilePicture", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."Theme", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
