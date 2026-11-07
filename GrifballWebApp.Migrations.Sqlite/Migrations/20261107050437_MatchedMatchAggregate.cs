using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MatchedMatchAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedKickVotes_History_Update";
                DROP TRIGGER IF EXISTS "MatchedKickVotes_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Update";
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Delete";
                """);

            migrationBuilder.DropIndex(
                name: "IX_MatchedKickVotesHistory_MatchId_VoterMatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedKickVotesHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchedKickVotes",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchedKickVotes",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                columns: new[] { "MatchId", "VoterMatchedPlayerId", "KickMatchedPlayerId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_OneKindOfResult",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "MatchID IS NULL OR RecordedGameID IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_ReportedHasResult",
                schema: "Matchmaking",
                table: "MatchedMatches",
                sql: "Status <> 2 OR MatchID IS NOT NULL OR RecordedGameID IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedKickVotesHistory_MatchId_VoterMatchedPlayerId_KickMatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedKickVotesHistory",
                columns: new[] { "MatchId", "VoterMatchedPlayerId", "KickMatchedPlayerId", "PeriodEnd" });

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedKickVotes_History_Update" AFTER UPDATE ON "MatchedKickVotes" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedKickVotesHistory" ("CreatedAt", "CreatedByID", "KickMatchedPlayerId", "MatchId", "ModifiedAt", "ModifiedByID", "PeriodStart", "VoterMatchedPlayerId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."KickMatchedPlayerId", OLD."MatchId", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."VoterMatchedPlayerId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedKickVotes" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedKickVotes_History_Delete" AFTER DELETE ON "MatchedKickVotes" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedKickVotesHistory" ("CreatedAt", "CreatedByID", "KickMatchedPlayerId", "MatchId", "ModifiedAt", "ModifiedByID", "PeriodStart", "VoterMatchedPlayerId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."KickMatchedPlayerId", OLD."MatchId", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."VoterMatchedPlayerId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

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
                DROP TRIGGER IF EXISTS "MatchedKickVotes_History_Update";
                DROP TRIGGER IF EXISTS "MatchedKickVotes_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Update";
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_OneKindOfResult",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchedMatches_ReportedHasResult",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropIndex(
                name: "IX_MatchedKickVotesHistory_MatchId_VoterMatchedPlayerId_KickMatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedKickVotesHistory");

            // The old key holds one kick vote a voter a match: each voter keeps the one against the player with the lowest key
            migrationBuilder.Sql("DELETE FROM \"MatchedKickVotes\" AS k WHERE EXISTS (SELECT 1 FROM \"MatchedKickVotes\" AS o WHERE o.\"MatchId\" = k.\"MatchId\" AND o.\"VoterMatchedPlayerId\" = k.\"VoterMatchedPlayerId\" AND o.\"KickMatchedPlayerId\" < k.\"KickMatchedPlayerId\");");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MatchedKickVotes",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MatchedKickVotes",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                columns: new[] { "MatchId", "VoterMatchedPlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedKickVotesHistory_MatchId_VoterMatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedKickVotesHistory",
                columns: new[] { "MatchId", "VoterMatchedPlayerId", "PeriodEnd" });

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedKickVotes_History_Update" AFTER UPDATE ON "MatchedKickVotes" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedKickVotesHistory" ("CreatedAt", "CreatedByID", "KickMatchedPlayerId", "MatchId", "ModifiedAt", "ModifiedByID", "PeriodStart", "VoterMatchedPlayerId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."KickMatchedPlayerId", OLD."MatchId", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."VoterMatchedPlayerId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedKickVotes" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedKickVotes_History_Delete" AFTER DELETE ON "MatchedKickVotes" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedKickVotesHistory" ("CreatedAt", "CreatedByID", "KickMatchedPlayerId", "MatchId", "ModifiedAt", "ModifiedByID", "PeriodStart", "VoterMatchedPlayerId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."KickMatchedPlayerId", OLD."MatchId", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."VoterMatchedPlayerId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

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
