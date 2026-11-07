using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class MatchedPlayerUserRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchedPlayers_Users_UserID",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedPlayers_Users_UserID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "UserID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchedPlayers_Users_UserID",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedPlayers_Users_UserID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "UserID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
