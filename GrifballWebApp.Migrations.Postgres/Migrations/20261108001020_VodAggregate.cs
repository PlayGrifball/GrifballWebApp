using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class VodAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the rows the constraints would refuse (the old model's public setters took any state): a VOD of a match is
            // of the match's season; a confirmed VOD with no season (the Late League VODs whose season MoveLateLeagueToMatchmaking
            // cleared) is dismissed, which keeps the sysadmin's decision and the VOD hidden, with a note saying why; an Auto VOD
            // with no season, or one in a status the model doesn't know, goes back to review. Each row is otherwise as it was.
            migrationBuilder.Sql("UPDATE \"Event\".\"Vods\" SET \"SeasonID\" = (SELECT m.\"SeasonID\" FROM \"Event\".\"SeasonMatches\" AS m WHERE m.\"SeasonMatchID\" = \"Vods\".\"SeasonMatchID\") WHERE \"SeasonID\" IS NULL AND \"SeasonMatchID\" IS NOT NULL;");
            migrationBuilder.Sql("UPDATE \"Event\".\"Vods\" SET \"MatchStatus\" = 2 WHERE \"MatchStatus\" NOT IN (0, 1, 2, 3);");
            migrationBuilder.Sql("UPDATE \"Event\".\"Vods\" SET \"MatchStatus\" = 3, \"MatchNote\" = 'Dismissed by the VodAggregate migration: confirmed without a season (a Late League VOD)' WHERE \"MatchStatus\" = 1 AND \"SeasonID\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"Event\".\"Vods\" SET \"MatchStatus\" = 2 WHERE \"MatchStatus\" = 0 AND \"SeasonID\" IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Vods_KnownStatus",
                schema: "Event",
                table: "Vods",
                sql: "\"MatchStatus\" IN (0, 1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Vods_LinkedHasSeason",
                schema: "Event",
                table: "Vods",
                sql: "\"MatchStatus\" NOT IN (0, 1) OR \"SeasonID\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Vods_MatchHasSeason",
                schema: "Event",
                table: "Vods",
                sql: "\"SeasonMatchID\" IS NULL OR \"SeasonID\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled rows stay settled: they were in no state a VOD can have
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Vods_KnownStatus",
                schema: "Event",
                table: "Vods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Vods_LinkedHasSeason",
                schema: "Event",
                table: "Vods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Vods_MatchHasSeason",
                schema: "Event",
                table: "Vods");
        }
    }
}
