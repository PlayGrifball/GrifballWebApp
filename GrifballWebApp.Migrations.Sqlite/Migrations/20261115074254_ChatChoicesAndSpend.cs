using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ChatChoicesAndSpend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatChoices",
                schema: "Auth",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatChoices", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "ChatChoicesHistory",
                schema: "Auth",
                columns: table => new
                {
                    Model = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ChatMonths",
                schema: "Auth",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CostMicros = table.Column<long>(type: "INTEGER", nullable: false),
                    InputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    Replies = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMonths", x => new { x.UserID, x.Month });
                    table.CheckConstraint("CK_Auth_ChatMonths_NotNegative", "CostMicros >= 0 AND InputTokens >= 0 AND OutputTokens >= 0 AND Replies >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatChoicesHistory_UserID_PeriodEnd",
                schema: "Auth",
                table: "ChatChoicesHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.Sql("""
                CREATE TRIGGER "ChatChoices_History_Update" AFTER UPDATE ON "ChatChoices" FOR EACH ROW BEGIN
                    INSERT INTO "ChatChoicesHistory" ("Model", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."Model", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "ChatChoices" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "ChatChoices_History_Delete" AFTER DELETE ON "ChatChoices" FOR EACH ROW BEGIN
                    INSERT INTO "ChatChoicesHistory" ("Model", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."Model", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "ChatChoices_History_Update";
                DROP TRIGGER IF EXISTS "ChatChoices_History_Delete";
                """);

            migrationBuilder.DropTable(
                name: "ChatChoices",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "ChatChoicesHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "ChatMonths",
                schema: "Auth");
        }
    }
}
