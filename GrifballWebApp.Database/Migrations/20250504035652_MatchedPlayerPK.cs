using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class MatchedPlayerPK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchedPlayers_Discord_DiscordUserID",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchedPlayers",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedPlayers")
                .Annotation("SqlServer:TemporalIsPeriodEndColumn", true);

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedPlayers")
                .Annotation("SqlServer:TemporalIsPeriodStartColumn", true);

            migrationBuilder.AlterTable(
                name: "MatchedPlayers",
                schema: "Matchmaking")
                .OldAnnotation("SqlServer:IsTemporal", true)
                .OldAnnotation("SqlServer:TemporalHistoryTableName", "MatchedPlayersHistory")
                .OldAnnotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .OldAnnotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .OldAnnotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchedPlayers",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedPlayers_DiscordUserID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "DiscordUserID");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedPlayers_Discord_DiscordUserID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "DiscordUserID",
                principalSchema: "User",
                principalTable: "Discord",
                principalColumn: "DiscordUserID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchedPlayers_Discord_DiscordUserID",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchedPlayers",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropIndex(
                name: "IX_MatchedPlayers_DiscordUserID",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.AlterTable(
                name: "MatchedPlayers",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedPlayersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:TemporalIsPeriodEndColumn", true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:TemporalIsPeriodStartColumn", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchedPlayers",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "DiscordUserID");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedPlayers_Discord_DiscordUserID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "DiscordUserID",
                principalSchema: "User",
                principalTable: "Discord",
                principalColumn: "DiscordUserID");
        }
    }
}
