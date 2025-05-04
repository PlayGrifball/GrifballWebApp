using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class MatchedPlayerPKSystemVersionAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
