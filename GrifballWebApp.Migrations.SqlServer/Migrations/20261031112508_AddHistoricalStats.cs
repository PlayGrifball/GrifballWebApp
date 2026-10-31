using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Seasons_SeasonName",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.AlterColumn<string>(
                name: "SeasonName",
                schema: "Event",
                table: "Seasons",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "Division",
                schema: "Event",
                table: "Seasons",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GameVersionID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceID",
                schema: "Event",
                table: "Seasons",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceUrl",
                schema: "Event",
                table: "Seasons",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHistorical",
                schema: "Event",
                table: "Seasons",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The seasons there are now are the app's own, which are AGLA's (LeagueID 1, inserted below before the
            // foreign key is added): they get it here, as the column's default.
            migrationBuilder.AddColumn<int>(
                name: "LeagueID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "Other",
                table: "GameVersions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "GamertagClaims",
                schema: "Event",
                columns: table => new
                {
                    GamertagClaimID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    Gamertag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NormalizedGamertag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlaceholderUserID = table.Column<int>(type: "int", nullable: true),
                    SeasonID = table.Column<int>(type: "int", nullable: true),
                    TeamName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_GamertagClaims", x => x.GamertagClaimID);
                    table.ForeignKey(
                        name: "FK_GamertagClaims_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GamertagClaims_Users_PlaceholderUserID",
                        column: x => x.PlaceholderUserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GamertagClaims_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GamertagClaimsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "HistoricalImportJobs",
                schema: "Event",
                columns: table => new
                {
                    HistoricalImportJobID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SeasonsDone = table.Column<int>(type: "int", nullable: false),
                    SeasonsTotal = table.Column<int>(type: "int", nullable: false),
                    Report = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HeartbeatAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByID = table.Column<int>(type: "int", nullable: true),
                    ModifiedByID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalImportJobs", x => x.HistoricalImportJobID);
                });

            migrationBuilder.CreateTable(
                name: "Leagues",
                schema: "Event",
                columns: table => new
                {
                    LeagueID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeagueName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_Leagues", x => x.LeagueID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "LeaguesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "PlayerSeasonStats",
                schema: "Event",
                columns: table => new
                {
                    PlayerSeasonStatID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    SeasonID = table.Column<int>(type: "int", nullable: false),
                    GameVersionID = table.Column<int>(type: "int", nullable: false),
                    Gamertag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Division = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TradeNote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RoleNote = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    StintIndex = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    IsPlayoffs = table.Column<bool>(type: "bit", nullable: false),
                    IsCombined = table.Column<bool>(type: "bit", nullable: false),
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
                    ExtendedMetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_PlayerSeasonStats", x => x.PlayerSeasonStatID);
                    table.ForeignKey(
                        name: "FK_PlayerSeasonStats_GameVersions_GameVersionID",
                        column: x => x.GameVersionID,
                        principalSchema: "Other",
                        principalTable: "GameVersions",
                        principalColumn: "GameVesionID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerSeasonStats_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerSeasonStats_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PlayerSeasonStatsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "XboxGamertagHistory",
                schema: "Event",
                columns: table => new
                {
                    XboxGamertagHistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    Gamertag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NormalizedGamertag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_XboxGamertagHistory", x => x.XboxGamertagHistoryID);
                    table.ForeignKey(
                        name: "FK_XboxGamertagHistory_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "XboxGamertagHistoryHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "PlayerChampionships",
                schema: "Event",
                columns: table => new
                {
                    PlayerChampionshipID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    SeasonID = table.Column<int>(type: "int", nullable: false),
                    LeagueID = table.Column<int>(type: "int", nullable: false),
                    GameVersionID = table.Column<int>(type: "int", nullable: true),
                    Tier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsCaptain = table.Column<bool>(type: "bit", nullable: false),
                    GamertagUsed = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_PlayerChampionships", x => x.PlayerChampionshipID);
                    table.ForeignKey(
                        name: "FK_PlayerChampionships_GameVersions_GameVersionID",
                        column: x => x.GameVersionID,
                        principalSchema: "Other",
                        principalTable: "GameVersions",
                        principalColumn: "GameVesionID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerChampionships_Leagues_LeagueID",
                        column: x => x.LeagueID,
                        principalSchema: "Event",
                        principalTable: "Leagues",
                        principalColumn: "LeagueID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerChampionships_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerChampionships_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PlayerChampionshipsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 1,
                column: "Slug",
                value: "halo3");

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 2,
                column: "Slug",
                value: "reach");

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 3,
                column: "Slug",
                value: "reach-dash");

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 4,
                column: "Slug",
                value: "halo4");

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 5,
                column: "Slug",
                value: "halo5");

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 6,
                column: "Slug",
                value: "infinite");

            migrationBuilder.InsertData(
                schema: "Event",
                table: "Leagues",
                columns: new[] { "LeagueID", "CreatedAt", "CreatedByID", "LeagueName", "ModifiedAt", "ModifiedByID", "Slug" },
                values: new object[,]
                {
                    { 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "AGLA", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "agla" },
                    { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Late League", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "late-league" },
                    { 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "GWL", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "gwl" },
                    { 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "IGL", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "igl" },
                    { 5, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Community/Side", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "side" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_GameVersionID",
                schema: "Event",
                table: "Seasons",
                column: "GameVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_HistoricalSourceID",
                schema: "Event",
                table: "Seasons",
                column: "HistoricalSourceID",
                unique: true,
                filter: "[HistoricalSourceID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_LeagueID_SeasonName",
                schema: "Event",
                table: "Seasons",
                columns: new[] { "LeagueID", "SeasonName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameVersions_Slug",
                schema: "Other",
                table: "GameVersions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims",
                columns: new[] { "Kind", "XboxUserID", "NormalizedGamertag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_PlaceholderUserID",
                schema: "Event",
                table: "GamertagClaims",
                column: "PlaceholderUserID");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_SeasonID",
                schema: "Event",
                table: "GamertagClaims",
                column: "SeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Status",
                schema: "Event",
                table: "GamertagClaims",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_XboxUserID",
                schema: "Event",
                table: "GamertagClaims",
                column: "XboxUserID");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalImportJobs_Status",
                schema: "Event",
                table: "HistoricalImportJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_LeagueName",
                schema: "Event",
                table: "Leagues",
                column: "LeagueName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_Slug",
                schema: "Event",
                table: "Leagues",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionships_GamertagUsed",
                schema: "Event",
                table: "PlayerChampionships",
                column: "GamertagUsed");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionships_GameVersionID",
                schema: "Event",
                table: "PlayerChampionships",
                column: "GameVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionships_LeagueID",
                schema: "Event",
                table: "PlayerChampionships",
                column: "LeagueID");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionships_SeasonID_Tier_UserID",
                schema: "Event",
                table: "PlayerChampionships",
                columns: new[] { "SeasonID", "Tier", "UserID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionships_UserID",
                schema: "Event",
                table: "PlayerChampionships",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStats_Gamertag",
                schema: "Event",
                table: "PlayerSeasonStats",
                column: "Gamertag");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStats_GameVersionID",
                schema: "Event",
                table: "PlayerSeasonStats",
                column: "GameVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStats_SeasonID_UserID_StintIndex_IsPlayoffs",
                schema: "Event",
                table: "PlayerSeasonStats",
                columns: new[] { "SeasonID", "UserID", "StintIndex", "IsPlayoffs" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStats_UserID",
                schema: "Event",
                table: "PlayerSeasonStats",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_XboxGamertagHistory_NormalizedGamertag",
                schema: "Event",
                table: "XboxGamertagHistory",
                column: "NormalizedGamertag");

            migrationBuilder.CreateIndex(
                name: "IX_XboxGamertagHistory_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "XboxGamertagHistory",
                columns: new[] { "XboxUserID", "NormalizedGamertag" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_GameVersions_GameVersionID",
                schema: "Event",
                table: "Seasons",
                column: "GameVersionID",
                principalSchema: "Other",
                principalTable: "GameVersions",
                principalColumn: "GameVesionID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Leagues_LeagueID",
                schema: "Event",
                table: "Seasons",
                column: "LeagueID",
                principalSchema: "Event",
                principalTable: "Leagues",
                principalColumn: "LeagueID",
                onDelete: ReferentialAction.Restrict);
            // The renames the XboxUsers table's own history already holds: each account's earlier gamertags, from when the
            // first version with it began to when the last one ended. Read from the history table itself: FOR SYSTEM_TIME leaves out
            // versions that lasted no time at all (two renames in one transaction's time). Gamertags are normalised as the app
            // does (HistoricalKeys): trimmed, runs of spaces made one (each REPLACE halves them), upper-cased.
            migrationBuilder.Sql("""
                WITH v AS (
                    SELECT h.[XboxUserID], h.[PeriodStart], h.[PeriodEnd],
                        CAST(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(REPLACE(h.[Gamertag], N'  ', N' '), N'  ', N' '), N'  ', N' '), N'  ', N' '))) AS nvarchar(50)) AS [Clean],
                        CAST(UPPER(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(REPLACE(x.[Gamertag], N'  ', N' '), N'  ', N' '), N'  ', N' '), N'  ', N' ')))) AS nvarchar(50)) AS [Current]
                    FROM [Xbox].[XboxUsersHistory] AS h
                    JOIN [Xbox].[XboxUsers] AS x ON x.[XboxUserID] = h.[XboxUserID]
                )
                INSERT INTO [Event].[XboxGamertagHistory] ([XboxUserID], [Gamertag], [NormalizedGamertag], [FirstSeen], [LastSeen], [Source], [CreatedAt], [ModifiedAt])
                SELECT [XboxUserID], MAX([Clean]), UPPER([Clean]), MIN([PeriodStart]), MAX([PeriodEnd]), 1, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM v
                WHERE UPPER([Clean]) <> [Current]
                GROUP BY [XboxUserID], UPPER([Clean]);
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-lossy by necessity: historical seasons go (their stats and titles with them, by cascade), and so do their
            // old versions. SeasonName is 30 long again and unique on its own: a longer name, or one two leagues share, gets
            // its ID ("Name~12") so that it fits and the index can be made. The history can only be written to with
            // versioning off, each its own batch: SQL Server checks a batch that writes to the history table before
            // versioning is off. Placeholder users the import made are deleted at the end, once nothing refers to them.
            migrationBuilder.Sql("""
                DELETE FROM [Event].[Seasons] WHERE [IsHistorical] = 1;
                UPDATE s SET [SeasonName] = LEFT(s.[SeasonName], 29 - LEN(CAST(s.[SeasonID] AS nvarchar(11)))) + N'~' + CAST(s.[SeasonID] AS nvarchar(11))
                FROM [Event].[Seasons] AS s
                WHERE LEN(s.[SeasonName]) > 30
                    OR EXISTS (SELECT 1 FROM [Event].[Seasons] AS o WHERE o.[SeasonName] = s.[SeasonName] AND o.[SeasonID] < s.[SeasonID]);
                """);
            migrationBuilder.Sql("""
                ALTER TABLE [Event].[Seasons] SET (SYSTEM_VERSIONING = OFF);
                """);
            migrationBuilder.Sql("""
                DELETE FROM [Event].[SeasonsHistory] WHERE [IsHistorical] = 1;
                UPDATE [Event].[SeasonsHistory] SET [SeasonName] = LEFT([SeasonName], 30) WHERE LEN([SeasonName]) > 30;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE [Event].[Seasons] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Event].[SeasonsHistory]));
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_GameVersions_GameVersionID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Leagues_LeagueID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropTable(
                name: "GamertagClaims",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GamertagClaimsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "HistoricalImportJobs",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "PlayerChampionships",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PlayerChampionshipsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "PlayerSeasonStats",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PlayerSeasonStatsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "XboxGamertagHistory",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "XboxGamertagHistoryHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Leagues",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "LeaguesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_GameVersionID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_HistoricalSourceID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_LeagueID_SeasonName",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_GameVersions_Slug",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.DropColumn(
                name: "Division",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "GameVersionID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceUrl",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "IsHistorical",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "LeagueID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.AlterColumn<string>(
                name: "SeasonName",
                schema: "Event",
                table: "Seasons",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_SeasonName",
                schema: "Event",
                table: "Seasons",
                column: "SeasonName",
                unique: true);

            // Placeholder users the historical import made (and nothing refers to now)
            migrationBuilder.Sql("DELETE FROM [Auth].[Users] WHERE [IsDummyUser] = 1 AND [UserName] LIKE N'historical:%';");
        }
    }
}
