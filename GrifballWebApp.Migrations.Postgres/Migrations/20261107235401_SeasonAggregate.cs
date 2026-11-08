using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class SeasonAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the seasons the constraint would refuse first: a source link on a season of the app's (none was ever
            // written, as only the import sets one; the link is dropped, the season is otherwise as it was)
            migrationBuilder.Sql("UPDATE \"Event\".\"Seasons\" SET \"HistoricalSourceUrl\" = NULL WHERE \"HistoricalSourceID\" IS NULL AND \"HistoricalSourceUrl\" IS NOT NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Seasons_SourceUrlOnlyImported",
                schema: "Event",
                table: "Seasons",
                sql: "\"HistoricalSourceUrl\" IS NULL OR \"HistoricalSourceID\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A link the constraint settled stays dropped: no season of the app's had one to keep
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Seasons_SourceUrlOnlyImported",
                schema: "Event",
                table: "Seasons");
        }
    }
}
