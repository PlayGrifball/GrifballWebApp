using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
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
                table: "MatchedMatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WinnerSide",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "int",
                nullable: true);

            // Every match had Active alone: one with a result on record was reported, one still active is active, and an ended
            // one without a result was ended by vote or canceled, which were stored alike: canceled (#296)
            migrationBuilder.Sql("UPDATE [Matchmaking].[MatchedMatches] SET [Status] = CASE WHEN [MatchID] IS NOT NULL OR [RecordedGameID] IS NOT NULL THEN 2 WHEN [Active] = 1 THEN 1 ELSE 4 END;");

            // The history alike, before Active goes from it too: its rows took Status's default, 0, which is no status. EF may have
            // turned versioning off already for this migration's columns (it turns it back on at the end): only turn it off, and
            // back on, if it is on.
            migrationBuilder.Sql("""
                DECLARE @versioned bit = CASE WHEN OBJECTPROPERTY(OBJECT_ID(N'[Matchmaking].[MatchedMatches]'), N'TableTemporalType') = 2 THEN 1 ELSE 0 END;
                IF @versioned = 1 ALTER TABLE [Matchmaking].[MatchedMatches] SET (SYSTEM_VERSIONING = OFF);
                -- In EXEC, so the batch compiles while the history table is still versioned
                EXEC(N'UPDATE [Matchmaking].[MatchedMatchesHistory] SET [Status] = CASE WHEN [MatchID] IS NOT NULL OR [RecordedGameID] IS NOT NULL THEN 2 WHEN [Active] = 1 THEN 1 ELSE 4 END;');
                IF @versioned = 1 ALTER TABLE [Matchmaking].[MatchedMatches] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Matchmaking].[MatchedMatchesHistory]));
                """);

            migrationBuilder.DropColumn(
                name: "Active",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_ActiveHasNoResult",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "Status <> 1 OR (MatchID IS NULL AND RecordedGameID IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_WinnerSideOnlyWhenVoteDecided",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "(Status = 3 AND WinnerSide IS NOT NULL) OR (Status <> 3 AND WinnerSide IS NULL)");
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
                table: "MatchedMatches",
                type: "bit",
                nullable: false,
                defaultValue: false);
            migrationBuilder.Sql("UPDATE [Matchmaking].[MatchedMatches] SET [Active] = CASE WHEN [Status] = 1 THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "WinnerSide",
                schema: "Matchmaking",
                table: "MatchedMatches");

        }
    }
}
