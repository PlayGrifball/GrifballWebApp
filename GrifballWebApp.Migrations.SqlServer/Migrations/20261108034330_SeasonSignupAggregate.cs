using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
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
                table: "SeasonSignups");

            // Settle the rows the constraints would refuse first: a team name trimmed (a blank one, which SQL Server
            // compares equal to '', goes; DATALENGTH counts the trailing spaces that = ignores), and no help drafting
            // for a player who won't captain
            migrationBuilder.Sql("""
                UPDATE [Event].[SeasonSignups] SET [TeamName] = NULLIF(LTRIM(RTRIM([TeamName])), N'')
                WHERE [TeamName] = N'' OR DATALENGTH([TeamName]) <> DATALENGTH(LTRIM(RTRIM([TeamName])));
                UPDATE [Event].[SeasonSignups] SET [RequiresAssistanceDrafting] = 0 WHERE [WillCaptain] = 0 AND [RequiresAssistanceDrafting] = 1;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonSignups_AssistanceOnlyForCaptains",
                schema: "Event",
                table: "SeasonSignups",
                sql: "CAST(RequiresAssistanceDrafting AS INT) = 0 OR CAST(WillCaptain AS INT) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonSignups_TeamNameTrimmed",
                schema: "Event",
                table: "SeasonSignups",
                sql: "TeamName IS NULL OR (TeamName <> '' AND TeamName = LTRIM(RTRIM(TeamName)))");
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
                table: "SeasonSignups",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
