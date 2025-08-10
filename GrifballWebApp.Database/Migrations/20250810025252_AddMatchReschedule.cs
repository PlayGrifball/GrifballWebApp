using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchReschedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchReschedules",
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
                    CreatedByID = table.Column<int>(type: "int", nullable: true),
                    ModifiedByID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchReschedules", x => x.MatchRescheduleID);
                    table.ForeignKey(
                        name: "FK_MatchReschedules_SeasonMatches_SeasonMatchID",
                        column: x => x.SeasonMatchID,
                        principalSchema: "Event",
                        principalTable: "SeasonMatches",
                        principalColumn: "SeasonMatchID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchReschedules_Users_ApprovedByUserID",
                        column: x => x.ApprovedByUserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchReschedules_Users_RequestedByUserID",
                        column: x => x.RequestedByUserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_ApprovedByUserID",
                table: "MatchReschedules",
                column: "ApprovedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_DiscordThreadID",
                table: "MatchReschedules",
                column: "DiscordThreadID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_RequestedAt",
                table: "MatchReschedules",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_RequestedByUserID",
                table: "MatchReschedules",
                column: "RequestedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_SeasonMatchID",
                table: "MatchReschedules",
                column: "SeasonMatchID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_Status",
                table: "MatchReschedules",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchReschedules");
        }
    }
}
