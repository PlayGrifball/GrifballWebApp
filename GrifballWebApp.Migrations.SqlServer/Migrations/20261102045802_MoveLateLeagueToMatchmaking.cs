using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
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
                UPDATE [Recorded].[Games] SET [SeasonID] = NULL WHERE [SeasonID] IN (SELECT [SeasonID] FROM [Event].[Seasons] WHERE [HistoricalSourceID] IS NOT NULL AND ([Format] = 3 OR [LeagueID] = 2));
                UPDATE [Recorded].[GameTeams] SET [TeamID] = NULL WHERE [TeamID] IN (SELECT [TeamID] FROM [Event].[Teams] WHERE [SeasonID] IN (SELECT [SeasonID] FROM [Event].[Seasons] WHERE [HistoricalSourceID] IS NOT NULL AND ([Format] = 3 OR [LeagueID] = 2)));
                UPDATE [Event].[GamertagClaims] SET [TeamID] = NULL WHERE [TeamID] IN (SELECT [TeamID] FROM [Event].[Teams] WHERE [SeasonID] IN (SELECT [SeasonID] FROM [Event].[Seasons] WHERE [HistoricalSourceID] IS NOT NULL AND ([Format] = 3 OR [LeagueID] = 2)));
                UPDATE [Event].[PlayerSeasonStats] SET [TeamID] = NULL WHERE [TeamID] IN (SELECT [TeamID] FROM [Event].[Teams] WHERE [SeasonID] IN (SELECT [SeasonID] FROM [Event].[Seasons] WHERE [HistoricalSourceID] IS NOT NULL AND ([Format] = 3 OR [LeagueID] = 2)));
                UPDATE [Event].[Vods] SET [SeasonID] = NULL WHERE [SeasonID] IN (SELECT [SeasonID] FROM [Event].[Seasons] WHERE [HistoricalSourceID] IS NOT NULL AND ([Format] = 3 OR [LeagueID] = 2));
                DELETE FROM [Event].[PlayerChampionships] WHERE [SeasonID] IN (SELECT [SeasonID] FROM [Event].[Seasons] WHERE [HistoricalSourceID] IS NOT NULL AND ([Format] = 3 OR [LeagueID] = 2));
                DELETE FROM [Event].[SeasonMatches] WHERE [SeasonID] IN (SELECT [SeasonID] FROM [Event].[Seasons] WHERE [HistoricalSourceID] IS NOT NULL AND ([Format] = 3 OR [LeagueID] = 2));
                UPDATE [Event].[Teams] SET [CaptainID] = NULL WHERE [SeasonID] IN (SELECT [SeasonID] FROM [Event].[Seasons] WHERE [HistoricalSourceID] IS NOT NULL AND ([Format] = 3 OR [LeagueID] = 2));
                DELETE FROM [Event].[Seasons] WHERE [HistoricalSourceID] IS NOT NULL AND ([Format] = 3 OR [LeagueID] = 2);
                UPDATE [Event].[Seasons] SET [SeasonName] = LEFT([SeasonName], 85) + N' (Late League)' WHERE ([Format] = 3 OR [LeagueID] = 2) AND EXISTS (SELECT 1 FROM [Event].[Seasons] AS [o] WHERE [o].[LeagueID] = 1 AND [o].[SeasonName] = [Event].[Seasons].[SeasonName]);
                UPDATE [Event].[Seasons] SET [LeagueID] = 1, [Format] = 1 WHERE ([Format] = 3 OR [LeagueID] = 2);
                """);

            migrationBuilder.DeleteData(
                schema: "Event",
                table: "Leagues",
                keyColumn: "LeagueID",
                keyValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatchmakingSeasons",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchmakingSeasonID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GameVersionID = table.Column<int>(type: "int", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HistoricalSourceID = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HistoricalSourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    CreatedByID = table.Column<int>(type: "int", nullable: true),
                    ModifiedByID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchmakingSeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "PlayerStats",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchmakingPlayerStatID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchmakingSeasonID = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    CreatedByID = table.Column<int>(type: "int", nullable: true),
                    ModifiedByID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    GameVersionID = table.Column<int>(type: "int", nullable: false),
                    Gamertag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GamesPlayed = table.Column<int>(type: "int", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    WinPercent = table.Column<decimal>(type: "decimal(6,3)", nullable: true),
                    Goals = table.Column<int>(type: "int", nullable: false),
                    Kills = table.Column<int>(type: "int", nullable: false),
                    Deaths = table.Column<int>(type: "int", nullable: false),
                    Assists = table.Column<int>(type: "int", nullable: false),
                    KdRatio = table.Column<decimal>(type: "decimal(6,3)", nullable: true),
                    KdSpread = table.Column<int>(type: "int", nullable: true),
                    CarrierKills = table.Column<int>(type: "int", nullable: false),
                    AverageLifeSeconds = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    TotalSecondsPlayed = table.Column<int>(type: "int", nullable: true),
                    Sprees = table.Column<int>(type: "int", nullable: false),
                    MultiKills = table.Column<int>(type: "int", nullable: false),
                    Exterminations = table.Column<int>(type: "int", nullable: false),
                    Betrayals = table.Column<int>(type: "int", nullable: false),
                    HammerKills = table.Column<int>(type: "int", nullable: true),
                    SwordKills = table.Column<int>(type: "int", nullable: true),
                    BallKills = table.Column<int>(type: "int", nullable: true),
                    Punches = table.Column<int>(type: "int", nullable: true),
                    Score = table.Column<int>(type: "int", nullable: true),
                    DamageDealt = table.Column<int>(type: "int", nullable: true),
                    DamageTaken = table.Column<int>(type: "int", nullable: true),
                    ExtendedMetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PlayerStatsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

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
                unique: true,
                filter: "[RecordedGameID] IS NOT NULL");

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
                unique: true,
                filter: "[HistoricalSourceID] IS NOT NULL");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_Games_RecordedGameID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedMatches_MatchmakingSeasons_MatchmakingSeasonID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropTable(
                name: "PlayerStats",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PlayerStatsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MatchmakingSeasons",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchmakingSeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

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
        }
    }
}
