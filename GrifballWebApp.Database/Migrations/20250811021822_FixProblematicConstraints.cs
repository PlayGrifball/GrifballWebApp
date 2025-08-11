using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class FixProblematicConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches",
                sql: "(HomeTeamID IS NULL) OR (AwayTeamID IS NULL) OR (HomeTeamID != AwayTeamID)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo",
                sql: "(AwayTeamSeedNumber IS NOT NULL AND AwayTeamPreviousMatchBracketInfoID IS NULL) OR (AwayTeamPreviousMatchBracketInfoID IS NOT NULL AND AwayTeamSeedNumber IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo",
                sql: "(HomeTeamSeedNumber IS NOT NULL AND HomeTeamPreviousMatchBracketInfoID IS NULL) OR (HomeTeamPreviousMatchBracketInfoID IS NOT NULL AND HomeTeamSeedNumber IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches",
                sql: "\r\n(HomeTeamID IS NULL) OR\r\n(AwayTeamID IS NULL) OR\r\n(HomeTeamID != AwayTeamID)\r\n");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo",
                sql: "\r\n(AwayTeamSeedNumber IS NOT NULL AND AwayTeamPreviousMatchBracketInfoID IS NULL) OR\r\n(AwayTeamPreviousMatchBracketInfoID IS NOT NULL AND AwayTeamSeedNumber IS NULL)\r\n");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo",
                sql: "\r\n(HomeTeamSeedNumber IS NOT NULL AND HomeTeamPreviousMatchBracketInfoID IS NULL) OR\r\n(HomeTeamPreviousMatchBracketInfoID IS NOT NULL AND HomeTeamSeedNumber IS NULL)\r\n");
        }
    }
}
