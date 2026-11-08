using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class StatsSheetsAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the rows the constraint would refuse first: a stint below 1 (the import always counted from 1, so none
            // is expected). Each becomes the player's next stint of that season's phase, after their last one from 1 (one
            // below 1 alone becomes the first), so none clashes with a row already there (the unique index): 0 is the next
            // stint, -1 the one after, and so on. The totals stay.
            migrationBuilder.Sql("UPDATE s SET [StintIndex] = (SELECT COALESCE(MAX(o.[StintIndex]), 0) FROM [Event].[PlayerSeasonStats] o WHERE o.[SeasonID] = s.[SeasonID] AND o.[UserID] = s.[UserID] AND o.[IsPlayoffs] = s.[IsPlayoffs] AND o.[StintIndex] >= 1) + 1 - s.[StintIndex] FROM [Event].[PlayerSeasonStats] s WHERE s.[StintIndex] < 1;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_PlayerSeasonStats_StintFromOne",
                schema: "Event",
                table: "PlayerSeasonStats",
                sql: "StintIndex >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A stint the constraint settled keeps its new number: it is the player's next stint
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_PlayerSeasonStats_StintFromOne",
                schema: "Event",
                table: "PlayerSeasonStats");
        }
    }
}
