using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MatchID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatches_MatchID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "MatchID",
                unique: true,
                filter: "[MatchID] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedMatches_Matches_MatchID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "MatchID",
                principalSchema: "Infinite",
                principalTable: "Matches",
                principalColumn: "MatchID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_Matches_MatchID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropIndex(
                name: "IX_MatchedMatches_MatchID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "Active",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "MatchID",
                schema: "Matchmaking",
                table: "MatchedMatches");
        }
    }
}
