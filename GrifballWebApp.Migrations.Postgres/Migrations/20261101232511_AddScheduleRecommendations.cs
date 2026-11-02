using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                    ScheduleRecommendationID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "case_insensitive"),
                    Fitness = table.Column<double>(type: "double precision", nullable: false),
                    Generations = table.Column<int>(type: "integer", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "case_insensitive"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    ScheduleRecommendationMatchID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleRecommendationID = table.Column<int>(type: "integer", nullable: false),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: false),
                    ScheduledTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Overlaps = table.Column<bool>(type: "boolean", nullable: false),
                    HomeTeamAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    AwayTeamAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRecommendationMatches", x => x.ScheduleRecommendationMatchID);
                    table.ForeignKey(
                        name: "FK_ScheduleRecommendationMatches_ScheduleRecommendations_Sched~",
                        column: x => x.ScheduleRecommendationID,
                        principalSchema: "Event",
                        principalTable: "ScheduleRecommendations",
                        principalColumn: "ScheduleRecommendationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRecommendationMatches_ScheduleRecommendationID_Seas~",
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
