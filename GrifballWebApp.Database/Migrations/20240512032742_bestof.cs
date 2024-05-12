using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class bestof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchLinks_SeasonMatches_SeasonMatchID",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchLinks",
                schema: "Event",
                table: "MatchLinks")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropIndex(
                name: "IX_MatchLinks_SeasonMatchID",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "MatchLinks")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchLinks")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AlterTable(
                name: "MatchLinks",
                schema: "Event")
                .OldAnnotation("SqlServer:IsTemporal", true)
                .OldAnnotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .OldAnnotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .OldAnnotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .OldAnnotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "BestOf",
                schema: "Event",
                table: "SeasonMatches",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AlterColumn<int>(
                name: "SeasonMatchID",
                schema: "Event",
                table: "MatchLinks",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:IsTemporal", true)
                .OldAnnotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .OldAnnotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .OldAnnotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .OldAnnotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AlterColumn<Guid>(
                name: "MatchID",
                schema: "Event",
                table: "MatchLinks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier")
                .OldAnnotation("SqlServer:IsTemporal", true)
                .OldAnnotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .OldAnnotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .OldAnnotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .OldAnnotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<int>(
                name: "MatchLinkID",
                schema: "Event",
                table: "MatchLinks",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "MatchNumber",
                schema: "Event",
                table: "MatchLinks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchLinks",
                schema: "Event",
                table: "MatchLinks",
                column: "MatchLinkID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinks_SeasonMatchID_MatchNumber",
                schema: "Event",
                table: "MatchLinks",
                columns: new[] { "SeasonMatchID", "MatchNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchLinks_SeasonMatches_SeasonMatchID",
                schema: "Event",
                table: "MatchLinks",
                column: "SeasonMatchID",
                principalSchema: "Event",
                principalTable: "SeasonMatches",
                principalColumn: "SeasonMatchID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchLinks_SeasonMatches_SeasonMatchID",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchLinks",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropIndex(
                name: "IX_MatchLinks_SeasonMatchID_MatchNumber",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "BestOf",
                schema: "Event",
                table: "SeasonMatches")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "MatchLinkID",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "MatchNumber",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.AlterTable(
                name: "MatchLinks",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AlterColumn<int>(
                name: "SeasonMatchID",
                schema: "Event",
                table: "MatchLinks",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AlterColumn<Guid>(
                name: "MatchID",
                schema: "Event",
                table: "MatchLinks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "MatchLinks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchLinks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified))
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchLinks",
                schema: "Event",
                table: "MatchLinks",
                columns: new[] { "MatchID", "SeasonMatchID" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinks_SeasonMatchID",
                schema: "Event",
                table: "MatchLinks",
                column: "SeasonMatchID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchLinks_SeasonMatches_SeasonMatchID",
                schema: "Event",
                table: "MatchLinks",
                column: "SeasonMatchID",
                principalSchema: "Event",
                principalTable: "SeasonMatches",
                principalColumn: "SeasonMatchID");
        }
    }
}
