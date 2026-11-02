using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduleRecommendations",
                schema: "Event",
                columns: table => new
                {
                    ScheduleRecommendationID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    Fitness = table.Column<double>(type: "REAL", nullable: false),
                    Generations = table.Column<int>(type: "INTEGER", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AppliedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRecommendations", x => x.ScheduleRecommendationID);
                    table.ForeignKey(
                        name: "FK_ScheduleRecommendations_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRecommendationMatches",
                schema: "Event",
                columns: table => new
                {
                    ScheduleRecommendationMatchID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduleRecommendationID = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduledTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Overlaps = table.Column<bool>(type: "INTEGER", nullable: false),
                    HomeTeamAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    AwayTeamAvailable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRecommendationMatches", x => x.ScheduleRecommendationMatchID);
                    table.ForeignKey(
                        name: "FK_ScheduleRecommendationMatches_ScheduleRecommendations_ScheduleRecommendationID",
                        column: x => x.ScheduleRecommendationID,
                        principalSchema: "Event",
                        principalTable: "ScheduleRecommendations",
                        principalColumn: "ScheduleRecommendationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRecommendationMatches_ScheduleRecommendationID_SeasonMatchID",
                schema: "Event",
                table: "ScheduleRecommendationMatches",
                columns: new[] { "ScheduleRecommendationID", "SeasonMatchID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRecommendations_SeasonID_CreatedAt",
                schema: "Event",
                table: "ScheduleRecommendations",
                columns: new[] { "SeasonID", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleRecommendationMatches",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "ScheduleRecommendations",
                schema: "Event");
        }
    }
}
