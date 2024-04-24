using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class teamstable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchParticipants_Matches_MatchID",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.AddColumn<int>(
                name: "PersonalScore",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "Score",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                schema: "Infinite",
                table: "Matches",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0))
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchTeams",
                schema: "Infinite",
                columns: table => new
                {
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    TeamID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Score = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Outcome = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchTeams", x => new { x.MatchID, x.TeamID });
                    table.ForeignKey(
                        name: "FK_MatchTeams_Matches_MatchID",
                        column: x => x.MatchID,
                        principalSchema: "Infinite",
                        principalTable: "Matches",
                        principalColumn: "MatchID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_MatchID_TeamID",
                schema: "Infinite",
                table: "MatchParticipants",
                columns: new[] { "MatchID", "TeamID" });

            migrationBuilder.AddForeignKey(
                name: "FK_MatchParticipants_MatchTeams_MatchID_TeamID",
                schema: "Infinite",
                table: "MatchParticipants",
                columns: new[] { "MatchID", "TeamID" },
                principalSchema: "Infinite",
                principalTable: "MatchTeams",
                principalColumns: new[] { "MatchID", "TeamID" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchParticipants_MatchTeams_MatchID_TeamID",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropTable(
                name: "MatchTeams",
                schema: "Infinite")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropIndex(
                name: "IX_MatchParticipants_MatchID_TeamID",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "PersonalScore",
                schema: "Infinite",
                table: "MatchParticipants")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "Score",
                schema: "Infinite",
                table: "MatchParticipants")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "Duration",
                schema: "Infinite",
                table: "Matches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchParticipants_Matches_MatchID",
                schema: "Infinite",
                table: "MatchParticipants",
                column: "MatchID",
                principalSchema: "Infinite",
                principalTable: "Matches",
                principalColumn: "MatchID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
