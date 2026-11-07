using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class MatchedMatchStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerSide",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WinnerSide",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "integer",
                nullable: true);

            // Every match had Active alone: one with a result on record was reported, one still active is active, and an ended
            // one without a result was ended by vote or canceled, which were stored alike: canceled (#296)
            migrationBuilder.Sql("UPDATE \"Matchmaking\".\"MatchedMatches\" SET \"Status\" = CASE WHEN \"MatchID\" IS NOT NULL OR \"RecordedGameID\" IS NOT NULL THEN 2 WHEN \"Active\" THEN 1 ELSE 4 END;");

            // The history alike, before Active goes from it too, the rows the update above just wrote included: their Status is
            // null (the versioning function's columns are refreshed only below)
            migrationBuilder.Sql("UPDATE \"Matchmaking\".\"MatchedMatchesHistory\" SET \"Status\" = CASE WHEN \"MatchID\" IS NOT NULL OR \"RecordedGameID\" IS NOT NULL THEN 2 WHEN \"Active\" THEN 1 ELSE 4 END;");

            migrationBuilder.DropColumn(
                name: "Active",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory");

            migrationBuilder.DropColumn(
                name: "Active",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Matchmaking\".\"MatchedMatches\"');");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_ActiveHasNoResult",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "\"Status\" <> 1 OR (\"MatchID\" IS NULL AND \"RecordedGameID\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_WinnerSideOnlyWhenVoteDecided",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "(\"Status\" = 3 AND \"WinnerSide\" IS NOT NULL) OR (\"Status\" <> 3 AND \"WinnerSide\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_ActiveHasNoResult",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_WinnerSideOnlyWhenVoteDecided",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE \"Matchmaking\".\"MatchedMatches\" SET \"Active\" = \"Status\" = 1;");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory");

            migrationBuilder.DropColumn(
                name: "WinnerSide",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "WinnerSide",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Matchmaking\".\"MatchedMatches\"');");
        }
    }
}
