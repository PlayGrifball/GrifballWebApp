using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ReferenceDataAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Other_Regions_NameNotBlank",
                schema: "Other",
                table: "Regions",
                sql: "RegionName <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_Ranks_ThresholdNotNegative",
                schema: "Matchmaking",
                table: "Ranks",
                sql: "MmrThreshold >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Leagues_NameAndSlugNotBlank",
                schema: "Event",
                table: "Leagues",
                sql: "LeagueName <> '' AND Slug <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Other_GameVersions_NameAndSlugNotBlank",
                schema: "Other",
                table: "GameVersions",
                sql: "GameVersionName <> '' AND Slug <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Other_Regions_NameNotBlank",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_Ranks_ThresholdNotNegative",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Leagues_NameAndSlugNotBlank",
                schema: "Event",
                table: "Leagues");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Other_GameVersions_NameAndSlugNotBlank",
                schema: "Other",
                table: "GameVersions");
        }
    }
}
