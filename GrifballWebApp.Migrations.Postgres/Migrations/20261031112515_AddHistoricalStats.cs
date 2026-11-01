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

            // The app's own seasons so far are Halo Infinite's (a season now always has its game): given as the column's
            // default, so the rows get it without a new version in their history, then the default is dropped
            migrationBuilder.AddColumn<int>(
                name: "GameVersionID",
                schema: "Event",
                table: "Seasons",
                type: "integer",
                nullable: true,
                defaultValue: 6);

            migrationBuilder.AlterColumn<int>(
                name: "GameVersionID",
                schema: "Event",
                table: "Seasons",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldDefaultValue: 6);

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
                name: "PastGamertags",
                schema: "Xbox",
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
                    table.PrimaryKey("PK_PastGamertags", x => x.XboxGamertagHistoryID);
                    table.ForeignKey(
                        name: "FK_PastGamertags_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PastGamertagsHistory",
                schema: "Xbox",
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

            migrationBuilder.CreateIndex(
                name: "IX_PastGamertagsHistory_XboxGamertagHistoryID_PeriodEnd",
                schema: "Xbox",
                table: "PastGamertagsHistory",
                columns: new[] { "XboxGamertagHistoryID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PastGamertagsHistory_XboxUserID_PeriodEnd",
                schema: "Xbox",
                table: "PastGamertagsHistory",
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

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Xbox\".\"PastGamertags\"');");
            // The renames the XboxUsers table's history already holds: each account's earlier gamertags, from when the first
            // version with it began to when the last one ended ('-infinity', the start of rows history was added to, is unknown).
            // Gamertags are normalised as the app does (HistoricalKeys): trimmed, runs of whitespace made one space, upper-cased
            // (in the "C" collation: regular expressions refuse the case-insensitive one).
            migrationBuilder.Sql("""
                INSERT INTO "Xbox"."PastGamertags" ("XboxUserID", "Gamertag", "NormalizedGamertag", "FirstSeen", "LastSeen", "Source", "CreatedAt", "ModifiedAt")
                SELECT h."XboxUserID", left(max(btrim(regexp_replace(h."Gamertag" COLLATE "C", '\s+', ' ', 'g'))), 50), left(upper(btrim(regexp_replace(h."Gamertag" COLLATE "C", '\s+', ' ', 'g'))), 50),
                    min(NULLIF(h."PeriodStart", '-infinity')), max(h."PeriodEnd"), 1, now(), now()
                FROM "Xbox"."XboxUsersHistory" AS h
                JOIN "Xbox"."XboxUsers" AS x ON x."XboxUserID" = h."XboxUserID"
                WHERE upper(btrim(regexp_replace(h."Gamertag" COLLATE "C", '\s+', ' ', 'g'))) <> upper(btrim(regexp_replace(x."Gamertag" COLLATE "C", '\s+', ' ', 'g')))
                GROUP BY h."XboxUserID", left(upper(btrim(regexp_replace(h."Gamertag" COLLATE "C", '\s+', ' ', 'g'))), 50);
                """);

        

            // Round 3 of the review: championships take their league from the season, claims link their team, stat rows
            // have no division of their own (the gamertag history is created in the Xbox schema as PastGamertags above)
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerChampionships_Leagues_LeagueID",
                schema: "Event",
                table: "PlayerChampionships");


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

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "Xbox",
                table: "PastGamertagsHistory",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "Xbox",
                table: "PastGamertags",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Xbox\".\"PastGamertags\"');");

            migrationBuilder.AddColumn<string>(
                name: "CurrentGamertag",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<DateTime>(
                name: "LookedUpAt",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentGamertag",
                schema: "Event",
                table: "GamertagClaims",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<DateTime>(
                name: "LookedUpAt",
                schema: "Event",
                table: "GamertagClaims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"GamertagClaims\"');");

            migrationBuilder.CreateTable(
                name: "XuidLookups",
                schema: "Xbox",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    LookedUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true),
                    Error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive")
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
                table: "UsersHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegionSource",
                schema: "Auth",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"Users\"');");

            migrationBuilder.AddColumn<int>(
                name: "Format",
                schema: "Event",
                table: "SeasonsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Format",
                schema: "Event",
                table: "Seasons",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"Seasons\"');");

            migrationBuilder.AddColumn<string>(
                name: "ChangedSourceIDs",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "text",
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<bool>(
                name: "Guarded",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "SeasonKeys",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "text",
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotIDs",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "text",
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<int>(
                name: "Trigger",
                schema: "Event",
                table: "HistoricalImportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ClaimantUserID",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstSeen",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "case_insensitive");

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
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstSeen",
                schema: "Event",
                table: "GamertagClaims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                schema: "Event",
                table: "GamertagClaims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "Event",
                table: "GamertagClaims",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"GamertagClaims\"');");

            migrationBuilder.CreateTable(
                name: "DuplicateDismissals",
                schema: "Auth",
                columns: table => new
                {
                    LowUserID = table.Column<int>(type: "integer", nullable: false),
                    HighUserID = table.Column<int>(type: "integer", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DismissedByUserID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateDismissals", x => new { x.LowUserID, x.HighUserID });
                    table.CheckConstraint("CK_Auth_DuplicateDismissals_Order", "\"LowUserID\" < \"HighUserID\"");
                });

            migrationBuilder.CreateTable(
                name: "GameParticipantsHistory",
                schema: "Recorded",
                columns: table => new
                {
                    Assists = table.Column<int>(type: "integer", nullable: true),
                    BallKills = table.Column<int>(type: "integer", nullable: true),
                    Betrayals = table.Column<int>(type: "integer", nullable: true),
                    CarrierKills = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Deaths = table.Column<int>(type: "integer", nullable: true),
                    Gamertag = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    Goals = table.Column<int>(type: "integer", nullable: true),
                    HammerKills = table.Column<int>(type: "integer", nullable: true),
                    Kills = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Punches = table.Column<int>(type: "integer", nullable: true),
                    RecordedGameID = table.Column<int>(type: "integer", nullable: true),
                    RecordedGameParticipantID = table.Column<int>(type: "integer", nullable: true),
                    RecordedGameTeamID = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Suicides = table.Column<int>(type: "integer", nullable: true),
                    SwordKills = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Games",
                schema: "Recorded",
                columns: table => new
                {
                    RecordedGameID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameVersionID = table.Column<int>(type: "integer", nullable: false),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    Map = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    Gametype = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    PlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "case_insensitive"),
                    IsPlayoffs = table.Column<bool>(type: "boolean", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "GamesHistory",
                schema: "Recorded",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    Gametype = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    IsPlayoffs = table.Column<bool>(type: "boolean", nullable: true),
                    Map = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordedGameID = table.Column<int>(type: "integer", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "GameTeamsHistory",
                schema: "Recorded",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedGameID = table.Column<int>(type: "integer", nullable: true),
                    RecordedGameTeamID = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Side = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HistoricalImportSettings",
                schema: "Event",
                columns: table => new
                {
                    HistoricalImportSettingsID = table.Column<int>(type: "integer", nullable: false),
                    SchedulePaused = table.Column<bool>(type: "boolean", nullable: false),
                    LastScheduledRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastScheduledJobID = table.Column<int>(type: "integer", nullable: true),
                    LastScheduledRunNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    HistoricalSeasonReviewID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HistoricalImportJobID = table.Column<int>(type: "integer", nullable: false),
                    SeasonKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "case_insensitive"),
                    SeasonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatsBefore = table.Column<int>(type: "integer", nullable: false),
                    StatsRemoved = table.Column<int>(type: "integer", nullable: false),
                    TeamsBefore = table.Column<int>(type: "integer", nullable: false),
                    TeamsRemoved = table.Column<int>(type: "integer", nullable: false),
                    TitlesBefore = table.Column<int>(type: "integer", nullable: false),
                    TitlesRemoved = table.Column<int>(type: "integer", nullable: false),
                    ApprovalJobID = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalSeasonReviews", x => x.HistoricalSeasonReviewID);
                    table.ForeignKey(
                        name: "FK_HistoricalSeasonReviews_HistoricalImportJobs_HistoricalImpo~",
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
                    HistoricalSourceID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, collation: "case_insensitive"),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, collation: "case_insensitive"),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SheetKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    Gid = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "case_insensitive"),
                    CsvSheetName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    WaybackTimestamp = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true, collation: "case_insensitive"),
                    League = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    GameVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "case_insensitive"),
                    Format = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "case_insensitive"),
                    Config = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDead = table.Column<bool>(type: "boolean", nullable: false),
                    LastFetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFetchStatus = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    LastChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalSources", x => x.HistoricalSourceID);
                });

            migrationBuilder.CreateTable(
                name: "SeriesGamesHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    GameNumber = table.Column<int>(type: "integer", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedGameID = table.Column<int>(type: "integer", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true),
                    SeriesGameID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserMerges",
                schema: "Auth",
                columns: table => new
                {
                    UserMergeID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggeredByUserID = table.Column<int>(type: "integer", nullable: true),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    SourceUserID = table.Column<int>(type: "integer", nullable: false),
                    SourceUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "case_insensitive"),
                    TargetUserID = table.Column<int>(type: "integer", nullable: false),
                    TargetUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "case_insensitive"),
                    Manifest = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    RowsMoved = table.Column<int>(type: "integer", nullable: false),
                    RowsRemoved = table.Column<int>(type: "integer", nullable: false),
                    RowsAdded = table.Column<int>(type: "integer", nullable: false),
                    UndoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UndoneByUserID = table.Column<int>(type: "integer", nullable: true),
                    UndoNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "case_insensitive")
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
                    RecordedGameTeamID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordedGameID = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "SeriesGames",
                schema: "Event",
                columns: table => new
                {
                    SeriesGameID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: false),
                    GameNumber = table.Column<int>(type: "integer", nullable: false),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedGameID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesGames", x => x.SeriesGameID);
                    table.CheckConstraint("CK_Event_SeriesGames_OneGame", "(\"MatchID\" IS NOT NULL AND \"RecordedGameID\" IS NULL) OR (\"MatchID\" IS NULL AND \"RecordedGameID\" IS NOT NULL)");
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
                });

            migrationBuilder.CreateTable(
                name: "HistoricalSourceSnapshots",
                schema: "Event",
                columns: table => new
                {
                    HistoricalSourceSnapshotID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HistoricalSourceID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false, collation: "case_insensitive"),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalSourceSnapshots", x => x.HistoricalSourceSnapshotID);
                    table.ForeignKey(
                        name: "FK_HistoricalSourceSnapshots_HistoricalSources_HistoricalSourc~",
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
                    RecordedGameParticipantID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordedGameID = table.Column<int>(type: "integer", nullable: false),
                    RecordedGameTeamID = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    Gamertag = table.Column<string>(type: "text", nullable: false, collation: "case_insensitive"),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Deaths = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Goals = table.Column<int>(type: "integer", nullable: true),
                    CarrierKills = table.Column<int>(type: "integer", nullable: true),
                    Betrayals = table.Column<int>(type: "integer", nullable: true),
                    Suicides = table.Column<int>(type: "integer", nullable: true),
                    HammerKills = table.Column<int>(type: "integer", nullable: true),
                    SwordKills = table.Column<int>(type: "integer", nullable: true),
                    BallKills = table.Column<int>(type: "integer", nullable: true),
                    Punches = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                });

            migrationBuilder.InsertData(
                schema: "Other",
                table: "Regions",
                columns: new[] { "RegionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "RegionName" },
                values: new object[] { 7, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Europe" });

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaimsHistory_ClaimantUserID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory",
                columns: new[] { "ClaimantUserID", "PeriodEnd" });

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
                filter: "\"Status\" IN (1, 2) AND \"ClaimantUserID\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims",
                columns: new[] { "Kind", "XboxUserID", "NormalizedGamertag" },
                unique: true,
                filter: "\"Status\" IN (1, 2) AND \"ClaimantUserID\" IS NULL");

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
                name: "IX_GameParticipantsHistory_RecordedGameID_PeriodEnd",
                schema: "Recorded",
                table: "GameParticipantsHistory",
                columns: new[] { "RecordedGameID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipantsHistory_RecordedGameParticipantID_PeriodEnd",
                schema: "Recorded",
                table: "GameParticipantsHistory",
                columns: new[] { "RecordedGameParticipantID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipantsHistory_RecordedGameTeamID_PeriodEnd",
                schema: "Recorded",
                table: "GameParticipantsHistory",
                columns: new[] { "RecordedGameTeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipantsHistory_UserID_PeriodEnd",
                schema: "Recorded",
                table: "GameParticipantsHistory",
                columns: new[] { "UserID", "PeriodEnd" });

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
                name: "IX_GamesHistory_GameVersionID_PeriodEnd",
                schema: "Recorded",
                table: "GamesHistory",
                columns: new[] { "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GamesHistory_RecordedGameID_PeriodEnd",
                schema: "Recorded",
                table: "GamesHistory",
                columns: new[] { "RecordedGameID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GamesHistory_SeasonID_PeriodEnd",
                schema: "Recorded",
                table: "GamesHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

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
                name: "IX_GameTeamsHistory_RecordedGameID_PeriodEnd",
                schema: "Recorded",
                table: "GameTeamsHistory",
                columns: new[] { "RecordedGameID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GameTeamsHistory_RecordedGameTeamID_PeriodEnd",
                schema: "Recorded",
                table: "GameTeamsHistory",
                columns: new[] { "RecordedGameTeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GameTeamsHistory_TeamID_PeriodEnd",
                schema: "Recorded",
                table: "GameTeamsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

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
                filter: "\"MatchID\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesGames_RecordedGameID",
                schema: "Event",
                table: "SeriesGames",
                column: "RecordedGameID",
                unique: true,
                filter: "\"RecordedGameID\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesGames_SeasonMatchID_GameNumber",
                schema: "Event",
                table: "SeriesGames",
                columns: new[] { "SeasonMatchID", "GameNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeriesGamesHistory_MatchID_PeriodEnd",
                schema: "Event",
                table: "SeriesGamesHistory",
                columns: new[] { "MatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesGamesHistory_RecordedGameID_PeriodEnd",
                schema: "Event",
                table: "SeriesGamesHistory",
                columns: new[] { "RecordedGameID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesGamesHistory_SeasonMatchID_PeriodEnd",
                schema: "Event",
                table: "SeriesGamesHistory",
                columns: new[] { "SeasonMatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesGamesHistory_SeriesGameID_PeriodEnd",
                schema: "Event",
                table: "SeriesGamesHistory",
                columns: new[] { "SeriesGameID", "PeriodEnd" });

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

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Recorded\".\"GameParticipants\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Recorded\".\"Games\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Recorded\".\"GameTeams\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"SeriesGames\"');");

            migrationBuilder.Sql("INSERT INTO \"Event\".\"SeriesGames\" (\"SeasonMatchID\",\"GameNumber\",\"MatchID\",\"RecordedGameID\",\"CreatedByID\",\"ModifiedByID\",\"CreatedAt\",\"ModifiedAt\") SELECT \"SeasonMatchID\",\"MatchNumber\",\"MatchID\",NULL,\"CreatedByID\",\"ModifiedByID\",\"CreatedAt\",\"ModifiedAt\" FROM \"Event\".\"MatchLinks\";");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"MatchLinks\"');");

            migrationBuilder.DropTable(
                name: "MatchLinks",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchLinksHistory",
                schema: "Event");

            migrationBuilder.CreateTable(
                name: "Vods",
                schema: "Event",
                columns: table => new
                {
                    VodID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YouTubeVideoID = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, collation: "case_insensitive"),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "case_insensitive"),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, collation: "case_insensitive"),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAtIsApproximate = table.Column<bool>(type: "boolean", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ChannelID = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, collation: "case_insensitive"),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true),
                    MatchStatus = table.Column<int>(type: "integer", nullable: false),
                    MatchNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "VodsHistory",
                schema: "Event",
                columns: table => new
                {
                    ChannelID = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true, collation: "case_insensitive"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, collation: "case_insensitive"),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    MatchNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "case_insensitive"),
                    MatchStatus = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAtIsApproximate = table.Column<bool>(type: "boolean", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    VodID = table.Column<int>(type: "integer", nullable: true),
                    YouTubeVideoID = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, collation: "case_insensitive")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "VodTeamsHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    VodID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "VodTeams",
                schema: "Event",
                columns: table => new
                {
                    VodID = table.Column<int>(type: "integer", nullable: false),
                    TeamID = table.Column<int>(type: "integer", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                });

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
                name: "IX_VodsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "VodsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_VodsHistory_SeasonMatchID_PeriodEnd",
                schema: "Event",
                table: "VodsHistory",
                columns: new[] { "SeasonMatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_VodsHistory_VodID_PeriodEnd",
                schema: "Event",
                table: "VodsHistory",
                columns: new[] { "VodID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_VodTeams_TeamID",
                schema: "Event",
                table: "VodTeams",
                column: "TeamID");

            migrationBuilder.CreateIndex(
                name: "IX_VodTeamsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "VodTeamsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_VodTeamsHistory_VodID_TeamID_PeriodEnd",
                schema: "Event",
                table: "VodTeamsHistory",
                columns: new[] { "VodID", "TeamID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"Vods\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"VodTeams\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"VodTeams\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"Vods\"');");

            migrationBuilder.DropTable(
                name: "VodsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "VodTeams",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "VodTeamsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "Vods",
                schema: "Event");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"SeriesGames\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Recorded\".\"GameTeams\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Recorded\".\"Games\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Recorded\".\"GameParticipants\"');");

            migrationBuilder.DropForeignKey(
                name: "FK_GamertagClaims_Users_ClaimantUserID",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropTable(
                name: "DuplicateDismissals",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "GameParticipantsHistory",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "GamesHistory",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "GameTeamsHistory",
                schema: "Recorded");

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
                name: "SeriesGamesHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "UserMerges",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "HistoricalSources",
                schema: "Event");

            migrationBuilder.DropIndex(
                name: "IX_GamertagClaimsHistory_ClaimantUserID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory");

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
                table: "UsersHistory");

            migrationBuilder.DropColumn(
                name: "RegionSource",
                schema: "Auth",
                table: "Users");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"Users\"');");

            migrationBuilder.DropColumn(
                name: "Format",
                schema: "Event",
                table: "SeasonsHistory");

            migrationBuilder.DropColumn(
                name: "Format",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"Seasons\"');");

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
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "FirstSeen",
                schema: "Event",
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "LastSeen",
                schema: "Event",
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                schema: "Event",
                table: "GamertagClaimsHistory");

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

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"GamertagClaims\"');");

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

            migrationBuilder.CreateTable(
                name: "MatchLinks",
                schema: "Event",
                columns: table => new
                {
                    MatchLinkID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchNumber = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()")
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
                });

            migrationBuilder.CreateTable(
                name: "MatchLinksHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchLinkID = table.Column<int>(type: "integer", nullable: true),
                    MatchNumber = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_Kind_XboxUserID_NormalizedGamertag",
                schema: "Event",
                table: "GamertagClaims",
                columns: new[] { "Kind", "XboxUserID", "NormalizedGamertag" },
                unique: true,
                filter: "\"Status\" IN (1, 2)");

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

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinksHistory_MatchID_PeriodEnd",
                schema: "Event",
                table: "MatchLinksHistory",
                columns: new[] { "MatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinksHistory_MatchLinkID_PeriodEnd",
                schema: "Event",
                table: "MatchLinksHistory",
                columns: new[] { "MatchLinkID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinksHistory_SeasonMatchID_PeriodEnd",
                schema: "Event",
                table: "MatchLinksHistory",
                columns: new[] { "SeasonMatchID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"MatchLinks\"');");

            migrationBuilder.Sql("INSERT INTO \"Event\".\"MatchLinks\" (\"MatchID\",\"SeasonMatchID\",\"MatchNumber\",\"CreatedByID\",\"ModifiedByID\",\"CreatedAt\",\"ModifiedAt\") SELECT \"MatchID\",\"SeasonMatchID\",\"GameNumber\",\"CreatedByID\",\"ModifiedByID\",\"CreatedAt\",\"ModifiedAt\" FROM \"Event\".\"SeriesGames\" WHERE \"MatchID\" IS NOT NULL;");

            migrationBuilder.DropTable(
                name: "SeriesGames",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "GameParticipants",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "GameTeams",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "Games",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "XuidLookups",
                schema: "Xbox");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "Xbox",
                table: "PastGamertagsHistory");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "Xbox",
                table: "PastGamertags");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Xbox\".\"PastGamertags\"');");

            migrationBuilder.DropColumn(
                name: "CurrentGamertag",
                schema: "Event",
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "LookedUpAt",
                schema: "Event",
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "CurrentGamertag",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropColumn(
                name: "LookedUpAt",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"GamertagClaims\"');");

            // Round 3 undone first (see the end of Up)
            migrationBuilder.DropForeignKey(
                name: "FK_GamertagClaims_Teams_TeamID",
                schema: "Event",
                table: "GamertagClaims");


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


            migrationBuilder.DropColumn(
                name: "TeamID",
                schema: "Event",
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "TeamID",
                schema: "Event",
                table: "GamertagClaims");








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

        

            // Data-lossy by necessity: historical seasons go (their stats and titles with them, by cascade), and so do their
            // old versions. SeasonName is 30 long again and unique on its own: a longer name, or one two leagues share, gets
            // its ID ("Name~12") so that it fits and the index can be made. Placeholder users the import made are deleted at
            // the end, once nothing refers to them.
            migrationBuilder.Sql("""
                DELETE FROM "Event"."Seasons" WHERE "HistoricalSourceID" IS NOT NULL;
                UPDATE "Event"."Seasons" AS s SET "SeasonName" = left(s."SeasonName", 29 - length(s."SeasonID"::text)) || '~' || s."SeasonID"::text
                WHERE length(s."SeasonName") > 30
                    OR EXISTS (SELECT 1 FROM "Event"."Seasons" AS o WHERE o."SeasonName" = s."SeasonName" AND o."SeasonID" < s."SeasonID");
                DELETE FROM "Event"."SeasonsHistory" WHERE "HistoricalSourceID" IS NOT NULL;
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

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Xbox\".\"PastGamertags\"');");

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
                name: "PastGamertags",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "PastGamertagsHistory",
                schema: "Xbox");

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
