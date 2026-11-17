using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    MinPlayersAvailable = table.Column<int>(type: "integer", nullable: false),
                    MaxMatchesPerTeamPerNight = table.Column<int>(type: "integer", nullable: false),
                    MaxMatchesPerTeamPerWeek = table.Column<int>(type: "integer", nullable: false),
                    MaxConcurrentMatches = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()")
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
                    MaxConcurrentMatches = table.Column<int>(type: "integer", nullable: true),
                    MaxMatchesPerTeamPerNight = table.Column<int>(type: "integer", nullable: true),
                    MaxMatchesPerTeamPerWeek = table.Column<int>(type: "integer", nullable: true),
                    MinPlayersAvailable = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeasonID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSchedulingRulesHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "SeasonSchedulingRulesHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"SeasonSchedulingRules\"');");

            // Recommendations saved before this hold the league's Eastern wall-clock times, which applying would now take for
            // UTC: the ones not applied yet go (run the scheduler again). Their times first, as SQLite migrates with foreign keys off.
            migrationBuilder.Sql("""
                DELETE FROM "Event"."ScheduleRecommendationMatches" WHERE "ScheduleRecommendationID" IN (SELECT "ScheduleRecommendationID" FROM "Event"."ScheduleRecommendations" WHERE "AppliedAt" IS NULL);
                DELETE FROM "Event"."ScheduleRecommendations" WHERE "AppliedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"SeasonSchedulingRules\"');");

            migrationBuilder.DropTable(
                name: "SeasonSchedulingRules",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonSchedulingRulesHistory",
                schema: "Event");
        }
    }
}
