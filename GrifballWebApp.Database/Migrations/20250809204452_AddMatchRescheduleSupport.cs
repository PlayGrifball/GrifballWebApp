using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchRescheduleSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchReschedules",
                schema: "Season",
                columns: table => new
                {
                    MatchRescheduleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonMatchID = table.Column<int>(type: "int", nullable: false),
                    OriginalScheduledTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewScheduledTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedByUserID = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserID = table.Column<int>(type: "int", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscordThreadID = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    CommissionerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchReschedules", x => x.MatchRescheduleID);
                    table.ForeignKey(
                        name: "FK_MatchReschedules_Matches_SeasonMatchID",
                        column: x => x.SeasonMatchID,
                        principalSchema: "Season",
                        principalTable: "Matches",
                        principalColumn: "SeasonMatchID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchReschedules_Users_RequestedByUserID",
                        column: x => x.RequestedByUserID,
                        principalSchema: "User",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchReschedules_Users_ApprovedByUserID",
                        column: x => x.ApprovedByUserID,
                        principalSchema: "User",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchReschedulesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Season")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_SeasonMatchID",
                schema: "Season",
                table: "MatchReschedules",
                column: "SeasonMatchID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_Status",
                schema: "Season",
                table: "MatchReschedules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_RequestedAt",
                schema: "Season",
                table: "MatchReschedules",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_DiscordThreadID",
                schema: "Season",
                table: "MatchReschedules",
                column: "DiscordThreadID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_RequestedByUserID",
                schema: "Season",
                table: "MatchReschedules",
                column: "RequestedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_ApprovedByUserID",
                schema: "Season",
                table: "MatchReschedules",
                column: "ApprovedByUserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchReschedules",
                schema: "Season")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchReschedulesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Season")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}