using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MatchedMatchStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Update";
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Delete";
                """);

            migrationBuilder.RenameColumn(
                name: "Active",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                newName: "WinnerSide");

            migrationBuilder.RenameColumn(
                name: "Active",
                schema: "Matchmaking",
                table: "MatchedMatches",
                newName: "Status");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerSide",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "INTEGER",
                nullable: true);

            // Every match had Active alone: one with a result on record was reported, one still active is active, and an ended
            // one without a result was ended by vote or canceled, which were stored alike: canceled (#296)
            // EF renamed Active to Status (its 1 is Active) and, in the history, to WinnerSide (whose old values mean nothing there)
            migrationBuilder.Sql("UPDATE \"MatchedMatches\" SET \"Status\" = CASE WHEN \"MatchID\" IS NOT NULL OR \"RecordedGameID\" IS NOT NULL THEN 2 WHEN \"Status\" = 1 THEN 1 ELSE 4 END;");

            // The history alike (its old Active is in WinnerSide now), as on SQL Server and Postgres
            migrationBuilder.Sql("UPDATE \"MatchedMatchesHistory\" SET \"Status\" = CASE WHEN \"MatchID\" IS NOT NULL OR \"RecordedGameID\" IS NOT NULL THEN 2 WHEN \"WinnerSide\" = 1 THEN 1 ELSE 4 END, \"WinnerSide\" = NULL;");

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

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedMatches_History_Update" AFTER UPDATE ON "MatchedMatches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedMatchesHistory" ("AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedGameID", "StartedAt", "Status", "ThreadID", "VoteMessageID", "WinnerSide", "PeriodEnd") VALUES (OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedGameID", OLD."StartedAt", OLD."Status", OLD."ThreadID", OLD."VoteMessageID", OLD."WinnerSide", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedMatches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedMatches_History_Delete" AFTER DELETE ON "MatchedMatches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedMatchesHistory" ("AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedGameID", "StartedAt", "Status", "ThreadID", "VoteMessageID", "WinnerSide", "PeriodEnd") VALUES (OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedGameID", OLD."StartedAt", OLD."Status", OLD."ThreadID", OLD."VoteMessageID", OLD."WinnerSide", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Update";
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_ActiveHasNoResult",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_WinnerSideOnlyWhenVoteDecided",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory");

            migrationBuilder.DropColumn(
                name: "WinnerSide",
                schema: "Matchmaking",
                table: "MatchedMatches");

            // Status becomes Active again (1 is active), and the history's WinnerSide its Active, unknown there
            migrationBuilder.Sql("UPDATE \"MatchedMatches\" SET \"Status\" = CASE WHEN \"Status\" = 1 THEN 1 ELSE 0 END;");

            migrationBuilder.Sql("UPDATE \"MatchedMatchesHistory\" SET \"WinnerSide\" = NULL;");

            migrationBuilder.RenameColumn(
                name: "WinnerSide",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                newName: "Active");

            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "Matchmaking",
                table: "MatchedMatches",
                newName: "Active");

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedMatches_History_Update" AFTER UPDATE ON "MatchedMatches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedMatchesHistory" ("Active", "AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedGameID", "StartedAt", "ThreadID", "VoteMessageID", "PeriodEnd") VALUES (OLD."Active", OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedGameID", OLD."StartedAt", OLD."ThreadID", OLD."VoteMessageID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedMatches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedMatches_History_Delete" AFTER DELETE ON "MatchedMatches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedMatchesHistory" ("Active", "AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedGameID", "StartedAt", "ThreadID", "VoteMessageID", "PeriodEnd") VALUES (OLD."Active", OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedGameID", OLD."StartedAt", OLD."ThreadID", OLD."VoteMessageID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
