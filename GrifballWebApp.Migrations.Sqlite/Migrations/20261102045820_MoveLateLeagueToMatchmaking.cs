using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MoveLateLeagueToMatchmaking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Late League is matchmaking now, not seasons. The Late League seasons an import made (format 3, the Late League
            // league) go, with their teams, rosters, stats, series and claims; their recorded games stay, in no season, and their
            // VODs lose the link: the next import makes them matchmaking seasons and matches (found again by their keys). One
            // made in the app keeps everything it has, as an AGLA league season (the league and format are gone), for a
            // sysadmin to put right.
            migrationBuilder.Sql("""
                UPDATE "Games" SET "SeasonID" = NULL WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Seasons" WHERE "HistoricalSourceID" IS NOT NULL AND ("Format" = 3 OR "LeagueID" = 2));
                UPDATE "GameTeams" SET "TeamID" = NULL WHERE "TeamID" IN (SELECT "TeamID" FROM "Teams" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Seasons" WHERE "HistoricalSourceID" IS NOT NULL AND ("Format" = 3 OR "LeagueID" = 2)));
                UPDATE "GamertagClaims" SET "TeamID" = NULL WHERE "TeamID" IN (SELECT "TeamID" FROM "Teams" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Seasons" WHERE "HistoricalSourceID" IS NOT NULL AND ("Format" = 3 OR "LeagueID" = 2)));
                UPDATE "PlayerSeasonStats" SET "TeamID" = NULL WHERE "TeamID" IN (SELECT "TeamID" FROM "Teams" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Seasons" WHERE "HistoricalSourceID" IS NOT NULL AND ("Format" = 3 OR "LeagueID" = 2)));
                UPDATE "Vods" SET "SeasonID" = NULL WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Seasons" WHERE "HistoricalSourceID" IS NOT NULL AND ("Format" = 3 OR "LeagueID" = 2));
                DELETE FROM "PlayerChampionships" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Seasons" WHERE "HistoricalSourceID" IS NOT NULL AND ("Format" = 3 OR "LeagueID" = 2));
                DELETE FROM "SeasonMatches" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Seasons" WHERE "HistoricalSourceID" IS NOT NULL AND ("Format" = 3 OR "LeagueID" = 2));
                UPDATE "Teams" SET "CaptainID" = NULL WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Seasons" WHERE "HistoricalSourceID" IS NOT NULL AND ("Format" = 3 OR "LeagueID" = 2));
                DELETE FROM "Seasons" WHERE "HistoricalSourceID" IS NOT NULL AND ("Format" = 3 OR "LeagueID" = 2);
                UPDATE "Seasons" SET "SeasonName" = substr("SeasonName", 1, 85) || ' (Late League)' WHERE ("Format" = 3 OR "LeagueID" = 2) AND EXISTS (SELECT 1 FROM "Seasons" AS "o" WHERE "o"."LeagueID" = 1 AND "o"."SeasonName" = "Seasons"."SeasonName");
                UPDATE "Seasons" SET "LeagueID" = 1, "Format" = 1 WHERE ("Format" = 3 OR "LeagueID" = 2);
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Leagues_History_Update";
                DROP TRIGGER IF EXISTS "Leagues_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Update";
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Delete";
                """);

            migrationBuilder.DeleteData(
                schema: "Event",
                table: "Leagues",
                keyColumn: "LeagueID",
                keyValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchmakingSeasons",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchmakingSeasonID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HistoricalSourceID = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    HistoricalSourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchmakingSeasons", x => x.MatchmakingSeasonID);
                    table.ForeignKey(
                        name: "FK_MatchmakingSeasons_GameVersions_GameVersionID",
                        column: x => x.GameVersionID,
                        principalSchema: "Other",
                        principalTable: "GameVersions",
                        principalColumn: "GameVesionID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchmakingSeasonsHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: true),
                    HistoricalSourceID = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    HistoricalSourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE"),
                    MatchmakingSeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PlayerStatsHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    Assists = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageLifeSeconds = table.Column<double>(type: "decimal(6, 2)", nullable: true),
                    BallKills = table.Column<int>(type: "INTEGER", nullable: true),
                    Betrayals = table.Column<int>(type: "INTEGER", nullable: true),
                    CarrierKills = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    DamageDealt = table.Column<int>(type: "INTEGER", nullable: true),
                    DamageTaken = table.Column<int>(type: "INTEGER", nullable: true),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: true),
                    ExtendedMetricsJson = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Exterminations = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: true),
                    Gamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    GamesPlayed = table.Column<int>(type: "INTEGER", nullable: true),
                    Goals = table.Column<int>(type: "INTEGER", nullable: true),
                    HammerKills = table.Column<int>(type: "INTEGER", nullable: true),
                    KdRatio = table.Column<double>(type: "decimal(6, 3)", nullable: true),
                    KdSpread = table.Column<int>(type: "INTEGER", nullable: true),
                    Kills = table.Column<int>(type: "INTEGER", nullable: true),
                    Losses = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchmakingPlayerStatID = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchmakingSeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    MultiKills = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Punches = table.Column<int>(type: "INTEGER", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    Sprees = table.Column<int>(type: "INTEGER", nullable: true),
                    SwordKills = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalSecondsPlayed = table.Column<int>(type: "INTEGER", nullable: true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true),
                    WinPercent = table.Column<double>(type: "decimal(6, 3)", nullable: true),
                    Wins = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PlayerStats",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchmakingPlayerStatID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchmakingSeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: false),
                    Gamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    GamesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false),
                    WinPercent = table.Column<double>(type: "decimal(6, 3)", nullable: true),
                    Goals = table.Column<int>(type: "INTEGER", nullable: false),
                    Kills = table.Column<int>(type: "INTEGER", nullable: false),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    KdRatio = table.Column<double>(type: "decimal(6, 3)", nullable: true),
                    KdSpread = table.Column<int>(type: "INTEGER", nullable: true),
                    CarrierKills = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageLifeSeconds = table.Column<double>(type: "decimal(6, 2)", nullable: true),
                    TotalSecondsPlayed = table.Column<int>(type: "INTEGER", nullable: true),
                    Sprees = table.Column<int>(type: "INTEGER", nullable: false),
                    MultiKills = table.Column<int>(type: "INTEGER", nullable: false),
                    Exterminations = table.Column<int>(type: "INTEGER", nullable: false),
                    Betrayals = table.Column<int>(type: "INTEGER", nullable: false),
                    HammerKills = table.Column<int>(type: "INTEGER", nullable: true),
                    SwordKills = table.Column<int>(type: "INTEGER", nullable: true),
                    BallKills = table.Column<int>(type: "INTEGER", nullable: true),
                    Punches = table.Column<int>(type: "INTEGER", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    DamageDealt = table.Column<int>(type: "INTEGER", nullable: true),
                    DamageTaken = table.Column<int>(type: "INTEGER", nullable: true),
                    ExtendedMetricsJson = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStats", x => x.MatchmakingPlayerStatID);
                    table.ForeignKey(
                        name: "FK_PlayerStats_GameVersions_GameVersionID",
                        column: x => x.GameVersionID,
                        principalSchema: "Other",
                        principalTable: "GameVersions",
                        principalColumn: "GameVesionID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerStats_MatchmakingSeasons_MatchmakingSeasonID",
                        column: x => x.MatchmakingSeasonID,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchmakingSeasons",
                        principalColumn: "MatchmakingSeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerStats_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatchesHistory_MatchmakingSeasonID_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                columns: new[] { "MatchmakingSeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatchesHistory_RecordedGameID_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                columns: new[] { "RecordedGameID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatches_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "MatchmakingSeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatches_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "RecordedGameID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchmakingSeasons_GameVersionID",
                schema: "Matchmaking",
                table: "MatchmakingSeasons",
                column: "GameVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchmakingSeasons_HistoricalSourceID",
                schema: "Matchmaking",
                table: "MatchmakingSeasons",
                column: "HistoricalSourceID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchmakingSeasonsHistory_GameVersionID_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchmakingSeasonsHistory",
                columns: new[] { "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchmakingSeasonsHistory_MatchmakingSeasonID_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchmakingSeasonsHistory",
                columns: new[] { "MatchmakingSeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStats_GameVersionID",
                schema: "Matchmaking",
                table: "PlayerStats",
                column: "GameVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStats_MatchmakingSeasonID_UserID",
                schema: "Matchmaking",
                table: "PlayerStats",
                columns: new[] { "MatchmakingSeasonID", "UserID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStats_UserID",
                schema: "Matchmaking",
                table: "PlayerStats",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatsHistory_GameVersionID_PeriodEnd",
                schema: "Matchmaking",
                table: "PlayerStatsHistory",
                columns: new[] { "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatsHistory_MatchmakingPlayerStatID_PeriodEnd",
                schema: "Matchmaking",
                table: "PlayerStatsHistory",
                columns: new[] { "MatchmakingPlayerStatID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatsHistory_MatchmakingSeasonID_PeriodEnd",
                schema: "Matchmaking",
                table: "PlayerStatsHistory",
                columns: new[] { "MatchmakingSeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerStatsHistory_UserID_PeriodEnd",
                schema: "Matchmaking",
                table: "PlayerStatsHistory",
                columns: new[] { "UserID", "PeriodEnd" });

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
                CREATE TRIGGER "Leagues_History_Update" AFTER UPDATE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Leagues" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Leagues_History_Delete" AFTER DELETE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedMatches_History_Update" AFTER UPDATE ON "MatchedMatches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedMatchesHistory" ("Active", "AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedGameID", "StartedAt", "ThreadID", "VoteMessageID", "PeriodEnd") VALUES (OLD."Active", OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedGameID", OLD."StartedAt", OLD."ThreadID", OLD."VoteMessageID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedMatches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedMatches_History_Delete" AFTER DELETE ON "MatchedMatches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedMatchesHistory" ("Active", "AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedGameID", "StartedAt", "ThreadID", "VoteMessageID", "PeriodEnd") VALUES (OLD."Active", OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedGameID", OLD."StartedAt", OLD."ThreadID", OLD."VoteMessageID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchmakingSeasons_History_Update" AFTER UPDATE ON "MatchmakingSeasons" FOR EACH ROW BEGIN
                    INSERT INTO "MatchmakingSeasonsHistory" ("CreatedAt", "CreatedByID", "EndsAt", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "StartsAt", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."EndsAt", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", OLD."StartsAt", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchmakingSeasons" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchmakingSeasons_History_Delete" AFTER DELETE ON "MatchmakingSeasons" FOR EACH ROW BEGIN
                    INSERT INTO "MatchmakingSeasonsHistory" ("CreatedAt", "CreatedByID", "EndsAt", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "StartsAt", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."EndsAt", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", OLD."StartsAt", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "PlayerStats_History_Update" AFTER UPDATE ON "PlayerStats" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerStatsHistory" ("Assists", "AverageLifeSeconds", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "ExtendedMetricsJson", "Exterminations", "GameVersionID", "Gamertag", "GamesPlayed", "Goals", "HammerKills", "KdRatio", "KdSpread", "Kills", "Losses", "MatchmakingPlayerStatID", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "MultiKills", "PeriodStart", "Punches", "Score", "Sprees", "SwordKills", "TotalSecondsPlayed", "UserID", "WinPercent", "Wins", "PeriodEnd") VALUES (OLD."Assists", OLD."AverageLifeSeconds", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."ExtendedMetricsJson", OLD."Exterminations", OLD."GameVersionID", OLD."Gamertag", OLD."GamesPlayed", OLD."Goals", OLD."HammerKills", OLD."KdRatio", OLD."KdSpread", OLD."Kills", OLD."Losses", OLD."MatchmakingPlayerStatID", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."MultiKills", OLD."PeriodStart", OLD."Punches", OLD."Score", OLD."Sprees", OLD."SwordKills", OLD."TotalSecondsPlayed", OLD."UserID", OLD."WinPercent", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "PlayerStats" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "PlayerStats_History_Delete" AFTER DELETE ON "PlayerStats" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerStatsHistory" ("Assists", "AverageLifeSeconds", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "ExtendedMetricsJson", "Exterminations", "GameVersionID", "Gamertag", "GamesPlayed", "Goals", "HammerKills", "KdRatio", "KdSpread", "Kills", "Losses", "MatchmakingPlayerStatID", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "MultiKills", "PeriodStart", "Punches", "Score", "Sprees", "SwordKills", "TotalSecondsPlayed", "UserID", "WinPercent", "Wins", "PeriodEnd") VALUES (OLD."Assists", OLD."AverageLifeSeconds", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."ExtendedMetricsJson", OLD."Exterminations", OLD."GameVersionID", OLD."Gamertag", OLD."GamesPlayed", OLD."Goals", OLD."HammerKills", OLD."KdRatio", OLD."KdSpread", OLD."Kills", OLD."Losses", OLD."MatchmakingPlayerStatID", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."MultiKills", OLD."PeriodStart", OLD."Punches", OLD."Score", OLD."Sprees", OLD."SwordKills", OLD."TotalSecondsPlayed", OLD."UserID", OLD."WinPercent", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Leagues_History_Update";
                DROP TRIGGER IF EXISTS "Leagues_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Update";
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchmakingSeasons_History_Update";
                DROP TRIGGER IF EXISTS "MatchmakingSeasons_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PlayerStats_History_Update";
                DROP TRIGGER IF EXISTS "PlayerStats_History_Delete";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropTable(
                name: "MatchmakingSeasonsHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "PlayerStats",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "PlayerStatsHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchmakingSeasons",
                schema: "Matchmaking");

            migrationBuilder.DropIndex(
                name: "IX_MatchedMatchesHistory_MatchmakingSeasonID_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory");

            migrationBuilder.DropIndex(
                name: "IX_MatchedMatchesHistory_RecordedGameID_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory");

            migrationBuilder.DropIndex(
                name: "IX_MatchedMatches_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropIndex(
                name: "IX_MatchedMatches_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory");

            migrationBuilder.DropColumn(
                name: "RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory");

            migrationBuilder.DropColumn(
                name: "MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.InsertData(
                schema: "Event",
                table: "Leagues",
                columns: new[] { "LeagueID", "CreatedAt", "CreatedByID", "LeagueName", "ModifiedAt", "ModifiedByID", "Slug" },
                values: new object[] { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Late League", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "late-league" });

            migrationBuilder.Sql("""
                CREATE TRIGGER "Leagues_History_Update" AFTER UPDATE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Leagues" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Leagues_History_Delete" AFTER DELETE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedMatches_History_Update" AFTER UPDATE ON "MatchedMatches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedMatchesHistory" ("Active", "AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartedAt", "ThreadID", "VoteMessageID", "PeriodEnd") VALUES (OLD."Active", OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartedAt", OLD."ThreadID", OLD."VoteMessageID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedMatches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedMatches_History_Delete" AFTER DELETE ON "MatchedMatches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedMatchesHistory" ("Active", "AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartedAt", "ThreadID", "VoteMessageID", "PeriodEnd") VALUES (OLD."Active", OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartedAt", OLD."ThreadID", OLD."VoteMessageID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
