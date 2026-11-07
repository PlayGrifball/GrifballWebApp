using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SeasonDatesInOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Imported seasons first, as ScheduleArchived settles them: the old import kept a start after the import time and
            // cut the end back to it, so such a season ended at its import. All its dates go back to that end, so it stays
            // ended (rather than its end moving forward to a future start).
            migrationBuilder.Sql("UPDATE [Event].[Seasons] SET [SignupsOpen] = [SeasonEnd], [SignupsClose] = [SeasonEnd], [DraftStart] = [SeasonEnd], [SeasonStart] = [SeasonEnd], [PublicAt] = [SeasonEnd] WHERE [HistoricalSourceID] IS NOT NULL AND [SeasonEnd] < [SeasonStart];");

            // Settle the seasons the constraint would refuse (the editor took dates in any order): signups that closed before
            // they opened close as they open, a season that ended before it started ends as it starts, and a draft after the
            // start is at the start. Each row is otherwise as it was, and its history keeps the old dates.
            migrationBuilder.Sql("UPDATE [Event].[Seasons] SET [SignupsClose] = [SignupsOpen] WHERE [SignupsClose] < [SignupsOpen];");
            migrationBuilder.Sql("UPDATE [Event].[Seasons] SET [SeasonEnd] = [SeasonStart] WHERE [SeasonEnd] < [SeasonStart];");
            migrationBuilder.Sql("UPDATE [Event].[Seasons] SET [DraftStart] = [SeasonStart] WHERE [DraftStart] > [SeasonStart];");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Seasons_DatesInOrder",
                schema: "Event",
                table: "Seasons",
                sql: "SignupsOpen <= SignupsClose AND DraftStart <= SeasonStart AND SeasonStart <= SeasonEnd");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled dates stay settled: they were in no order a season can have
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Seasons_DatesInOrder",
                schema: "Event",
                table: "Seasons");
        }
    }
}
