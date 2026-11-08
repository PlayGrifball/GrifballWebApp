using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class TeamAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the teams the constraint would refuse first: half a record of the spreadsheet's (the import always
            // wrote both halves, so none is expected). The blank half becomes 0, as the standings already counted it
            // (TeamRecord.Of): every team keeps the record it showed.
            migrationBuilder.Sql("UPDATE \"Event\".\"Teams\" SET \"RecordedWins\" = COALESCE(\"RecordedWins\", 0), \"RecordedLosses\" = COALESCE(\"RecordedLosses\", 0) WHERE (\"RecordedWins\" IS NULL AND \"RecordedLosses\" IS NOT NULL) OR (\"RecordedWins\" IS NOT NULL AND \"RecordedLosses\" IS NULL);");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Teams_RecordBothOrNeither",
                schema: "Event",
                table: "Teams",
                sql: "(\"RecordedWins\" IS NULL AND \"RecordedLosses\" IS NULL) OR (\"RecordedWins\" IS NOT NULL AND \"RecordedLosses\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A half record the constraint settled keeps its 0: it is the record the team showed
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Teams_RecordBothOrNeither",
                schema: "Event",
                table: "Teams");
        }
    }
}
