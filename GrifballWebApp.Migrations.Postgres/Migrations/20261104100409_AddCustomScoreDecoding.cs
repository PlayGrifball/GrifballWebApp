using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                table: "MatchParticipantsHistory",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BallPunches",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarrierKills",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoalAssists",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Goals",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassIns",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlateStops",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SwordKills",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "BallHeldTime",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BallPunches",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarrierKills",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoalAssists",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Goals",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassIns",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlateStops",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SwordKills",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Infinite\".\"MatchParticipants\"');");

            migrationBuilder.AddColumn<int>(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "MatchesHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "Matches",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Infinite\".\"Matches\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BallHeldTime",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "BallPunches",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "CarrierKills",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "GoalAssists",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "Goals",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "PassIns",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "PlateStops",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "SwordKills",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

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

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Infinite\".\"MatchParticipants\"');");

            migrationBuilder.DropColumn(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "MatchesHistory");

            migrationBuilder.DropColumn(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Infinite\".\"Matches\"');");
        }
    }
}
