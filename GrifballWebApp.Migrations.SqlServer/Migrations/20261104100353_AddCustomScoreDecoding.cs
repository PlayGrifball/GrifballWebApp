using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomScoreDecoding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "BallHeldTime",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BallPunches",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarrierKills",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoalAssists",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Goals",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassIns",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlateStops",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SwordKills",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "Matches",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BallHeldTime",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "BallPunches",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "CarrierKills",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "GoalAssists",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "Goals",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "PassIns",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "PlateStops",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "SwordKills",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "Matches");
        }
    }
}
