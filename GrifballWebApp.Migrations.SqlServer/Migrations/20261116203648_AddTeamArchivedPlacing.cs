using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamArchivedPlacing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArchivedFinish",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchivedRank",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Teams_ArchivedPlacingFromFirst",
                schema: "Event",
                table: "Teams",
                sql: "(ArchivedRank IS NULL OR ArchivedRank >= 1) AND (ArchivedFinish IS NULL OR ArchivedFinish >= 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Teams_ArchivedPlacingFromFirst",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ArchivedFinish",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ArchivedRank",
                schema: "Event",
                table: "Teams");
        }
    }
}
