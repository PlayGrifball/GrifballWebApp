using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class MoveLateLeagueToMatchmaking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Late League is matchmaking now, not seasons: the Late League seasons an import made (format 3, the Late League
            // league) go, with their teams, rosters, stats, series and claims. Their recorded games stay, in no season, and their
            // VODs lose the link: the next import makes them matchmaking seasons and matches (found again by their keys).
            migrationBuilder.Sql("""
                UPDATE "Recorded"."Games" SET "SeasonID" = NULL WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Event"."Seasons" WHERE "Format" = 3 OR "LeagueID" = 2);
                UPDATE "Recorded"."GameTeams" SET "TeamID" = NULL WHERE "TeamID" IN (SELECT "TeamID" FROM "Event"."Teams" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Event"."Seasons" WHERE "Format" = 3 OR "LeagueID" = 2));
                UPDATE "Event"."GamertagClaims" SET "TeamID" = NULL WHERE "TeamID" IN (SELECT "TeamID" FROM "Event"."Teams" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Event"."Seasons" WHERE "Format" = 3 OR "LeagueID" = 2));
                UPDATE "Event"."PlayerSeasonStats" SET "TeamID" = NULL WHERE "TeamID" IN (SELECT "TeamID" FROM "Event"."Teams" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Event"."Seasons" WHERE "Format" = 3 OR "LeagueID" = 2));
                UPDATE "Event"."Vods" SET "SeasonID" = NULL WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Event"."Seasons" WHERE "Format" = 3 OR "LeagueID" = 2);
                DELETE FROM "Event"."PlayerChampionships" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Event"."Seasons" WHERE "Format" = 3 OR "LeagueID" = 2);
                DELETE FROM "Event"."SeasonMatches" WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Event"."Seasons" WHERE "Format" = 3 OR "LeagueID" = 2);
                UPDATE "Event"."Teams" SET "CaptainID" = NULL WHERE "SeasonID" IN (SELECT "SeasonID" FROM "Event"."Seasons" WHERE "Format" = 3 OR "LeagueID" = 2);
                DELETE FROM "Event"."Seasons" WHERE "Format" = 3 OR "LeagueID" = 2;
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
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Matchmaking\".\"MatchedMatches\"');");

            migrationBuilder.CreateTable(
                name: "MatchmakingSeasons",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchmakingSeasonID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HistoricalSourceID = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    HistoricalSourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    HistoricalSourceID = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    HistoricalSourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "case_insensitive"),
                    MatchmakingSeasonID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PlayerStatsHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    Assists = table.Column<int>(type: "integer", nullable: true),
                    AverageLifeSeconds = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    BallKills = table.Column<int>(type: "integer", nullable: true),
                    Betrayals = table.Column<int>(type: "integer", nullable: true),
                    CarrierKills = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DamageDealt = table.Column<int>(type: "integer", nullable: true),
                    DamageTaken = table.Column<int>(type: "integer", nullable: true),
                    Deaths = table.Column<int>(type: "integer", nullable: true),
                    ExtendedMetricsJson = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    Exterminations = table.Column<int>(type: "integer", nullable: true),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    GamesPlayed = table.Column<int>(type: "integer", nullable: true),
                    Goals = table.Column<int>(type: "integer", nullable: true),
                    HammerKills = table.Column<int>(type: "integer", nullable: true),
                    KdRatio = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    KdSpread = table.Column<int>(type: "integer", nullable: true),
                    Kills = table.Column<int>(type: "integer", nullable: true),
                    Losses = table.Column<int>(type: "integer", nullable: true),
                    MatchmakingPlayerStatID = table.Column<int>(type: "integer", nullable: true),
                    MatchmakingSeasonID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    MultiKills = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Punches = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Sprees = table.Column<int>(type: "integer", nullable: true),
                    SwordKills = table.Column<int>(type: "integer", nullable: true),
                    TotalSecondsPlayed = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    WinPercent = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    Wins = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PlayerStats",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchmakingPlayerStatID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchmakingSeasonID = table.Column<int>(type: "integer", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    GameVersionID = table.Column<int>(type: "integer", nullable: false),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    GamesPlayed = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false),
                    WinPercent = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    Goals = table.Column<int>(type: "integer", nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Deaths = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    KdRatio = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    KdSpread = table.Column<int>(type: "integer", nullable: true),
                    CarrierKills = table.Column<int>(type: "integer", nullable: false),
                    AverageLifeSeconds = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    TotalSecondsPlayed = table.Column<int>(type: "integer", nullable: true),
                    Sprees = table.Column<int>(type: "integer", nullable: false),
                    MultiKills = table.Column<int>(type: "integer", nullable: false),
                    Exterminations = table.Column<int>(type: "integer", nullable: false),
                    Betrayals = table.Column<int>(type: "integer", nullable: false),
                    HammerKills = table.Column<int>(type: "integer", nullable: true),
                    SwordKills = table.Column<int>(type: "integer", nullable: true),
                    BallKills = table.Column<int>(type: "integer", nullable: true),
                    Punches = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    DamageDealt = table.Column<int>(type: "integer", nullable: true),
                    DamageTaken = table.Column<int>(type: "integer", nullable: true),
                    ExtendedMetricsJson = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive")
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

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"MatchmakingSeasons\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"PlayerStats\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"PlayerStats\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"MatchmakingSeasons\"');");

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

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Matchmaking\".\"MatchedMatches\"');");

            migrationBuilder.InsertData(
                schema: "Event",
                table: "Leagues",
                columns: new[] { "LeagueID", "CreatedAt", "CreatedByID", "LeagueName", "ModifiedAt", "ModifiedByID", "Slug" },
                values: new object[] { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Late League", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "late-league" });
        }
    }
}
