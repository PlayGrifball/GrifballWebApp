using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class MatchedMatchVoteMessageNeedsThread : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settled before the check: a vote message with no thread to be posted in is no message (nothing was ever voted on it)
            migrationBuilder.Sql("UPDATE [Matchmaking].[MatchedMatches] SET [VoteMessageID] = NULL WHERE [VoteMessageID] IS NOT NULL AND [ThreadID] IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_VoteMessageNeedsThread",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "VoteMessageID IS NULL OR ThreadID IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_VoteMessageNeedsThread",
                schema: "Matchmaking",
                table: "MatchedMatches");
        }
    }
}
