using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
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
                    SeasonID = table.Column<int>(type: "int", nullable: false),
                    MinPlayersAvailable = table.Column<int>(type: "int", nullable: false),
                    MaxMatchesPerTeamPerNight = table.Column<int>(type: "int", nullable: false),
                    MaxMatchesPerTeamPerWeek = table.Column<int>(type: "int", nullable: false),
                    MaxConcurrentMatches = table.Column<int>(type: "int", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonSchedulingRulesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            // Recommendations saved before this hold the league's Eastern wall-clock times, which applying would now take for
            // UTC: the ones not applied yet go (run the scheduler again). Their times first, as SQLite migrates with foreign keys off.
            migrationBuilder.Sql("""
                DELETE FROM [Event].[ScheduleRecommendationMatches] WHERE [ScheduleRecommendationID] IN (SELECT [ScheduleRecommendationID] FROM [Event].[ScheduleRecommendations] WHERE [AppliedAt] IS NULL);
                DELETE FROM [Event].[ScheduleRecommendations] WHERE [AppliedAt] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeasonSchedulingRules",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonSchedulingRulesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
