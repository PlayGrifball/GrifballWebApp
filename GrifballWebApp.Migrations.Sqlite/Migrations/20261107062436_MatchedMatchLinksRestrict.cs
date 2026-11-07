using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MatchedMatchLinksRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Update";
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Delete";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "RecordedGameID",
                principalSchema: "Recorded",
                principalTable: "Games",
                principalColumn: "RecordedGameID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "MatchmakingSeasonID",
                principalSchema: "Matchmaking",
                principalTable: "MatchmakingSeasons",
                principalColumn: "MatchmakingSeasonID",
                onDelete: ReferentialAction.Restrict);

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

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "RecordedGameID",
                principalSchema: "Recorded",
                principalTable: "Games",
                principalColumn: "RecordedGameID");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "MatchmakingSeasonID",
                principalSchema: "Matchmaking",
                principalTable: "MatchmakingSeasons",
                principalColumn: "MatchmakingSeasonID");

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
