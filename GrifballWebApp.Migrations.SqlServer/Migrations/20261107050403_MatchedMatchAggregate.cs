using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class MatchedMatchAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchedKickVotes",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchedKickVotes",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                columns: new[] { "MatchId", "VoterMatchedPlayerId", "KickMatchedPlayerId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_OneKindOfResult",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "MatchID IS NULL OR RecordedGameID IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_ReportedHasResult",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "Status <> 2 OR MatchID IS NOT NULL OR RecordedGameID IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_OneKindOfResult",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_ReportedHasResult",
                schema: "Matchmaking",
                table: "MatchedMatches");

            // The old key holds one kick vote a voter a match: each voter keeps the one against the player with the lowest key
            migrationBuilder.Sql("DELETE k FROM [Matchmaking].[MatchedKickVotes] AS k WHERE EXISTS (SELECT 1 FROM [Matchmaking].[MatchedKickVotes] AS o WHERE o.[MatchId] = k.[MatchId] AND o.[VoterMatchedPlayerId] = k.[VoterMatchedPlayerId] AND o.[KickMatchedPlayerId] < k.[KickMatchedPlayerId]);");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchedKickVotes",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchedKickVotes",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                columns: new[] { "MatchId", "VoterMatchedPlayerId" });
        }
    }
}
