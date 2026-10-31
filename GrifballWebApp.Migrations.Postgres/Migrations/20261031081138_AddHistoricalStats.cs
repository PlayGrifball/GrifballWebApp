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
                name: "PlayerAliases",
                schema: "Event",
                columns: table => new
                {
                    PlayerAliasID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    Alias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    NormalizedAlias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAliases", x => x.PlayerAliasID);
                    table.ForeignKey(
                        name: "FK_PlayerAliases_GameVersions_GameVersionID",
                        column: x => x.GameVersionID,
                        principalSchema: "Other",
                        principalTable: "GameVersions",
                        principalColumn: "GameVesionID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerAliases_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerAliasesHistory",
                schema: "Event",
                columns: table => new
                {
                    Alias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    NormalizedAlias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlayerAliasID = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    UserID = table.Column<int>(type: "integer", nullable: true)
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
                    TeamName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
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
                    TeamName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
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
                    TeamName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
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
                    TeamName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
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
                name: "IX_PlayerAliases_GameVersionID",
                schema: "Event",
                table: "PlayerAliases",
                column: "GameVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAliases_NormalizedAlias",
                schema: "Event",
                table: "PlayerAliases",
                column: "NormalizedAlias");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAliases_UserID_NormalizedAlias",
                schema: "Event",
                table: "PlayerAliases",
                columns: new[] { "UserID", "NormalizedAlias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAliasesHistory_GameVersionID_PeriodEnd",
                schema: "Event",
                table: "PlayerAliasesHistory",
                columns: new[] { "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAliasesHistory_PlayerAliasID_PeriodEnd",
                schema: "Event",
                table: "PlayerAliasesHistory",
                columns: new[] { "PlayerAliasID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAliasesHistory_UserID_PeriodEnd",
                schema: "Event",
                table: "PlayerAliasesHistory",
                columns: new[] { "UserID", "PeriodEnd" });

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
                name: "IX_PlayerSeasonStatsHistory_UserID_PeriodEnd",
                schema: "Event",
                table: "PlayerSeasonStatsHistory",
                columns: new[] { "UserID", "PeriodEnd" });

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

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"Leagues\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"PlayerAliases\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"PlayerChampionships\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"PlayerSeasonStats\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Historical seasons go (their stats and titles with them, by cascade), and so do their old versions, as
            // SeasonName is 30 long again below, in the history table too. A name of the app's own longer than that is cut.
            migrationBuilder.Sql("""
                DELETE FROM "Event"."Seasons" WHERE "IsHistorical";
                UPDATE "Event"."Seasons" SET "SeasonName" = left("SeasonName", 30) WHERE length("SeasonName") > 30;
                DELETE FROM "Event"."SeasonsHistory" WHERE "IsHistorical";
                UPDATE "Event"."SeasonsHistory" SET "SeasonName" = left("SeasonName", 30) WHERE length("SeasonName") > 30;
                """);

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"PlayerSeasonStats\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"PlayerChampionships\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"PlayerAliases\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"Leagues\"');");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_GameVersions_GameVersionID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Leagues_LeagueID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropTable(
                name: "LeaguesHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "PlayerAliases",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "PlayerAliasesHistory",
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
        }
    }
}
