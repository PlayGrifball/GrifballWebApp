using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MatchedMatchVoteMessageNeedsThread : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settled before the check: a vote message with no thread to be posted in is no message (nothing was ever voted on it)
            migrationBuilder.Sql("UPDATE \"MatchedMatches\" SET \"VoteMessageID\" = NULL WHERE \"VoteMessageID\" IS NOT NULL AND \"ThreadID\" IS NULL;");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Update";
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_VoteMessageNeedsThread",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "VoteMessageID IS NULL OR ThreadID IS NOT NULL");

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
                name: "CK_Matchmaking_MatchedMatches_VoteMessageNeedsThread",
                schema: "Matchmaking",
                table: "MatchedMatches");

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
    }
}
