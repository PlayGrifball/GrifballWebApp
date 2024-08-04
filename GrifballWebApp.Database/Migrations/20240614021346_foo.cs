using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class foo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvailabilityGridOptions",
                schema: "Event",
                columns: table => new
                {
                    DayOfWeek = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "AvailabilityGridOptionsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "AvailabilityGridOptionsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "AvailabilityGridOptionsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "AvailabilityGridOptionsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityGridOptions", x => new { x.DayOfWeek, x.Time });
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AvailabilityGridOptionsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "TeamAvailability",
                schema: "Event",
                columns: table => new
                {
                    TeamID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamAvailabilityHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamAvailabilityHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamAvailabilityHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamAvailabilityHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamAvailabilityHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamAvailability", x => new { x.TeamID, x.DayOfWeek, x.Time });
                    table.ForeignKey(
                        name: "FK_TeamAvailability_AvailabilityGridOptions_DayOfWeek_Time",
                        columns: x => new { x.DayOfWeek, x.Time },
                        principalSchema: "Event",
                        principalTable: "AvailabilityGridOptions",
                        principalColumns: new[] { "DayOfWeek", "Time" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamAvailability_Teams_TeamID",
                        column: x => x.TeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamAvailabilityHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.InsertData(
                schema: "Event",
                table: "AvailabilityGridOptions",
                columns: new[] { "DayOfWeek", "Time" },
                values: new object[,]
                {
                    { 0, new TimeOnly(19, 0, 0) },
                    { 0, new TimeOnly(19, 30, 0) },
                    { 0, new TimeOnly(20, 0, 0) },
                    { 0, new TimeOnly(20, 30, 0) },
                    { 0, new TimeOnly(21, 0, 0) },
                    { 0, new TimeOnly(21, 30, 0) },
                    { 0, new TimeOnly(22, 0, 0) },
                    { 0, new TimeOnly(22, 30, 0) },
                    { 1, new TimeOnly(19, 0, 0) },
                    { 1, new TimeOnly(19, 30, 0) },
                    { 1, new TimeOnly(20, 0, 0) },
                    { 1, new TimeOnly(20, 30, 0) },
                    { 1, new TimeOnly(21, 0, 0) },
                    { 1, new TimeOnly(21, 30, 0) },
                    { 1, new TimeOnly(22, 0, 0) },
                    { 1, new TimeOnly(22, 30, 0) },
                    { 2, new TimeOnly(19, 0, 0) },
                    { 2, new TimeOnly(19, 30, 0) },
                    { 2, new TimeOnly(20, 0, 0) },
                    { 2, new TimeOnly(20, 30, 0) },
                    { 2, new TimeOnly(21, 0, 0) },
                    { 2, new TimeOnly(21, 30, 0) },
                    { 2, new TimeOnly(22, 0, 0) },
                    { 2, new TimeOnly(22, 30, 0) },
                    { 3, new TimeOnly(19, 0, 0) },
                    { 3, new TimeOnly(19, 30, 0) },
                    { 3, new TimeOnly(20, 0, 0) },
                    { 3, new TimeOnly(20, 30, 0) },
                    { 3, new TimeOnly(21, 0, 0) },
                    { 3, new TimeOnly(21, 30, 0) },
                    { 3, new TimeOnly(22, 0, 0) },
                    { 3, new TimeOnly(22, 30, 0) },
                    { 4, new TimeOnly(19, 0, 0) },
                    { 4, new TimeOnly(19, 30, 0) },
                    { 4, new TimeOnly(20, 0, 0) },
                    { 4, new TimeOnly(20, 30, 0) },
                    { 4, new TimeOnly(21, 0, 0) },
                    { 4, new TimeOnly(21, 30, 0) },
                    { 4, new TimeOnly(22, 0, 0) },
                    { 4, new TimeOnly(22, 30, 0) },
                    { 5, new TimeOnly(19, 0, 0) },
                    { 5, new TimeOnly(19, 30, 0) },
                    { 5, new TimeOnly(20, 0, 0) },
                    { 5, new TimeOnly(20, 30, 0) },
                    { 5, new TimeOnly(21, 0, 0) },
                    { 5, new TimeOnly(21, 30, 0) },
                    { 5, new TimeOnly(22, 0, 0) },
                    { 5, new TimeOnly(22, 30, 0) },
                    { 6, new TimeOnly(19, 0, 0) },
                    { 6, new TimeOnly(19, 30, 0) },
                    { 6, new TimeOnly(20, 0, 0) },
                    { 6, new TimeOnly(20, 30, 0) },
                    { 6, new TimeOnly(21, 0, 0) },
                    { 6, new TimeOnly(21, 30, 0) },
                    { 6, new TimeOnly(22, 0, 0) },
                    { 6, new TimeOnly(22, 30, 0) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamAvailability_DayOfWeek_Time",
                schema: "Event",
                table: "TeamAvailability",
                columns: new[] { "DayOfWeek", "Time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamAvailability",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamAvailabilityHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "AvailabilityGridOptions",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AvailabilityGridOptionsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
