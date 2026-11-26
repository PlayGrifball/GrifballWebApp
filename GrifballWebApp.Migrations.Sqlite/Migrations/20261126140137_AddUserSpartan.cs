using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSpartan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.Sql("""
                CREATE TRIGGER "UserSpartans_History_Update" AFTER UPDATE ON "UserSpartans" FOR EACH ROW BEGIN
                    INSERT INTO "UserSpartansHistory" ("ChoiceJson", "PeriodStart", "UpdatedAt", "UserId", "PeriodEnd") VALUES (OLD."ChoiceJson", OLD."PeriodStart", OLD."UpdatedAt", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "UserSpartans" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "UserSpartans_History_Delete" AFTER DELETE ON "UserSpartans" FOR EACH ROW BEGIN
                    INSERT INTO "UserSpartansHistory" ("ChoiceJson", "PeriodStart", "UpdatedAt", "UserId", "PeriodEnd") VALUES (OLD."ChoiceJson", OLD."PeriodStart", OLD."UpdatedAt", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "UserSpartans_History_Update";
                DROP TRIGGER IF EXISTS "UserSpartans_History_Delete";
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
                name: "UserSpartanRenders",
                schema: "Xbox");
        }
    }
}
