using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                table: "SeasonsHistory",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true,
                oldCollation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Division",
                schema: "Event",
                table: "SeasonsHistory",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<int>(
                name: "GameVersionID",
                schema: "Event",
                table: "SeasonsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceID",
                schema: "Event",
                table: "SeasonsHistory",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceUrl",
                schema: "Event",
                table: "SeasonsHistory",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<bool>(
                name: "IsHistorical",
                schema: "Event",
                table: "SeasonsHistory",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeagueID",
                schema: "Event",
                table: "SeasonsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SeasonName",
                schema: "Event",
                table: "Seasons",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldCollation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Division",
                schema: "Event",
                table: "Seasons",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<int>(
                name: "GameVersionID",
                schema: "Event",
                table: "Seasons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceID",
                schema: "Event",
                table: "Seasons",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceUrl",
                schema: "Event",
                table: "Seasons",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<bool>(
                name: "IsHistorical",
                schema: "Event",
                table: "Seasons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The seasons there are now are the app's own, which are AGLA's (LeagueID 1, inserted below before the
            // foreign key is added): they get it here, as the column's default.
            migrationBuilder.AddColumn<int>(
                name: "LeagueID",
                schema: "Event",
                table: "Seasons",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"Seasons\"');");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "Other",
                table: "GameVersionsHistory",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "Other",
                table: "GameVersions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                collation: "case_insensitive");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Other\".\"GameVersions\"');");

            // Imported teams keep the names the sheets gave them, a few longer than the app lets a name be
            migrationBuilder.AlterColumn<string>(
                name: "TeamName",
                schema: "Event",
                table: "TeamsHistory",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true,
                oldCollation: "case_insensitive");

            migrationBuilder.AddColumn<int>(
                name: "RecordedLosses",
                schema: "Event",
                table: "TeamsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedWins",
                schema: "Event",
                table: "TeamsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TeamName",
                schema: "Event",
                table: "Teams",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true,
                oldCollation: "case_insensitive");

            migrationBuilder.AddColumn<int>(
                name: "RecordedLosses",
                schema: "Event",
                table: "Teams",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordedWins",
                schema: "Event",
                table: "Teams",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"Teams\"');");

            migrationBuilder.CreateTable(
                name: "GamertagClaims",
                schema: "Event",
                columns: table => new
                {
                    GamertagClaimID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    NormalizedGamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    PlaceholderUserID = table.Column<int>(type: "integer", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    TeamName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "GamertagClaimsHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    GamertagClaimID = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    NormalizedGamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlaceholderUserID = table.Column<int>(type: "integer", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    TeamName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HistoricalImportJobs",
                schema: "Event",
                columns: table => new
                {
                    HistoricalImportJobID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    Stage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    SeasonsDone = table.Column<int>(type: "integer", nullable: false),
                    SeasonsTotal = table.Column<int>(type: "integer", nullable: false),
                    Report = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    Error = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HeartbeatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    LeagueID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leagues", x => x.LeagueID);
                });

            migrationBuilder.CreateTable(
                name: "LeaguesHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    LeagueID = table.Column<int>(type: "integer", nullable: true),
                    LeagueName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PlayerChampionshipsHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    GamertagUsed = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    IsCaptain = table.Column<bool>(type: "boolean", nullable: true),
                    LeagueID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlayerChampionshipID = table.Column<int>(type: "integer", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    Tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    UserID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PlayerSeasonStats",
                schema: "Event",
                columns: table => new
                {
                    PlayerSeasonStatID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    GameVersionID = table.Column<int>(type: "integer", nullable: false),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    Division = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    TradeNote = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    RoleNote = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    StintIndex = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    IsPlayoffs = table.Column<bool>(type: "boolean", nullable: false),
                    IsCombined = table.Column<bool>(type: "boolean", nullable: false),
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
                    ExtendedMetricsJson = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "PlayerSeasonStatsHistory",
                schema: "Event",
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
                    Division = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    ExtendedMetricsJson = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    Exterminations = table.Column<int>(type: "integer", nullable: true),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    GamesPlayed = table.Column<int>(type: "integer", nullable: true),
                    Goals = table.Column<int>(type: "integer", nullable: true),
                    HammerKills = table.Column<int>(type: "integer", nullable: true),
                    IsCombined = table.Column<bool>(type: "boolean", nullable: true),
                    IsPlayoffs = table.Column<bool>(type: "boolean", nullable: true),
                    KdRatio = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    KdSpread = table.Column<int>(type: "integer", nullable: true),
                    Kills = table.Column<int>(type: "integer", nullable: true),
                    Losses = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    MultiKills = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlayerSeasonStatID = table.Column<int>(type: "integer", nullable: true),
                    Punches = table.Column<int>(type: "integer", nullable: true),
                    RoleNote = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: true),
                    Sprees = table.Column<int>(type: "integer", nullable: true),
                    StintIndex = table.Column<int>(type: "integer", nullable: true),
                    SwordKills = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    TotalSecondsPlayed = table.Column<int>(type: "integer", nullable: true),
                    TradeNote = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    WinPercent = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    Wins = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "XboxGamertagHistory",
                schema: "Event",
                columns: table => new
                {
                    XboxGamertagHistoryID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    NormalizedGamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    FirstSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "XboxGamertagHistoryHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    FirstSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    NormalizedGamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: true),
                    XboxGamertagHistoryID = table.Column<int>(type: "integer", nullable: true),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PlayerChampionships",
                schema: "Event",
                columns: table => new
                {
                    PlayerChampionshipID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    LeagueID = table.Column<int>(type: "integer", nullable: false),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    Tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    TeamID = table.Column<int>(type: "integer", nullable: false),
                    IsCaptain = table.Column<bool>(type: "boolean", nullable: false),
                    GamertagUsed = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                });

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
                name: "IX_SeasonsHistory_GameVersionID_PeriodEnd",
                schema: "Event",
                table: "SeasonsHistory",
                columns: new[] { "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonsHistory_LeagueID_PeriodEnd",
                schema: "Event",
                table: "SeasonsHistory",
                columns: new[] { "LeagueID", "PeriodEnd" });

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
                filter: "\"HistoricalSourceID\" IS NOT NULL");

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
                name: "IX_GamertagClaimsHistory_GamertagClaimID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory",
                columns: new[] { "GamertagClaimID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaimsHistory_PlaceholderUserID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory",
                columns: new[] { "PlaceholderUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaimsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaimsHistory_XboxUserID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory",
                columns: new[] { "XboxUserID", "PeriodEnd" });

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
                name: "IX_LeaguesHistory_LeagueID_PeriodEnd",
                schema: "Event",
                table: "LeaguesHistory",
                columns: new[] { "LeagueID", "PeriodEnd" });

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
                name: "IX_PlayerChampionshipsHistory_GameVersionID_PeriodEnd",
                schema: "Event",
                table: "PlayerChampionshipsHistory",
                columns: new[] { "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionshipsHistory_LeagueID_PeriodEnd",
                schema: "Event",
                table: "PlayerChampionshipsHistory",
                columns: new[] { "LeagueID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionshipsHistory_PlayerChampionshipID_PeriodEnd",
                schema: "Event",
                table: "PlayerChampionshipsHistory",
                columns: new[] { "PlayerChampionshipID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionshipsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "PlayerChampionshipsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionshipsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "PlayerChampionshipsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionshipsHistory_UserID_PeriodEnd",
                schema: "Event",
                table: "PlayerChampionshipsHistory",
                columns: new[] { "UserID", "PeriodEnd" });

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
                name: "IX_PlayerSeasonStatsHistory_GameVersionID_PeriodEnd",
                schema: "Event",
                table: "PlayerSeasonStatsHistory",
                columns: new[] { "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStatsHistory_PlayerSeasonStatID_PeriodEnd",
                schema: "Event",
                table: "PlayerSeasonStatsHistory",
                columns: new[] { "PlayerSeasonStatID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStatsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "PlayerSeasonStatsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStatsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "PlayerSeasonStatsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSeasonStatsHistory_UserID_PeriodEnd",
                schema: "Event",
                table: "PlayerSeasonStatsHistory",
                columns: new[] { "UserID", "PeriodEnd" });

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

            migrationBuilder.CreateIndex(
                name: "IX_XboxGamertagHistoryHistory_XboxGamertagHistoryID_PeriodEnd",
                schema: "Event",
                table: "XboxGamertagHistoryHistory",
                columns: new[] { "XboxGamertagHistoryID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_XboxGamertagHistoryHistory_XboxUserID_PeriodEnd",
                schema: "Event",
                table: "XboxGamertagHistoryHistory",
                columns: new[] { "XboxUserID", "PeriodEnd" });

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

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"GamertagClaims\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"Leagues\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"PlayerChampionships\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"PlayerSeasonStats\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"XboxGamertagHistory\"');");
            // The renames the XboxUsers table's history already holds: each account's earlier gamertags, from when the first
            // version with it began to when the last one ended ('-infinity', the start of rows history was added to, is unknown).
            // Gamertags are normalised as the app does (HistoricalKeys): trimmed, runs of whitespace made one space, upper-cased
            // (in the "C" collation: regular expressions refuse the case-insensitive one).
            migrationBuilder.Sql("""
                INSERT INTO "Event"."XboxGamertagHistory" ("XboxUserID", "Gamertag", "NormalizedGamertag", "FirstSeen", "LastSeen", "Source", "CreatedAt", "ModifiedAt")
                SELECT h."XboxUserID", left(max(btrim(regexp_replace(h."Gamertag" COLLATE "C", '\s+', ' ', 'g'))), 50), left(upper(btrim(regexp_replace(h."Gamertag" COLLATE "C", '\s+', ' ', 'g'))), 50),
                    min(NULLIF(h."PeriodStart", '-infinity')), max(h."PeriodEnd"), 1, now(), now()
                FROM "Xbox"."XboxUsersHistory" AS h
                JOIN "Xbox"."XboxUsers" AS x ON x."XboxUserID" = h."XboxUserID"
                WHERE upper(btrim(regexp_replace(h."Gamertag" COLLATE "C", '\s+', ' ', 'g'))) <> upper(btrim(regexp_replace(x."Gamertag" COLLATE "C", '\s+', ' ', 'g')))
                GROUP BY h."XboxUserID", left(upper(btrim(regexp_replace(h."Gamertag" COLLATE "C", '\s+', ' ', 'g'))), 50);
                """);

        
            // The app's own seasons so far are Halo Infinite's: a season now always has its game
            migrationBuilder.Sql("""UPDATE "Event"."Seasons" SET "GameVersionID" = 6 WHERE "GameVersionID" IS NULL;""");

            // Round 3 of the review: championships take their league from the season, claims link their team, stat rows
            // have no division of their own, and the gamertag history moves to the Xbox schema as PastGamertags
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerChampionships_Leagues_LeagueID",
                schema: "Event",
                table: "PlayerChampionships");

            migrationBuilder.DropForeignKey(
                name: "FK_XboxGamertagHistory_XboxUsers_XboxUserID",
                schema: "Event",
                table: "XboxGamertagHistory");

            migrationBuilder.DropIndex(
                name: "IX_PlayerChampionshipsHistory_LeagueID_PeriodEnd",
                schema: "Event",
                table: "PlayerChampionshipsHistory");

            migrationBuilder.DropIndex(
                name: "IX_PlayerChampionships_LeagueID",
                schema: "Event",
                table: "PlayerChampionships");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_XboxGamertagHistory",
                schema: "Event",
                table: "XboxGamertagHistory");

            migrationBuilder.DropColumn(
                name: "Division",
                schema: "Event",
                table: "PlayerSeasonStatsHistory");

            migrationBuilder.DropColumn(
                name: "Division",
                schema: "Event",
                table: "PlayerSeasonStats");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"PlayerSeasonStats\"');");

            migrationBuilder.DropColumn(
                name: "LeagueID",
                schema: "Event",
                table: "PlayerChampionshipsHistory");

            migrationBuilder.DropColumn(
                name: "LeagueID",
                schema: "Event",
                table: "PlayerChampionships");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"PlayerChampionships\"');");

            migrationBuilder.DropColumn(
                name: "TeamName",
                schema: "Event",
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "TeamName",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.RenameTable(
                name: "XboxGamertagHistoryHistory",
                schema: "Event",
                newName: "PastGamertagsHistory",
                newSchema: "Xbox");

            migrationBuilder.RenameTable(
                name: "XboxGamertagHistory",
                schema: "Event",
                newName: "PastGamertags",
                newSchema: "Xbox");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Xbox\".\"PastGamertags\"');");

            migrationBuilder.RenameIndex(
                name: "IX_XboxGamertagHistoryHistory_XboxUserID_PeriodEnd",
                schema: "Xbox",
                table: "PastGamertagsHistory",
                newName: "IX_PastGamertagsHistory_XboxUserID_PeriodEnd");

            migrationBuilder.RenameIndex(
                name: "IX_XboxGamertagHistoryHistory_XboxGamertagHistoryID_PeriodEnd",
                schema: "Xbox",
                table: "PastGamertagsHistory",
                newName: "IX_PastGamertagsHistory_XboxGamertagHistoryID_PeriodEnd");

            migrationBuilder.RenameIndex(
                name: "IX_XboxGamertagHistory_XboxUserID_NormalizedGamertag",
                schema: "Xbox",
                table: "PastGamertags",
                newName: "IX_PastGamertags_XboxUserID_NormalizedGamertag");

            migrationBuilder.RenameIndex(
                name: "IX_XboxGamertagHistory_NormalizedGamertag",
                schema: "Xbox",
                table: "PastGamertags",
                newName: "IX_PastGamertags_NormalizedGamertag");

            migrationBuilder.AddColumn<int>(
                name: "TeamID",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamID",
                schema: "Event",
                table: "GamertagClaims",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"GamertagClaims\"');");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PastGamertags",
                schema: "Xbox",
                table: "PastGamertags",
                column: "XboxGamertagHistoryID");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaimsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims",
                columns: new[] { "Kind", "XboxUserID", "NormalizedGamertag" },
                unique: true,
                filter: "\"Status\" IN (1, 2)");

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

            migrationBuilder.AddForeignKey(
                name: "FK_PastGamertags_XboxUsers_XboxUserID",
                schema: "Xbox",
                table: "PastGamertags",
                column: "XboxUserID",
                principalSchema: "Xbox",
                principalTable: "XboxUsers",
                principalColumn: "XboxUserID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Round 3 undone first (see the end of Up)
            migrationBuilder.DropForeignKey(
                name: "FK_GamertagClaims_Teams_TeamID",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_PastGamertags_XboxUsers_XboxUserID",
                schema: "Xbox",
                table: "PastGamertags");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaimsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaims_TeamID",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PastGamertags",
                schema: "Xbox",
                table: "PastGamertags");

            migrationBuilder.DropColumn(
                name: "TeamID",
                schema: "Event",
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "TeamID",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.RenameTable(
                name: "PastGamertagsHistory",
                schema: "Xbox",
                newName: "XboxGamertagHistoryHistory",
                newSchema: "Event");

            migrationBuilder.RenameTable(
                name: "PastGamertags",
                schema: "Xbox",
                newName: "XboxGamertagHistory",
                newSchema: "Event");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"XboxGamertagHistory\"');");

            migrationBuilder.RenameIndex(
                name: "IX_PastGamertagsHistory_XboxUserID_PeriodEnd",
                schema: "Event",
                table: "XboxGamertagHistoryHistory",
                newName: "IX_XboxGamertagHistoryHistory_XboxUserID_PeriodEnd");

            migrationBuilder.RenameIndex(
                name: "IX_PastGamertagsHistory_XboxGamertagHistoryID_PeriodEnd",
                schema: "Event",
                table: "XboxGamertagHistoryHistory",
                newName: "IX_XboxGamertagHistoryHistory_XboxGamertagHistoryID_PeriodEnd");

            migrationBuilder.RenameIndex(
                name: "IX_PastGamertags_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "XboxGamertagHistory",
                newName: "IX_XboxGamertagHistory_XboxUserID_NormalizedGamertag");

            migrationBuilder.RenameIndex(
                name: "IX_PastGamertags_NormalizedGamertag",
                schema: "Event",
                table: "XboxGamertagHistory",
                newName: "IX_XboxGamertagHistory_NormalizedGamertag");

            migrationBuilder.AddColumn<string>(
                name: "Division",
                schema: "Event",
                table: "PlayerSeasonStatsHistory",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Division",
                schema: "Event",
                table: "PlayerSeasonStats",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"PlayerSeasonStats\"');");

            migrationBuilder.AddColumn<int>(
                name: "LeagueID",
                schema: "Event",
                table: "PlayerChampionshipsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeagueID",
                schema: "Event",
                table: "PlayerChampionships",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            // Back from the season, which has the league now
            migrationBuilder.Sql("""UPDATE "Event"."PlayerChampionships" AS c SET "LeagueID" = s."LeagueID" FROM "Event"."Seasons" AS s WHERE s."SeasonID" = c."SeasonID";""");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"PlayerChampionships\"');");

            migrationBuilder.AddColumn<string>(
                name: "TeamName",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "TeamName",
                schema: "Event",
                table: "GamertagClaims",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"GamertagClaims\"');");

            migrationBuilder.AddPrimaryKey(
                name: "PK_XboxGamertagHistory",
                schema: "Event",
                table: "XboxGamertagHistory",
                column: "XboxGamertagHistoryID");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerChampionshipsHistory_LeagueID_PeriodEnd",
                schema: "Event",
                table: "PlayerChampionshipsHistory",
                columns: new[] { "LeagueID", "PeriodEnd" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_XboxGamertagHistory_XboxUsers_XboxUserID",
                schema: "Event",
                table: "XboxGamertagHistory",
                column: "XboxUserID",
                principalSchema: "Xbox",
                principalTable: "XboxUsers",
                principalColumn: "XboxUserID",
                onDelete: ReferentialAction.Cascade);
        

            // Data-lossy by necessity: historical seasons go (their stats and titles with them, by cascade), and so do their
            // old versions. SeasonName is 30 long again and unique on its own: a longer name, or one two leagues share, gets
            // its ID ("Name~12") so that it fits and the index can be made. Placeholder users the import made are deleted at
            // the end, once nothing refers to them.
            migrationBuilder.Sql("""
                DELETE FROM "Event"."Seasons" WHERE "IsHistorical";
                UPDATE "Event"."Seasons" AS s SET "SeasonName" = left(s."SeasonName", 29 - length(s."SeasonID"::text)) || '~' || s."SeasonID"::text
                WHERE length(s."SeasonName") > 30
                    OR EXISTS (SELECT 1 FROM "Event"."Seasons" AS o WHERE o."SeasonName" = s."SeasonName" AND o."SeasonID" < s."SeasonID");
                DELETE FROM "Event"."SeasonsHistory" WHERE "IsHistorical";
                UPDATE "Event"."SeasonsHistory" SET "SeasonName" = left("SeasonName", 30) WHERE length("SeasonName") > 30;
                """);
            // The historical seasons' teams went with them, into the history: names longer than 30 are cut there (none of
            // the app's own teams has one), so TeamName can be 30 long again.
            migrationBuilder.Sql("""
                UPDATE "Event"."TeamsHistory" SET "TeamName" = left("TeamName", 30) WHERE length("TeamName") > 30;
                """);

            migrationBuilder.DropColumn(
                name: "RecordedLosses",
                schema: "Event",
                table: "TeamsHistory");

            migrationBuilder.DropColumn(
                name: "RecordedWins",
                schema: "Event",
                table: "TeamsHistory");

            migrationBuilder.DropColumn(
                name: "RecordedLosses",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "RecordedWins",
                schema: "Event",
                table: "Teams");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"Teams\"');");

            migrationBuilder.AlterColumn<string>(
                name: "TeamName",
                schema: "Event",
                table: "TeamsHistory",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldCollation: "case_insensitive");

            migrationBuilder.AlterColumn<string>(
                name: "TeamName",
                schema: "Event",
                table: "Teams",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldCollation: "case_insensitive");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"XboxGamertagHistory\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"PlayerSeasonStats\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"PlayerChampionships\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"Leagues\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"GamertagClaims\"');");

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
                schema: "Event");

            migrationBuilder.DropTable(
                name: "GamertagClaimsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "HistoricalImportJobs",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "LeaguesHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "PlayerChampionships",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "PlayerChampionshipsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "PlayerSeasonStats",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "PlayerSeasonStatsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "XboxGamertagHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "XboxGamertagHistoryHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "Leagues",
                schema: "Event");

            migrationBuilder.DropIndex(
                name: "IX_SeasonsHistory_GameVersionID_PeriodEnd",
                schema: "Event",
                table: "SeasonsHistory");

            migrationBuilder.DropIndex(
                name: "IX_SeasonsHistory_LeagueID_PeriodEnd",
                schema: "Event",
                table: "SeasonsHistory");

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
                table: "SeasonsHistory");

            migrationBuilder.DropColumn(
                name: "GameVersionID",
                schema: "Event",
                table: "SeasonsHistory");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceID",
                schema: "Event",
                table: "SeasonsHistory");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceUrl",
                schema: "Event",
                table: "SeasonsHistory");

            migrationBuilder.DropColumn(
                name: "IsHistorical",
                schema: "Event",
                table: "SeasonsHistory");

            migrationBuilder.DropColumn(
                name: "LeagueID",
                schema: "Event",
                table: "SeasonsHistory");

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

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"Seasons\"');");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "Other",
                table: "GameVersionsHistory");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Other\".\"GameVersions\"');");

            migrationBuilder.AlterColumn<string>(
                name: "SeasonName",
                schema: "Event",
                table: "SeasonsHistory",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldCollation: "case_insensitive");

            migrationBuilder.AlterColumn<string>(
                name: "SeasonName",
                schema: "Event",
                table: "Seasons",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldCollation: "case_insensitive");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_SeasonName",
                schema: "Event",
                table: "Seasons",
                column: "SeasonName",
                unique: true);

            // Placeholder users the historical import made (and nothing refers to now)
            migrationBuilder.Sql("""DELETE FROM "Auth"."Users" WHERE "IsDummyUser" AND "UserName" LIKE 'historical:%';""");
        }
    }
}
