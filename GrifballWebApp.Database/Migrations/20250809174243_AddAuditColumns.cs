using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "Teams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "Teams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "Seasons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "Seasons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Infinite",
                table: "Matches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Infinite",
                table: "Matches",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "Matches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "Matches",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CreatedByID",
                schema: "Event",
                table: "Teams",
                column: "CreatedByID");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ModifiedByID",
                schema: "Event",
                table: "Teams",
                column: "ModifiedByID");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_CreatedByID",
                schema: "Event",
                table: "Seasons",
                column: "CreatedByID");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_ModifiedByID",
                schema: "Event",
                table: "Seasons",
                column: "ModifiedByID");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches",
                sql: "\n(HomeTeamID IS NULL) OR\n(AwayTeamID IS NULL) OR\n(HomeTeamID != AwayTeamID)\n");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CreatedByID",
                schema: "Infinite",
                table: "Matches",
                column: "CreatedByID");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ModifiedByID",
                schema: "Infinite",
                table: "Matches",
                column: "ModifiedByID");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo",
                sql: "\n(AwayTeamSeedNumber IS NOT NULL AND AwayTeamPreviousMatchBracketInfoID IS NULL) OR\n(AwayTeamPreviousMatchBracketInfoID IS NOT NULL AND AwayTeamSeedNumber IS NULL)\n");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo",
                sql: "\n(HomeTeamSeedNumber IS NOT NULL AND HomeTeamPreviousMatchBracketInfoID IS NULL) OR\n(HomeTeamPreviousMatchBracketInfoID IS NOT NULL AND HomeTeamSeedNumber IS NULL)\n");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Users_CreatedByID",
                schema: "Infinite",
                table: "Matches",
                column: "CreatedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Users_ModifiedByID",
                schema: "Infinite",
                table: "Matches",
                column: "ModifiedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Users_CreatedByID",
                schema: "Event",
                table: "Seasons",
                column: "CreatedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Users_ModifiedByID",
                schema: "Event",
                table: "Seasons",
                column: "ModifiedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Users_CreatedByID",
                schema: "Event",
                table: "Teams",
                column: "CreatedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Users_ModifiedByID",
                schema: "Event",
                table: "Teams",
                column: "ModifiedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Users_CreatedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Users_ModifiedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Users_CreatedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Users_ModifiedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Users_CreatedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Users_ModifiedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_CreatedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_ModifiedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_CreatedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_ModifiedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_CreatedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_ModifiedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "Teams")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "Teams")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "Teams")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "Teams")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "Seasons")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "Seasons")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "Seasons")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "Seasons")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Infinite",
                table: "Matches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Infinite",
                table: "Matches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "Matches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "Matches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches",
                sql: "\r\n(HomeTeamID IS NULL) OR\r\n(AwayTeamID IS NULL) OR\r\n(HomeTeamID != AwayTeamID)\r\n");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo",
                sql: "\r\n(AwayTeamSeedNumber IS NOT NULL AND AwayTeamPreviousMatchBracketInfoID IS NULL) OR\r\n(AwayTeamPreviousMatchBracketInfoID IS NOT NULL AND AwayTeamSeedNumber IS NULL)\r\n");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch",
                schema: "Event",
                table: "MatchBracketInfo",
                sql: "\r\n(HomeTeamSeedNumber IS NOT NULL AND HomeTeamPreviousMatchBracketInfoID IS NULL) OR\r\n(HomeTeamPreviousMatchBracketInfoID IS NOT NULL AND HomeTeamSeedNumber IS NULL)\r\n");
        }
    }
}
