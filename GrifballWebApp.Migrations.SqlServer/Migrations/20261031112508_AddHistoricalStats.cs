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

            // The app's own seasons so far are Halo Infinite's (a season now always has its game): given as the column's
            // default, so the rows get it without a new version in their history, then the default is dropped
            migrationBuilder.AddColumn<int>(
                name: "GameVersionID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: false,
                defaultValue: 6);

            // Not null while it was added, so SQL Server fills the rows there are (a nullable column it leaves null)
            migrationBuilder.AlterColumn<int>(
                name: "GameVersionID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: false,
                oldDefaultValue: 6);

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

            // Imported teams keep the names the sheets gave them, a few longer than the app lets a name be. A longer
            // nvarchar keeps the unique index on (TeamName, SeasonID).
            migrationBuilder.AlterColumn<string>(
                name: "TeamName",
                schema: "Event",
                table: "Teams",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedLosses",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedWins",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true);

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
                    TeamID = table.Column<int>(type: "int", nullable: true),
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
                        name: "FK_PlayerSeasonStats_Teams_TeamID",
                        column: x => x.TeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID");
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
                name: "PastGamertags",
                schema: "Xbox",
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
                    table.PrimaryKey("PK_PastGamertags", x => x.XboxGamertagHistoryID);
                    table.ForeignKey(
                        name: "FK_PastGamertags_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PastGamertagsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
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
                    TeamID = table.Column<int>(type: "int", nullable: false),
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
                        name: "FK_PlayerChampionships_Teams_TeamID",
                        column: x => x.TeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID");
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
                name: "IX_PlayerChampionships_TeamID",
                schema: "Event",
                table: "PlayerChampionships",
                column: "TeamID");

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
                name: "IX_PlayerSeasonStats_TeamID",
                schema: "Event",
                table: "PlayerSeasonStats",
                column: "TeamID");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStats_UserID",
                schema: "Event",
                table: "PlayerSeasonStats",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_PastGamertags_NormalizedGamertag",
                schema: "Xbox",
                table: "PastGamertags",
                column: "NormalizedGamertag");

            migrationBuilder.CreateIndex(
                name: "IX_PastGamertags_XboxUserID_NormalizedGamertag",
                schema: "Xbox",
                table: "PastGamertags",
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
            // does (HistoricalKeys): every run of whitespace (spaces, tabs, line breaks) made one space (each space marked <>, the
            // marks between two spaces taken out: gamertags have no < or >), trimmed, upper-cased.
            migrationBuilder.Sql("""
                WITH v AS (
                    SELECT h.[XboxUserID], h.[PeriodStart], h.[PeriodEnd],
                        CAST(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(h.[Gamertag], NCHAR(9), N' '), NCHAR(10), N' '), NCHAR(13), N' '), N' ', N'<>'), N'><', N''), N'<>', N' '))) AS nvarchar(50)) AS [Clean],
                        CAST(UPPER(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(x.[Gamertag], NCHAR(9), N' '), NCHAR(10), N' '), NCHAR(13), N' '), N' ', N'<>'), N'><', N''), N'<>', N' ')))) AS nvarchar(50)) AS [Current]
                    FROM [Xbox].[XboxUsersHistory] AS h
                    JOIN [Xbox].[XboxUsers] AS x ON x.[XboxUserID] = h.[XboxUserID]
                )
                INSERT INTO [Xbox].[PastGamertags] ([XboxUserID], [Gamertag], [NormalizedGamertag], [FirstSeen], [LastSeen], [Source], [CreatedAt], [ModifiedAt])
                SELECT [XboxUserID], MAX([Clean]), UPPER([Clean]), MIN([PeriodStart]), MAX([PeriodEnd]), 1, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM v
                WHERE UPPER([Clean]) <> [Current]
                GROUP BY [XboxUserID], UPPER([Clean]);
                """);

        

            // Round 3 of the review: championships take their league from the season, claims link their team, stat rows
            // have no division of their own (the gamertag history is created in the Xbox schema as PastGamertags above)
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerChampionships_Leagues_LeagueID",
                schema: "Event",
                table: "PlayerChampionships");


            migrationBuilder.DropIndex(
                name: "IX_PlayerChampionships_LeagueID",
                schema: "Event",
                table: "PlayerChampionships");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims");


            migrationBuilder.DropColumn(
                name: "Division",
                schema: "Event",
                table: "PlayerSeasonStats");

            migrationBuilder.DropColumn(
                name: "LeagueID",
                schema: "Event",
                table: "PlayerChampionships");

            migrationBuilder.DropColumn(
                name: "TeamName",
                schema: "Event",
                table: "GamertagClaims");





            migrationBuilder.AddColumn<int>(
                name: "TeamID",
                schema: "Event",
                table: "GamertagClaims",
                type: "int",
                nullable: true);


            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims",
                columns: new[] { "Kind", "XboxUserID", "NormalizedGamertag" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_TeamID",
                schema: "Event",
                table: "GamertagClaims",
                column: "TeamID");

            migrationBuilder.AddForeignKey(
                name: "FK_GamertagClaims_Teams_TeamID",
                schema: "Event",
                table: "GamertagClaims",
                column: "TeamID",
                principalSchema: "Event",
                principalTable: "Teams",
                principalColumn: "TeamID");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "Xbox",
                table: "PastGamertags",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentGamertag",
                schema: "Event",
                table: "GamertagClaims",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LookedUpAt",
                schema: "Event",
                table: "GamertagClaims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "XuidLookups",
                schema: "Xbox",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false),
                    LookedUpAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XuidLookups", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_XuidLookups_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XuidLookups_LookedUpAt",
                schema: "Xbox",
                table: "XuidLookups",
                column: "LookedUpAt");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.EnsureSchema(
                name: "Recorded");

            migrationBuilder.AddColumn<int>(
                name: "RegionSource",
                schema: "Auth",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Format",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ChangedSourceIDs",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Guarded",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "SeasonKeys",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotIDs",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Trigger",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<long>(
                name: "XboxUserID",
                schema: "Event",
                table: "GamertagClaims",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<int>(
                name: "ClaimantUserID",
                schema: "Event",
                table: "GamertagClaims",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstSeen",
                schema: "Event",
                table: "GamertagClaims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                schema: "Event",
                table: "GamertagClaims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "Event",
                table: "GamertagClaims",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DuplicateDismissals",
                schema: "Auth",
                columns: table => new
                {
                    LowUserID = table.Column<int>(type: "int", nullable: false),
                    HighUserID = table.Column<int>(type: "int", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DismissedByUserID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateDismissals", x => new { x.LowUserID, x.HighUserID });
                    table.CheckConstraint("CK_Auth_DuplicateDismissals_Order", "LowUserID < HighUserID");
                });

            migrationBuilder.CreateTable(
                name: "Games",
                schema: "Recorded",
                columns: table => new
                {
                    RecordedGameID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameVersionID = table.Column<int>(type: "int", nullable: false),
                    SeasonID = table.Column<int>(type: "int", nullable: true),
                    Map = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Gametype = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlayedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsPlayoffs = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_Games", x => x.RecordedGameID);
                    table.ForeignKey(
                        name: "FK_Games_GameVersions_GameVersionID",
                        column: x => x.GameVersionID,
                        principalSchema: "Other",
                        principalTable: "GameVersions",
                        principalColumn: "GameVesionID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Games_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GamesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Recorded")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "HistoricalImportSettings",
                schema: "Event",
                columns: table => new
                {
                    HistoricalImportSettingsID = table.Column<int>(type: "int", nullable: false),
                    SchedulePaused = table.Column<bool>(type: "bit", nullable: false),
                    LastScheduledRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastScheduledJobID = table.Column<int>(type: "int", nullable: true),
                    LastScheduledRunNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedByID = table.Column<int>(type: "int", nullable: true),
                    ModifiedByID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalImportSettings", x => x.HistoricalImportSettingsID);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalSeasonReviews",
                schema: "Event",
                columns: table => new
                {
                    HistoricalSeasonReviewID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistoricalImportJobID = table.Column<int>(type: "int", nullable: false),
                    SeasonKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SeasonName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatsBefore = table.Column<int>(type: "int", nullable: false),
                    StatsRemoved = table.Column<int>(type: "int", nullable: false),
                    TeamsBefore = table.Column<int>(type: "int", nullable: false),
                    TeamsRemoved = table.Column<int>(type: "int", nullable: false),
                    TitlesBefore = table.Column<int>(type: "int", nullable: false),
                    TitlesRemoved = table.Column<int>(type: "int", nullable: false),
                    ApprovalJobID = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByID = table.Column<int>(type: "int", nullable: true),
                    ModifiedByID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalSeasonReviews", x => x.HistoricalSeasonReviewID);
                    table.ForeignKey(
                        name: "FK_HistoricalSeasonReviews_HistoricalImportJobs_HistoricalImportJobID",
                        column: x => x.HistoricalImportJobID,
                        principalSchema: "Event",
                        principalTable: "HistoricalImportJobs",
                        principalColumn: "HistoricalImportJobID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalSources",
                schema: "Event",
                columns: table => new
                {
                    HistoricalSourceID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SheetKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Gid = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CsvSheetName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WaybackTimestamp = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    League = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GameVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Format = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    IsDead = table.Column<bool>(type: "bit", nullable: false),
                    LastFetchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFetchStatus = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LastChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByID = table.Column<int>(type: "int", nullable: true),
                    ModifiedByID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalSources", x => x.HistoricalSourceID);
                });

            migrationBuilder.CreateTable(
                name: "UserMerges",
                schema: "Auth",
                columns: table => new
                {
                    UserMergeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MergedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TriggeredByUserID = table.Column<int>(type: "int", nullable: true),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceUserID = table.Column<int>(type: "int", nullable: false),
                    SourceUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TargetUserID = table.Column<int>(type: "int", nullable: false),
                    TargetUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Manifest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowsMoved = table.Column<int>(type: "int", nullable: false),
                    RowsRemoved = table.Column<int>(type: "int", nullable: false),
                    RowsAdded = table.Column<int>(type: "int", nullable: false),
                    UndoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UndoneByUserID = table.Column<int>(type: "int", nullable: true),
                    UndoNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMerges", x => x.UserMergeID);
                });

            migrationBuilder.CreateTable(
                name: "GameTeams",
                schema: "Recorded",
                columns: table => new
                {
                    RecordedGameTeamID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordedGameID = table.Column<int>(type: "int", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    TeamID = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_GameTeams", x => x.RecordedGameTeamID);
                    table.ForeignKey(
                        name: "FK_GameTeams_Games_RecordedGameID",
                        column: x => x.RecordedGameID,
                        principalSchema: "Recorded",
                        principalTable: "Games",
                        principalColumn: "RecordedGameID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameTeams_Teams_TeamID",
                        column: x => x.TeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GameTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Recorded")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "SeriesGames",
                schema: "Event",
                columns: table => new
                {
                    SeriesGameID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonMatchID = table.Column<int>(type: "int", nullable: false),
                    GameNumber = table.Column<int>(type: "int", nullable: false),
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedGameID = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_SeriesGames", x => x.SeriesGameID);
                    table.CheckConstraint("CK_Event_SeriesGames_OneGame", "(MatchID IS NOT NULL AND RecordedGameID IS NULL) OR (MatchID IS NULL AND RecordedGameID IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SeriesGames_Games_RecordedGameID",
                        column: x => x.RecordedGameID,
                        principalSchema: "Recorded",
                        principalTable: "Games",
                        principalColumn: "RecordedGameID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesGames_Matches_MatchID",
                        column: x => x.MatchID,
                        principalSchema: "Infinite",
                        principalTable: "Matches",
                        principalColumn: "MatchID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesGames_SeasonMatches_SeasonMatchID",
                        column: x => x.SeasonMatchID,
                        principalSchema: "Event",
                        principalTable: "SeasonMatches",
                        principalColumn: "SeasonMatchID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeriesGamesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "HistoricalSourceSnapshots",
                schema: "Event",
                columns: table => new
                {
                    HistoricalSourceSnapshotID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistoricalSourceID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalSourceSnapshots", x => x.HistoricalSourceSnapshotID);
                    table.ForeignKey(
                        name: "FK_HistoricalSourceSnapshots_HistoricalSources_HistoricalSourceID",
                        column: x => x.HistoricalSourceID,
                        principalSchema: "Event",
                        principalTable: "HistoricalSources",
                        principalColumn: "HistoricalSourceID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameParticipants",
                schema: "Recorded",
                columns: table => new
                {
                    RecordedGameParticipantID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordedGameID = table.Column<int>(type: "int", nullable: false),
                    RecordedGameTeamID = table.Column<int>(type: "int", nullable: true),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    Gamertag = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kills = table.Column<int>(type: "int", nullable: false),
                    Deaths = table.Column<int>(type: "int", nullable: false),
                    Assists = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    Goals = table.Column<int>(type: "int", nullable: true),
                    CarrierKills = table.Column<int>(type: "int", nullable: true),
                    Betrayals = table.Column<int>(type: "int", nullable: true),
                    Suicides = table.Column<int>(type: "int", nullable: true),
                    HammerKills = table.Column<int>(type: "int", nullable: true),
                    SwordKills = table.Column<int>(type: "int", nullable: true),
                    BallKills = table.Column<int>(type: "int", nullable: true),
                    Punches = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_GameParticipants", x => x.RecordedGameParticipantID);
                    table.ForeignKey(
                        name: "FK_GameParticipants_GameTeams_RecordedGameTeamID",
                        column: x => x.RecordedGameTeamID,
                        principalSchema: "Recorded",
                        principalTable: "GameTeams",
                        principalColumn: "RecordedGameTeamID");
                    table.ForeignKey(
                        name: "FK_GameParticipants_Games_RecordedGameID",
                        column: x => x.RecordedGameID,
                        principalSchema: "Recorded",
                        principalTable: "Games",
                        principalColumn: "RecordedGameID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameParticipants_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GameParticipantsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Recorded")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.InsertData(
                schema: "Other",
                table: "Regions",
                columns: new[] { "RegionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "RegionName" },
                values: new object[] { 7, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Europe" });

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_ClaimantUserID",
                schema: "Event",
                table: "GamertagClaims",
                column: "ClaimantUserID");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Kind_ClaimantUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims",
                columns: new[] { "Kind", "ClaimantUserID", "NormalizedGamertag" },
                unique: true,
                filter: "[Status] IN (1, 2) AND [ClaimantUserID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims",
                columns: new[] { "Kind", "XboxUserID", "NormalizedGamertag" },
                unique: true,
                filter: "[Status] IN (1, 2) AND [ClaimantUserID] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_RecordedGameID",
                schema: "Recorded",
                table: "GameParticipants",
                column: "RecordedGameID");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_RecordedGameTeamID",
                schema: "Recorded",
                table: "GameParticipants",
                column: "RecordedGameTeamID");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_UserID",
                schema: "Recorded",
                table: "GameParticipants",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Games_GameVersionID",
                schema: "Recorded",
                table: "Games",
                column: "GameVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_Games_SeasonID",
                schema: "Recorded",
                table: "Games",
                column: "SeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_Games_SourceKey",
                schema: "Recorded",
                table: "Games",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameTeams_RecordedGameID_Side",
                schema: "Recorded",
                table: "GameTeams",
                columns: new[] { "RecordedGameID", "Side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameTeams_TeamID",
                schema: "Recorded",
                table: "GameTeams",
                column: "TeamID");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSeasonReviews_HistoricalImportJobID",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                column: "HistoricalImportJobID");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSeasonReviews_Status",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSourceSnapshots_HistoricalSourceID_FetchedAt",
                schema: "Event",
                table: "HistoricalSourceSnapshots",
                columns: new[] { "HistoricalSourceID", "FetchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesGames_MatchID",
                schema: "Event",
                table: "SeriesGames",
                column: "MatchID",
                unique: true,
                filter: "[MatchID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesGames_RecordedGameID",
                schema: "Event",
                table: "SeriesGames",
                column: "RecordedGameID",
                unique: true,
                filter: "[RecordedGameID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesGames_SeasonMatchID_GameNumber",
                schema: "Event",
                table: "SeriesGames",
                columns: new[] { "SeasonMatchID", "GameNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMerges_MergedAt",
                schema: "Auth",
                table: "UserMerges",
                column: "MergedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserMerges_SourceUserID",
                schema: "Auth",
                table: "UserMerges",
                column: "SourceUserID");

            migrationBuilder.CreateIndex(
                name: "IX_UserMerges_TargetUserID",
                schema: "Auth",
                table: "UserMerges",
                column: "TargetUserID");

            migrationBuilder.AddForeignKey(
                name: "FK_GamertagClaims_Users_ClaimantUserID",
                schema: "Event",
                table: "GamertagClaims",
                column: "ClaimantUserID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.Sql("INSERT INTO [Event].[SeriesGames] ([SeasonMatchID],[GameNumber],[MatchID],[RecordedGameID],[CreatedByID],[ModifiedByID],[CreatedAt],[ModifiedAt]) SELECT [SeasonMatchID],[MatchNumber],[MatchID],NULL,[CreatedByID],[ModifiedByID],[CreatedAt],[ModifiedAt] FROM [Event].[MatchLinks];");

            migrationBuilder.DropTable(
                name: "MatchLinks",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Vods",
                schema: "Event",
                columns: table => new
                {
                    VodID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YouTubeVideoID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtIsApproximate = table.Column<bool>(type: "bit", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ChannelID = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SeasonID = table.Column<int>(type: "int", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "int", nullable: true),
                    MatchStatus = table.Column<int>(type: "int", nullable: false),
                    MatchNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Vods", x => x.VodID);
                    table.ForeignKey(
                        name: "FK_Vods_SeasonMatches_SeasonMatchID",
                        column: x => x.SeasonMatchID,
                        principalSchema: "Event",
                        principalTable: "SeasonMatches",
                        principalColumn: "SeasonMatchID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Vods_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "VodsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "VodTeams",
                schema: "Event",
                columns: table => new
                {
                    VodID = table.Column<int>(type: "int", nullable: false),
                    TeamID = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_VodTeams", x => new { x.VodID, x.TeamID });
                    table.ForeignKey(
                        name: "FK_VodTeams_Teams_TeamID",
                        column: x => x.TeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VodTeams_Vods_VodID",
                        column: x => x.VodID,
                        principalSchema: "Event",
                        principalTable: "Vods",
                        principalColumn: "VodID");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "VodTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_Vods_MatchStatus",
                schema: "Event",
                table: "Vods",
                column: "MatchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Vods_SeasonID",
                schema: "Event",
                table: "Vods",
                column: "SeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_Vods_SeasonMatchID",
                schema: "Event",
                table: "Vods",
                column: "SeasonMatchID");

            migrationBuilder.CreateIndex(
                name: "IX_Vods_YouTubeVideoID",
                schema: "Event",
                table: "Vods",
                column: "YouTubeVideoID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VodTeams_TeamID",
                schema: "Event",
                table: "VodTeams",
                column: "TeamID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VodTeams",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "VodTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Vods",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "VodsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropForeignKey(
                name: "FK_GamertagClaims_Users_ClaimantUserID",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropTable(
                name: "DuplicateDismissals",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "HistoricalImportSettings",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "HistoricalSeasonReviews",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "HistoricalSourceSnapshots",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "UserMerges",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "HistoricalSources",
                schema: "Event");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_ClaimantUserID",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_Kind_ClaimantUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DeleteData(
                schema: "Other",
                table: "Regions",
                keyColumn: "RegionID",
                keyValue: 7);

            migrationBuilder.DropColumn(
                name: "RegionSource",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Format",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "ChangedSourceIDs",
                schema: "Event",
                table: "HistoricalImportJobs");

            migrationBuilder.DropColumn(
                name: "Guarded",
                schema: "Event",
                table: "HistoricalImportJobs");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "Event",
                table: "HistoricalImportJobs");

            migrationBuilder.DropColumn(
                name: "SeasonKeys",
                schema: "Event",
                table: "HistoricalImportJobs");

            migrationBuilder.DropColumn(
                name: "SnapshotIDs",
                schema: "Event",
                table: "HistoricalImportJobs");

            migrationBuilder.DropColumn(
                name: "Trigger",
                schema: "Event",
                table: "HistoricalImportJobs");

            migrationBuilder.DropColumn(
                name: "ClaimantUserID",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropColumn(
                name: "FirstSeen",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropColumn(
                name: "LastSeen",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                schema: "Event",
                table: "GamertagClaims");

            // SQL Server can't make an indexed column NOT NULL: the foreign key's index goes and comes back around it
            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_XboxUserID",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.AlterColumn<long>(
                name: "XboxUserID",
                schema: "Event",
                table: "GamertagClaims",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_XboxUserID",
                schema: "Event",
                table: "GamertagClaims",
                column: "XboxUserID");

            migrationBuilder.CreateTable(
                name: "MatchLinks",
                schema: "Event",
                columns: table => new
                {
                    MatchLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonMatchID = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByID = table.Column<int>(type: "int", nullable: true),
                    MatchNumber = table.Column<int>(type: "int", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedByID = table.Column<int>(type: "int", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLinks", x => x.MatchLinkID);
                    table.ForeignKey(
                        name: "FK_MatchLinks_Matches_MatchID",
                        column: x => x.MatchID,
                        principalSchema: "Infinite",
                        principalTable: "Matches",
                        principalColumn: "MatchID");
                    table.ForeignKey(
                        name: "FK_MatchLinks_SeasonMatches_SeasonMatchID",
                        column: x => x.SeasonMatchID,
                        principalSchema: "Event",
                        principalTable: "SeasonMatches",
                        principalColumn: "SeasonMatchID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims",
                columns: new[] { "Kind", "XboxUserID", "NormalizedGamertag" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinks_MatchID",
                schema: "Event",
                table: "MatchLinks",
                column: "MatchID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinks_SeasonMatchID_MatchNumber",
                schema: "Event",
                table: "MatchLinks",
                columns: new[] { "SeasonMatchID", "MatchNumber" },
                unique: true);

            migrationBuilder.Sql("INSERT INTO [Event].[MatchLinks] ([MatchID],[SeasonMatchID],[MatchNumber],[CreatedByID],[ModifiedByID],[CreatedAt],[ModifiedAt]) SELECT [MatchID],[SeasonMatchID],[GameNumber],[CreatedByID],[ModifiedByID],[CreatedAt],[ModifiedAt] FROM [Event].[SeriesGames] WHERE [MatchID] IS NOT NULL;");

            migrationBuilder.DropTable(
                name: "SeriesGames",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeriesGamesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "GameParticipants",
                schema: "Recorded")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GameParticipantsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Recorded")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "GameTeams",
                schema: "Recorded")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GameTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Recorded")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Games",
                schema: "Recorded")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GamesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Recorded")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "XuidLookups",
                schema: "Xbox");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "Xbox",
                table: "PastGamertags");

            migrationBuilder.DropColumn(
                name: "CurrentGamertag",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropColumn(
                name: "LookedUpAt",
                schema: "Event",
                table: "GamertagClaims");

            // Round 3 undone first (see the end of Up)
            migrationBuilder.DropForeignKey(
                name: "FK_GamertagClaims_Teams_TeamID",
                schema: "Event",
                table: "GamertagClaims");


            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_TeamID",
                schema: "Event",
                table: "GamertagClaims");


            migrationBuilder.DropColumn(
                name: "TeamID",
                schema: "Event",
                table: "GamertagClaims");





            migrationBuilder.AddColumn<string>(
                name: "Division",
                schema: "Event",
                table: "PlayerSeasonStats",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeagueID",
                schema: "Event",
                table: "PlayerChampionships",
                type: "int",
                nullable: false,
                defaultValue: 0);
            // Back from the season, which has the league now
            migrationBuilder.Sql("UPDATE c SET [LeagueID] = s.[LeagueID] FROM [Event].[PlayerChampionships] AS c JOIN [Event].[Seasons] AS s ON s.[SeasonID] = c.[SeasonID];");

            migrationBuilder.AddColumn<string>(
                name: "TeamName",
                schema: "Event",
                table: "GamertagClaims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);


            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionships_LeagueID",
                schema: "Event",
                table: "PlayerChampionships",
                column: "LeagueID");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims",
                columns: new[] { "Kind", "XboxUserID", "NormalizedGamertag" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerChampionships_Leagues_LeagueID",
                schema: "Event",
                table: "PlayerChampionships",
                column: "LeagueID",
                principalSchema: "Event",
                principalTable: "Leagues",
                principalColumn: "LeagueID",
                onDelete: ReferentialAction.Restrict);

        

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
            // EF may have turned versioning off already for this migration's other changes to the table (it turns it back on
            // at the end): only turn it off, and back on, if it is on.
            migrationBuilder.Sql("""
                DECLARE @versioned bit = CASE WHEN OBJECTPROPERTY(OBJECT_ID(N'[Event].[Seasons]'), N'TableTemporalType') = 2 THEN 1 ELSE 0 END;
                IF @versioned = 1 ALTER TABLE [Event].[Seasons] SET (SYSTEM_VERSIONING = OFF);
                -- In EXEC, so the batch compiles while the history table is still versioned
                EXEC(N'DELETE FROM [Event].[SeasonsHistory] WHERE [IsHistorical] = 1;
                    UPDATE [Event].[SeasonsHistory] SET [SeasonName] = LEFT([SeasonName], 30) WHERE LEN([SeasonName]) > 30;');
                IF @versioned = 1 ALTER TABLE [Event].[Seasons] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Event].[SeasonsHistory]));
                """);
            // The historical seasons' teams went with them, into the history: names longer than 30 are cut there (none of
            // the app's own teams has one), so TeamName can be 30 long again.
            // EF may have turned versioning off already for this migration's other changes to the table (it turns it back on
            // at the end): only turn it off, and back on, if it is on.
            migrationBuilder.Sql("""
                DECLARE @versioned bit = CASE WHEN OBJECTPROPERTY(OBJECT_ID(N'[Event].[Teams]'), N'TableTemporalType') = 2 THEN 1 ELSE 0 END;
                IF @versioned = 1 ALTER TABLE [Event].[Teams] SET (SYSTEM_VERSIONING = OFF);
                EXEC(N'UPDATE [Event].[TeamsHistory] SET [TeamName] = LEFT([TeamName], 30) WHERE LEN([TeamName]) > 30;');
                IF @versioned = 1 ALTER TABLE [Event].[Teams] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Event].[TeamsHistory]));
                """);

            migrationBuilder.DropColumn(
                name: "RecordedLosses",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "RecordedWins",
                schema: "Event",
                table: "Teams");

            // An index on the column stops it getting shorter: it is made again after
            migrationBuilder.DropIndex(
                name: "IX_Teams_TeamName_SeasonID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.AlterColumn<string>(
                name: "TeamName",
                schema: "Event",
                table: "Teams",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TeamName_SeasonID",
                schema: "Event",
                table: "Teams",
                columns: new[] { "TeamName", "SeasonID" },
                unique: true,
                filter: "[TeamName] IS NOT NULL");

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
                name: "PastGamertags",
                schema: "Xbox")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PastGamertagsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
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
