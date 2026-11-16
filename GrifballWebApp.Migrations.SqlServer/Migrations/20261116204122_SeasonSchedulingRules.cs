using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SeasonSchedulingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeasonSchedulingRules",
                schema: "Event",
                columns: table => new
                {
                    SeasonID = table.Column<int>(type: "int", nullable: false),
                    MinPlayersAvailable = table.Column<int>(type: "int", nullable: false),
                    MaxMatchesPerTeamPerNight = table.Column<int>(type: "int", nullable: false),
                    MaxMatchesPerTeamPerWeek = table.Column<int>(type: "int", nullable: false),
                    MaxConcurrentMatches = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonSchedulingRules", x => x.SeasonID);
                    table.ForeignKey(
                        name: "FK_SeasonSchedulingRules_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeasonSchedulingRules",
                schema: "Event");
        }
    }
}
