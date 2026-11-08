using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class RecordedGameAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the rows the constraints would refuse first (the old model's public setters took any state): an origin
            // the model doesn't know is the import's (every game before manual match entry was); a game entered by hand
            // leaves a season of its own (it counts through its series game); a side's unknown result is none (DidNotFinish,
            // which the stats count as neither a win nor a loss, as before)
            migrationBuilder.Sql("""
                UPDATE [Recorded].[Games] SET [Origin] = 1 WHERE [Origin] NOT IN (1, 2);
                UPDATE [Recorded].[Games] SET [SeasonID] = NULL WHERE [Origin] = 2 AND [SeasonID] IS NOT NULL;
                UPDATE [Recorded].[GameTeams] SET [Outcome] = 4 WHERE [Outcome] NOT IN (1, 2, 3, 4);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recorded_GameTeams_KnownOutcome",
                schema: "Recorded",
                table: "GameTeams",
                sql: "Outcome IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recorded_Games_KnownOrigin",
                schema: "Recorded",
                table: "Games",
                sql: "Origin IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recorded_Games_ManualHasNoSeason",
                schema: "Recorded",
                table: "Games",
                sql: "Origin <> 2 OR SeasonID IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled rows stay settled: they were in no state a recorded game can have
            migrationBuilder.DropCheckConstraint(
                name: "CK_Recorded_GameTeams_KnownOutcome",
                schema: "Recorded",
                table: "GameTeams");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recorded_Games_KnownOrigin",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recorded_Games_ManualHasNoSeason",
                schema: "Recorded",
                table: "Games");
        }
    }
}
