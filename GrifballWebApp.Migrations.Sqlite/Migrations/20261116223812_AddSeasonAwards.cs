using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasonAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeasonAwards",
                schema: "Event",
                columns: table => new
                {
                    SeasonAwardID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    WinnerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    Value = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonAwards", x => x.SeasonAwardID);
                    table.CheckConstraint("CK_Event_SeasonAwards_PlayerOrTeam", "UserID IS NOT NULL OR TeamID IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_SeasonAwards_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonAwards_Teams_TeamID",
                        column: x => x.TeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID");
                    table.ForeignKey(
                        name: "FK_SeasonAwards_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeasonAwardsHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonAwardID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true),
                    Value = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    WinnerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwards_SeasonID_Name_WinnerName",
                schema: "Event",
                table: "SeasonAwards",
                columns: new[] { "SeasonID", "Name", "WinnerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwards_TeamID",
                schema: "Event",
                table: "SeasonAwards",
                column: "TeamID");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwards_UserID",
                schema: "Event",
                table: "SeasonAwards",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwardsHistory_SeasonAwardID_PeriodEnd",
                schema: "Event",
                table: "SeasonAwardsHistory",
                columns: new[] { "SeasonAwardID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwardsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "SeasonAwardsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwardsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "SeasonAwardsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwardsHistory_UserID_PeriodEnd",
                schema: "Event",
                table: "SeasonAwardsHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeasonAwards_History_Update" AFTER UPDATE ON "SeasonAwards" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonAwardsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "Position", "SeasonAwardID", "SeasonID", "TeamID", "UserID", "Value", "WinnerName", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", OLD."Position", OLD."SeasonAwardID", OLD."SeasonID", OLD."TeamID", OLD."UserID", OLD."Value", OLD."WinnerName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeasonAwards" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeasonAwards_History_Delete" AFTER DELETE ON "SeasonAwards" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonAwardsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "Position", "SeasonAwardID", "SeasonID", "TeamID", "UserID", "Value", "WinnerName", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", OLD."Position", OLD."SeasonAwardID", OLD."SeasonID", OLD."TeamID", OLD."UserID", OLD."Value", OLD."WinnerName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeasonAwards_History_Update";
                DROP TRIGGER IF EXISTS "SeasonAwards_History_Delete";
                """);

            migrationBuilder.DropTable(
                name: "SeasonAwards",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonAwardsHistory",
                schema: "Event");
        }
    }
}
