using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class SeasonSignupAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Approved",
                schema: "Event",
                table: "SeasonSignupsHistory");

            migrationBuilder.DropColumn(
                name: "Approved",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"SeasonSignups\"');");

            // Settle the rows the constraints would refuse first: a team name trimmed (a blank one goes), and no help
            // drafting for a player who won't captain
            migrationBuilder.Sql("""
                UPDATE "Event"."SeasonSignups" SET "TeamName" = NULLIF(LTRIM(RTRIM("TeamName")), '')
                WHERE "TeamName" = '' OR "TeamName" <> LTRIM(RTRIM("TeamName"));
                UPDATE "Event"."SeasonSignups" SET "RequiresAssistanceDrafting" = false WHERE NOT "WillCaptain" AND "RequiresAssistanceDrafting";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonSignups_AssistanceOnlyForCaptains",
                schema: "Event",
                table: "SeasonSignups",
                sql: "CAST(\"RequiresAssistanceDrafting\" AS INT) = 0 OR CAST(\"WillCaptain\" AS INT) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonSignups_TeamNameTrimmed",
                schema: "Event",
                table: "SeasonSignups",
                sql: "\"TeamName\" IS NULL OR (\"TeamName\" <> '' AND \"TeamName\" = LTRIM(RTRIM(\"TeamName\")))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonSignups_AssistanceOnlyForCaptains",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonSignups_TeamNameTrimmed",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                schema: "Event",
                table: "SeasonSignupsHistory",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                schema: "Event",
                table: "SeasonSignups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"SeasonSignups\"');");
        }
    }
}
