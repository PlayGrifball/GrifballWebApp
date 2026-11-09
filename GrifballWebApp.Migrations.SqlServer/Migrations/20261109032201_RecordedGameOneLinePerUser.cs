using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class RecordedGameOneLinePerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One line a player in a game: every line of a user in a game but the oldest (lowest key) goes, as
            // RecordedGame.SetPlayers keeps it, before the unique index. Such a line came from a merge of two accounts that
            // both played the game, and counted the player twice. The table's history keeps the deleted lines.
            migrationBuilder.Sql("""
                DELETE p FROM [Recorded].[GameParticipants] AS p
                WHERE EXISTS (SELECT 1 FROM [Recorded].[GameParticipants] AS k
                    WHERE k.[RecordedGameID] = p.[RecordedGameID] AND k.[UserID] = p.[UserID] AND k.[RecordedGameParticipantID] < p.[RecordedGameParticipantID]);
                """);

            migrationBuilder.DropIndex(
                name: "IX_GameParticipants_RecordedGameID",
                schema: "Recorded",
                table: "GameParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_RecordedGameID_UserID",
                schema: "Recorded",
                table: "GameParticipants",
                columns: new[] { "RecordedGameID", "UserID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The deleted lines stay gone: a second line of one player in a game is no state a recorded game can have
            migrationBuilder.DropIndex(
                name: "IX_GameParticipants_RecordedGameID_UserID",
                schema: "Recorded",
                table: "GameParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_RecordedGameID",
                schema: "Recorded",
                table: "GameParticipants",
                column: "RecordedGameID");
        }
    }
}
