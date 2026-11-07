using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class MatchedMatchLinksRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "RecordedGameID",
                principalSchema: "Recorded",
                principalTable: "Games",
                principalColumn: "RecordedGameID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "MatchmakingSeasonID",
                principalSchema: "Matchmaking",
                principalTable: "MatchmakingSeasons",
                principalColumn: "MatchmakingSeasonID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "RecordedGameID",
                principalSchema: "Recorded",
                principalTable: "Games",
                principalColumn: "RecordedGameID");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "MatchmakingSeasonID",
                principalSchema: "Matchmaking",
                principalTable: "MatchmakingSeasons",
                principalColumn: "MatchmakingSeasonID");
        }
    }
}
