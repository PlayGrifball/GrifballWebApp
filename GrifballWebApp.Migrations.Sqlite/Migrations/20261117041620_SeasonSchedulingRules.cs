using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SeasonSchedulingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeasonSchedulingRules",
                schema: "Event",
                columns: table => new
                {
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    MinPlayersAvailable = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxMatchesPerTeamPerNight = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxMatchesPerTeamPerWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxConcurrentMatches = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonSchedulingRules", x => x.SeasonID);
                    table.ForeignKey(
                        name: "FK_SeasonSchedulingRules_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeasonSchedulingRulesHistory",
                schema: "Event",
                columns: table => new
                {
                    MaxConcurrentMatches = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxMatchesPerTeamPerNight = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxMatchesPerTeamPerWeek = table.Column<int>(type: "INTEGER", nullable: true),
                    MinPlayersAvailable = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSchedulingRulesHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "SeasonSchedulingRulesHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeasonSchedulingRules_History_Update" AFTER UPDATE ON "SeasonSchedulingRules" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonSchedulingRulesHistory" ("MaxConcurrentMatches", "MaxMatchesPerTeamPerNight", "MaxMatchesPerTeamPerWeek", "MinPlayersAvailable", "PeriodStart", "SeasonID", "PeriodEnd") VALUES (OLD."MaxConcurrentMatches", OLD."MaxMatchesPerTeamPerNight", OLD."MaxMatchesPerTeamPerWeek", OLD."MinPlayersAvailable", OLD."PeriodStart", OLD."SeasonID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeasonSchedulingRules" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeasonSchedulingRules_History_Delete" AFTER DELETE ON "SeasonSchedulingRules" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonSchedulingRulesHistory" ("MaxConcurrentMatches", "MaxMatchesPerTeamPerNight", "MaxMatchesPerTeamPerWeek", "MinPlayersAvailable", "PeriodStart", "SeasonID", "PeriodEnd") VALUES (OLD."MaxConcurrentMatches", OLD."MaxMatchesPerTeamPerNight", OLD."MaxMatchesPerTeamPerWeek", OLD."MinPlayersAvailable", OLD."PeriodStart", OLD."SeasonID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            // Recommendations saved before this hold the league's Eastern wall-clock times, which applying would now take for
            // UTC: the ones not applied yet go (run the scheduler again). Their times first, as SQLite migrates with foreign keys off.
            migrationBuilder.Sql("""
                DELETE FROM "ScheduleRecommendationMatches" WHERE "ScheduleRecommendationID" IN (SELECT "ScheduleRecommendationID" FROM "ScheduleRecommendations" WHERE "AppliedAt" IS NULL);
                DELETE FROM "ScheduleRecommendations" WHERE "AppliedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeasonSchedulingRules_History_Update";
                DROP TRIGGER IF EXISTS "SeasonSchedulingRules_History_Delete";
                """);

            migrationBuilder.DropTable(
                name: "SeasonSchedulingRules",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonSchedulingRulesHistory",
                schema: "Event");
        }
    }
}
