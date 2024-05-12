using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class moreseasonmatchcolumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayTeamResult",
                schema: "Event",
                table: "SeasonMatches",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "AwayTeamScore",
                schema: "Event",
                table: "SeasonMatches",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "HomeTeamResult",
                schema: "Event",
                table: "SeasonMatches",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "HomeTeamScore",
                schema: "Event",
                table: "SeasonMatches",
                type: "int",
                nullable: true)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches",
                sql: "\r\n(HomeTeamID IS NULL) OR\r\n(AwayTeamID IS NULL) OR\r\n(HomeTeamID != AwayTeamID)\r\n");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonMatches_MustBeDifferentTeams",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropColumn(
                name: "AwayTeamResult",
                schema: "Event",
                table: "SeasonMatches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "AwayTeamScore",
                schema: "Event",
                table: "SeasonMatches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "HomeTeamResult",
                schema: "Event",
                table: "SeasonMatches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "HomeTeamScore",
                schema: "Event",
                table: "SeasonMatches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
