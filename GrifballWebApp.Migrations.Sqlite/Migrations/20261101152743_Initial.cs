using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Event");

            migrationBuilder.EnsureSchema(
                name: "Auth");

            migrationBuilder.EnsureSchema(
                name: "User");

            migrationBuilder.EnsureSchema(
                name: "Recorded");

            migrationBuilder.EnsureSchema(
                name: "Other");

            migrationBuilder.EnsureSchema(
                name: "Matchmaking");

            migrationBuilder.EnsureSchema(
                name: "Infinite");

            migrationBuilder.EnsureSchema(
                name: "Discord");

            migrationBuilder.EnsureSchema(
                name: "Xbox");

            migrationBuilder.CreateTable(
                name: "AvailabilityOptions",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DayOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    Time = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityOptions", x => x.AvailabilityOptionID);
                });

            migrationBuilder.CreateTable(
                name: "AvailabilityOptionsHistory",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    DayOfWeek = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Time = table.Column<TimeOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FriendlyName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Xml = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeysHistory",
                schema: "Auth",
                columns: table => new
                {
                    FriendlyName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Id = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Xml = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Discord",
                schema: "User",
                columns: table => new
                {
                    DiscordUserID = table.Column<long>(type: "INTEGER", nullable: false),
                    DiscordUsername = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discord", x => x.DiscordUserID);
                });

            migrationBuilder.CreateTable(
                name: "DiscordHistory",
                schema: "User",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    DiscordUserID = table.Column<long>(type: "INTEGER", nullable: true),
                    DiscordUsername = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "DuplicateDismissals",
                schema: "Auth",
                columns: table => new
                {
                    LowUserID = table.Column<int>(type: "INTEGER", nullable: false),
                    HighUserID = table.Column<int>(type: "INTEGER", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DismissedByUserID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateDismissals", x => new { x.LowUserID, x.HighUserID });
                    table.CheckConstraint("CK_Auth_DuplicateDismissals_Order", "LowUserID < HighUserID");
                });

            migrationBuilder.CreateTable(
                name: "GameParticipantsHistory",
                schema: "Recorded",
                columns: table => new
                {
                    Assists = table.Column<int>(type: "INTEGER", nullable: true),
                    BallKills = table.Column<int>(type: "INTEGER", nullable: true),
                    Betrayals = table.Column<int>(type: "INTEGER", nullable: true),
                    CarrierKills = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: true),
                    Gamertag = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Goals = table.Column<int>(type: "INTEGER", nullable: true),
                    HammerKills = table.Column<int>(type: "INTEGER", nullable: true),
                    Kills = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Punches = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedGameID = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedGameParticipantID = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedGameTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    Suicides = table.Column<int>(type: "INTEGER", nullable: true),
                    SwordKills = table.Column<int>(type: "INTEGER", nullable: true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "GamertagClaimsHistory",
                schema: "Event",
                columns: table => new
                {
                    ClaimantUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentGamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Gamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    GamertagClaimID = table.Column<int>(type: "INTEGER", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LookedUpAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    NormalizedGamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlaceholderUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "GamesHistory",
                schema: "Recorded",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: true),
                    Gametype = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    IsPlayoffs = table.Column<bool>(type: "INTEGER", nullable: true),
                    Map = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RecordedGameID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "GameTeamsHistory",
                schema: "Recorded",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecordedGameID = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedGameTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    Side = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "GameVersions",
                schema: "Other",
                columns: table => new
                {
                    GameVesionID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameVersionName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameVersions", x => x.GameVesionID);
                });

            migrationBuilder.CreateTable(
                name: "GameVersionsHistory",
                schema: "Other",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersionName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    GameVesionID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HistoricalImportJobs",
                schema: "Event",
                columns: table => new
                {
                    HistoricalImportJobID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    SeasonsDone = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonsTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    Report = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Error = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HeartbeatAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    Trigger = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    Guarded = table.Column<bool>(type: "INTEGER", nullable: false),
                    SnapshotIDs = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    SeasonKeys = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ChangedSourceIDs = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalImportJobs", x => x.HistoricalImportJobID);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalImportSettings",
                schema: "Event",
                columns: table => new
                {
                    HistoricalImportSettingsID = table.Column<int>(type: "INTEGER", nullable: false),
                    SchedulePaused = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastScheduledRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastScheduledJobID = table.Column<int>(type: "INTEGER", nullable: true),
                    LastScheduledRunNote = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalImportSettings", x => x.HistoricalImportSettingsID);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalSources",
                schema: "Event",
                columns: table => new
                {
                    HistoricalSourceID = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false, collation: "NOCASE"),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false, collation: "NOCASE"),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    SheetKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    Gid = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    CsvSheetName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    WaybackTimestamp = table.Column<string>(type: "TEXT", maxLength: 14, nullable: true, collation: "NOCASE"),
                    League = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    GameVersion = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    Format = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    Config = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDead = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastFetchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastFetchStatus = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    LastChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalSources", x => x.HistoricalSourceID);
                });

            migrationBuilder.CreateTable(
                name: "Leagues",
                schema: "Event",
                columns: table => new
                {
                    LeagueID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LeagueName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    LeagueID = table.Column<int>(type: "INTEGER", nullable: true),
                    LeagueName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchBracketInfoHistory",
                schema: "Event",
                columns: table => new
                {
                    AwayTeamPreviousMatchBracketInfoID = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayTeamSeedNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Bracket = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeTeamPreviousMatchBracketInfoID = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeTeamSeedNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchBracketInfoID = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedKickVotesHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    KickMatchedPlayerId = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VoterMatchedPlayerId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedMatchesHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    Active = table.Column<bool>(type: "INTEGER", nullable: true),
                    AwayTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    Id = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ThreadID = table.Column<ulong>(type: "INTEGER", nullable: true),
                    VoteMessageID = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedPlayersHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Kicked = table.Column<bool>(type: "INTEGER", nullable: true),
                    MatchedTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedTeams",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchedTeamId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedTeams", x => x.MatchedTeamId);
                });

            migrationBuilder.CreateTable(
                name: "MatchedTeamsHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchedTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedWinnerVotesHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchedPlayerId = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WinnerVote = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                schema: "Infinite",
                columns: table => new
                {
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Duration = table.Column<long>(type: "INTEGER", nullable: false),
                    StatsPullDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.MatchID);
                });

            migrationBuilder.CreateTable(
                name: "MatchesHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Duration = table.Column<long>(type: "INTEGER", nullable: true),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StatsPullDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchParticipantsHistory",
                schema: "Infinite",
                columns: table => new
                {
                    Accuracy = table.Column<float>(type: "REAL", nullable: true),
                    Assists = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageLife = table.Column<long>(type: "INTEGER", nullable: true),
                    Betrayals = table.Column<int>(type: "INTEGER", nullable: true),
                    CalloutAssists = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    DamageDealt = table.Column<int>(type: "INTEGER", nullable: true),
                    DamageTaken = table.Column<int>(type: "INTEGER", nullable: true),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: true),
                    FirstJoinedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    JoinedInProgress = table.Column<bool>(type: "INTEGER", nullable: true),
                    Kda = table.Column<float>(type: "REAL", nullable: true),
                    Kills = table.Column<int>(type: "INTEGER", nullable: true),
                    LastLeaveTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LeftInProgress = table.Column<bool>(type: "INTEGER", nullable: true),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: true),
                    MaxKillingSpree = table.Column<int>(type: "INTEGER", nullable: true),
                    MeleeKills = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ObjectivesCompleted = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PersonalScore = table.Column<int>(type: "INTEGER", nullable: true),
                    PowerWeaponKills = table.Column<int>(type: "INTEGER", nullable: true),
                    PresentAtBeginning = table.Column<bool>(type: "INTEGER", nullable: true),
                    PresentAtCompletion = table.Column<bool>(type: "INTEGER", nullable: true),
                    Rank = table.Column<int>(type: "INTEGER", nullable: true),
                    RoundsLost = table.Column<int>(type: "INTEGER", nullable: true),
                    RoundsTied = table.Column<int>(type: "INTEGER", nullable: true),
                    RoundsWon = table.Column<int>(type: "INTEGER", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    ShotsFired = table.Column<int>(type: "INTEGER", nullable: true),
                    ShotsHit = table.Column<int>(type: "INTEGER", nullable: true),
                    Spawns = table.Column<int>(type: "INTEGER", nullable: true),
                    Suicides = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    TimePlayed = table.Column<long>(type: "INTEGER", nullable: true),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchTeamsHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MedalDifficulties",
                schema: "Infinite",
                columns: table => new
                {
                    MedalDifficultyID = table.Column<int>(type: "INTEGER", nullable: false),
                    MedalDifficultyName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalDifficulties", x => x.MedalDifficultyID);
                });

            migrationBuilder.CreateTable(
                name: "MedalDifficultiesHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    MedalDifficultyID = table.Column<int>(type: "INTEGER", nullable: true),
                    MedalDifficultyName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MedalEarnedHistory",
                schema: "Infinite",
                columns: table => new
                {
                    Count = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: true),
                    MedalID = table.Column<long>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalPersonalScoreAwarded = table.Column<int>(type: "INTEGER", nullable: true),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MedalsHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    MedalDifficultyID = table.Column<int>(type: "INTEGER", nullable: true),
                    MedalID = table.Column<long>(type: "INTEGER", nullable: true),
                    MedalName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    MedalTypeID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PersonalScore = table.Column<int>(type: "INTEGER", nullable: true),
                    SortingWeight = table.Column<int>(type: "INTEGER", nullable: true),
                    SpriteIndex = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MedalTypes",
                schema: "Infinite",
                columns: table => new
                {
                    MedalTypeID = table.Column<int>(type: "INTEGER", nullable: false),
                    MedalTypeName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalTypes", x => x.MedalTypeID);
                });

            migrationBuilder.CreateTable(
                name: "MedalTypesHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    MedalTypeID = table.Column<int>(type: "INTEGER", nullable: true),
                    MedalTypeName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MessagesHistory",
                schema: "Discord",
                columns: table => new
                {
                    Content = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    FromDiscordUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    Id = table.Column<long>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ToDiscordUserId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "Discord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Payload = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    GroupKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Result = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetLinksHistory",
                schema: "Auth",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Id = table.Column<int>(type: "INTEGER", nullable: true),
                    IsUsed = table.Column<bool>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true, collation: "NOCASE"),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PastGamertagsHistory",
                schema: "Xbox",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Gamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    NormalizedGamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: true),
                    XboxGamertagHistoryID = table.Column<int>(type: "INTEGER", nullable: true),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PlayerChampionshipsHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: true),
                    GamertagUsed = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    IsCaptain = table.Column<bool>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlayerChampionshipID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    Tier = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PlayerSeasonStatsHistory",
                schema: "Event",
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
                    IsCombined = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsPlayoffs = table.Column<bool>(type: "INTEGER", nullable: true),
                    KdRatio = table.Column<double>(type: "decimal(6, 3)", nullable: true),
                    KdSpread = table.Column<int>(type: "INTEGER", nullable: true),
                    Kills = table.Column<int>(type: "INTEGER", nullable: true),
                    Losses = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    MultiKills = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlayerSeasonStatID = table.Column<int>(type: "INTEGER", nullable: true),
                    Punches = table.Column<int>(type: "INTEGER", nullable: true),
                    RoleNote = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    Sprees = table.Column<int>(type: "INTEGER", nullable: true),
                    StintIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    SwordKills = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalSecondsPlayed = table.Column<int>(type: "INTEGER", nullable: true),
                    TradeNote = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true),
                    WinPercent = table.Column<double>(type: "decimal(6, 3)", nullable: true),
                    Wins = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "QueuedPlayersHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Ranks",
                schema: "Matchmaking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    MmrThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Icon = table.Column<byte[]>(type: "BLOB", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ranks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RanksHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    Color = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Icon = table.Column<byte[]>(type: "BLOB", nullable: true),
                    Id = table.Column<int>(type: "INTEGER", nullable: true),
                    MmrThreshold = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                schema: "Other",
                columns: table => new
                {
                    RegionID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegionName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.RegionID);
                });

            migrationBuilder.CreateTable(
                name: "RegionsHistory",
                schema: "Other",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RegionID = table.Column<int>(type: "INTEGER", nullable: true),
                    RegionName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "RoleClaimsHistory",
                schema: "Auth",
                columns: table => new
                {
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Id = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolesHistory",
                schema: "Auth",
                columns: table => new
                {
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Id = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SeasonAvailabilityHistory",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SeasonMatchesHistory",
                schema: "Event",
                columns: table => new
                {
                    ActiveRescheduleRequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayTeamResult = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayTeamScore = table.Column<int>(type: "INTEGER", nullable: true),
                    BestOf = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeTeamResult = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeTeamScore = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScheduledTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SeasonsHistory",
                schema: "Event",
                columns: table => new
                {
                    CaptainsLocked = table.Column<bool>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Division = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    DraftStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Format = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: true),
                    HistoricalSourceID = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    HistoricalSourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE"),
                    LeagueID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublicAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SeasonEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    SeasonStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignupsClose = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignupsOpen = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SeasonSignupsHistory",
                schema: "Event",
                columns: table => new
                {
                    Approved = table.Column<bool>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RequiresAssistanceDrafting = table.Column<bool>(type: "INTEGER", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonSignupID = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true),
                    WillCaptain = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SeriesGamesHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    GameNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecordedGameID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeriesGameID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SignupAvailabilityHistory",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonSignupID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "TeamAvailabilityHistory",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "TeamPlayersHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    DraftCaptainOrder = table.Column<int>(type: "INTEGER", nullable: true),
                    DraftPick = table.Column<int>(type: "INTEGER", nullable: true),
                    DraftRound = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamPlayerID = table.Column<int>(type: "INTEGER", nullable: true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "TeamsHistory",
                schema: "Event",
                columns: table => new
                {
                    CaptainID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecordedLosses = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedWins = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserClaimsHistory",
                schema: "Auth",
                columns: table => new
                {
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Id = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserExperiencesHistory",
                schema: "Other",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserLoginsHistory",
                schema: "Auth",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserMerges",
                schema: "Auth",
                columns: table => new
                {
                    UserMergeID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MergedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TriggeredByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    Trigger = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    SourceUserID = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    TargetUserID = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    Manifest = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    RowsMoved = table.Column<int>(type: "INTEGER", nullable: false),
                    RowsRemoved = table.Column<int>(type: "INTEGER", nullable: false),
                    RowsAdded = table.Column<int>(type: "INTEGER", nullable: false),
                    UndoneAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UndoneByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    UndoNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMerges", x => x.UserMergeID);
                });

            migrationBuilder.CreateTable(
                name: "UserRolesHistory",
                schema: "Auth",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UsersHistory",
                schema: "Auth",
                columns: table => new
                {
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    DiscordUserID = table.Column<long>(type: "INTEGER", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: true),
                    Id = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDummyUser = table.Column<bool>(type: "INTEGER", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LossStreak = table.Column<int>(type: "INTEGER", nullable: true),
                    Losses = table.Column<int>(type: "INTEGER", nullable: true),
                    MMR = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: true),
                    RegionID = table.Column<int>(type: "INTEGER", nullable: true),
                    RegionSource = table.Column<int>(type: "INTEGER", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    WinStreak = table.Column<int>(type: "INTEGER", nullable: true),
                    Wins = table.Column<int>(type: "INTEGER", nullable: true),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserTokensHistory",
                schema: "Auth",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Name = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Value = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "VodsHistory",
                schema: "Event",
                columns: table => new
                {
                    ChannelID = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true, collation: "NOCASE"),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE"),
                    MatchStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PublishedAtIsApproximate = table.Column<bool>(type: "INTEGER", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    VodID = table.Column<int>(type: "INTEGER", nullable: true),
                    YouTubeVideoID = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "VodTeamsHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    VodID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "XboxUsers",
                schema: "Xbox",
                columns: table => new
                {
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: false),
                    Gamertag = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XboxUsers", x => x.XboxUserID);
                });

            migrationBuilder.CreateTable(
                name: "XboxUsersHistory",
                schema: "Xbox",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Gamertag = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "Discord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    FromDiscordUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    ToDiscordUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Discord_FromDiscordUserId",
                        column: x => x.FromDiscordUserId,
                        principalSchema: "User",
                        principalTable: "Discord",
                        principalColumn: "DiscordUserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Discord_ToDiscordUserId",
                        column: x => x.ToDiscordUserId,
                        principalSchema: "User",
                        principalTable: "Discord",
                        principalColumn: "DiscordUserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalSeasonReviews",
                schema: "Event",
                columns: table => new
                {
                    HistoricalSeasonReviewID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HistoricalImportJobID = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    SeasonName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StatsBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    StatsRemoved = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamsBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamsRemoved = table.Column<int>(type: "INTEGER", nullable: false),
                    TitlesBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    TitlesRemoved = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalJobID = table.Column<int>(type: "INTEGER", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "HistoricalSourceSnapshots",
                schema: "Event",
                columns: table => new
                {
                    HistoricalSourceSnapshotID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HistoricalSourceID = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false, collation: "NOCASE"),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false)
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
                name: "Seasons",
                schema: "Event",
                columns: table => new
                {
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    CaptainsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublicAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignupsOpen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SignupsClose = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DraftStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: true),
                    LeagueID = table.Column<int>(type: "INTEGER", nullable: false),
                    Division = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    HistoricalSourceID = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    HistoricalSourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE"),
                    Format = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.SeasonID);
                    table.ForeignKey(
                        name: "FK_Seasons_GameVersions_GameVersionID",
                        column: x => x.GameVersionID,
                        principalSchema: "Other",
                        principalTable: "GameVersions",
                        principalColumn: "GameVesionID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Seasons_Leagues_LeagueID",
                        column: x => x.LeagueID,
                        principalSchema: "Event",
                        principalTable: "Leagues",
                        principalColumn: "LeagueID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchedMatches",
                schema: "Matchmaking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HomeTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    AwayTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: true),
                    ThreadID = table.Column<ulong>(type: "INTEGER", nullable: true),
                    VoteMessageID = table.Column<ulong>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchedMatches_MatchedTeams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchedTeams",
                        principalColumn: "MatchedTeamId");
                    table.ForeignKey(
                        name: "FK_MatchedMatches_MatchedTeams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchedTeams",
                        principalColumn: "MatchedTeamId");
                    table.ForeignKey(
                        name: "FK_MatchedMatches_Matches_MatchID",
                        column: x => x.MatchID,
                        principalSchema: "Infinite",
                        principalTable: "Matches",
                        principalColumn: "MatchID");
                });

            migrationBuilder.CreateTable(
                name: "MatchTeams",
                schema: "Infinite",
                columns: table => new
                {
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchTeams", x => new { x.MatchID, x.TeamID });
                    table.ForeignKey(
                        name: "FK_MatchTeams_Matches_MatchID",
                        column: x => x.MatchID,
                        principalSchema: "Infinite",
                        principalTable: "Matches",
                        principalColumn: "MatchID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Medals",
                schema: "Infinite",
                columns: table => new
                {
                    MedalID = table.Column<long>(type: "INTEGER", nullable: false),
                    MedalName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    Description = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    SpriteIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SortingWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    MedalDifficultyID = table.Column<int>(type: "INTEGER", nullable: false),
                    MedalTypeID = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonalScore = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medals", x => x.MedalID);
                    table.ForeignKey(
                        name: "FK_Medals_MedalDifficulties_MedalDifficultyID",
                        column: x => x.MedalDifficultyID,
                        principalSchema: "Infinite",
                        principalTable: "MedalDifficulties",
                        principalColumn: "MedalDifficultyID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medals_MedalTypes_MedalTypeID",
                        column: x => x.MedalTypeID,
                        principalSchema: "Infinite",
                        principalTable: "MedalTypes",
                        principalColumn: "MedalTypeID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "Auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PastGamertags",
                schema: "Xbox",
                columns: table => new
                {
                    XboxGamertagHistoryID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: false),
                    Gamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    NormalizedGamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "Users",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegionID = table.Column<int>(type: "INTEGER", nullable: true),
                    RegionSource = table.Column<int>(type: "INTEGER", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    IsDummyUser = table.Column<bool>(type: "INTEGER", nullable: false),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true),
                    DiscordUserID = table.Column<long>(type: "INTEGER", nullable: true),
                    MMR = table.Column<int>(type: "INTEGER", nullable: false),
                    WinStreak = table.Column<int>(type: "INTEGER", nullable: false),
                    LossStreak = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Discord_DiscordUserID",
                        column: x => x.DiscordUserID,
                        principalSchema: "User",
                        principalTable: "Discord",
                        principalColumn: "DiscordUserID");
                    table.ForeignKey(
                        name: "FK_Users_Regions_RegionID",
                        column: x => x.RegionID,
                        principalSchema: "Other",
                        principalTable: "Regions",
                        principalColumn: "RegionID");
                    table.ForeignKey(
                        name: "FK_Users_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID");
                });

            migrationBuilder.CreateTable(
                name: "Games",
                schema: "Recorded",
                columns: table => new
                {
                    RecordedGameID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    Map = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    Gametype = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    PlayedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    IsPlayoffs = table.Column<bool>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "SeasonAvailability",
                schema: "Event",
                columns: table => new
                {
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailabilityOptionID = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonAvailability", x => new { x.SeasonID, x.AvailabilityOptionID });
                    table.ForeignKey(
                        name: "FK_SeasonAvailability_AvailabilityOptions_AvailabilityOptionID",
                        column: x => x.AvailabilityOptionID,
                        principalSchema: "Event",
                        principalTable: "AvailabilityOptions",
                        principalColumn: "AvailabilityOptionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonAvailability_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchParticipants",
                schema: "Infinite",
                columns: table => new
                {
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: false),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonalScore = table.Column<int>(type: "INTEGER", nullable: false),
                    Kills = table.Column<int>(type: "INTEGER", nullable: false),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    Kda = table.Column<float>(type: "REAL", nullable: false),
                    Suicides = table.Column<int>(type: "INTEGER", nullable: false),
                    Betrayals = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageLife = table.Column<long>(type: "INTEGER", nullable: false),
                    MeleeKills = table.Column<int>(type: "INTEGER", nullable: false),
                    PowerWeaponKills = table.Column<int>(type: "INTEGER", nullable: false),
                    ShotsFired = table.Column<int>(type: "INTEGER", nullable: false),
                    ShotsHit = table.Column<int>(type: "INTEGER", nullable: false),
                    Accuracy = table.Column<float>(type: "REAL", nullable: false),
                    DamageDealt = table.Column<int>(type: "INTEGER", nullable: false),
                    CalloutAssists = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxKillingSpree = table.Column<int>(type: "INTEGER", nullable: false),
                    DamageTaken = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstJoinedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLeaveTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    JoinedInProgress = table.Column<bool>(type: "INTEGER", nullable: false),
                    LeftInProgress = table.Column<bool>(type: "INTEGER", nullable: false),
                    PresentAtBeginning = table.Column<bool>(type: "INTEGER", nullable: false),
                    PresentAtCompletion = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimePlayed = table.Column<long>(type: "INTEGER", nullable: false),
                    RoundsWon = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundsLost = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundsTied = table.Column<int>(type: "INTEGER", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    Spawns = table.Column<int>(type: "INTEGER", nullable: false),
                    ObjectivesCompleted = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchParticipants", x => new { x.MatchID, x.XboxUserID });
                    table.ForeignKey(
                        name: "FK_MatchParticipants_MatchTeams_MatchID_TeamID",
                        columns: x => new { x.MatchID, x.TeamID },
                        principalSchema: "Infinite",
                        principalTable: "MatchTeams",
                        principalColumns: new[] { "MatchID", "TeamID" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchParticipants_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchedPlayers",
                schema: "Matchmaking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    Kicked = table.Column<bool>(type: "INTEGER", nullable: false),
                    MatchedTeamID = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchedPlayers_MatchedTeams_MatchedTeamID",
                        column: x => x.MatchedTeamID,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchedTeams",
                        principalColumn: "MatchedTeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchedPlayers_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetLinks",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false, collation: "NOCASE"),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsUsed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetLinks_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QueuedPlayers",
                schema: "Matchmaking",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedPlayers", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_QueuedPlayers_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SeasonSignups",
                schema: "Event",
                columns: table => new
                {
                    SeasonSignupID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    WillCaptain = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresAssistanceDrafting = table.Column<bool>(type: "INTEGER", nullable: false),
                    Approved = table.Column<bool>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonSignups", x => x.SeasonSignupID);
                    table.ForeignKey(
                        name: "FK_SeasonSignups_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonSignups_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserExperiences",
                schema: "Other",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExperiences", x => new { x.UserID, x.GameVersionID });
                    table.ForeignKey(
                        name: "FK_UserExperiences_GameVersions_GameVersionID",
                        column: x => x.GameVersionID,
                        principalSchema: "Other",
                        principalTable: "GameVersions",
                        principalColumn: "GameVesionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserExperiences_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "Auth",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "Auth",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "Auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "Auth",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Value = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XuidLookups",
                schema: "Xbox",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    LookedUpAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE")
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

            migrationBuilder.CreateTable(
                name: "MedalEarned",
                schema: "Infinite",
                columns: table => new
                {
                    MedalID = table.Column<long>(type: "INTEGER", nullable: false),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: false),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPersonalScoreAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalEarned", x => new { x.MedalID, x.MatchID, x.XboxUserID });
                    table.ForeignKey(
                        name: "FK_MedalEarned_MatchParticipants_MatchID_XboxUserID",
                        columns: x => new { x.MatchID, x.XboxUserID },
                        principalSchema: "Infinite",
                        principalTable: "MatchParticipants",
                        principalColumns: new[] { "MatchID", "XboxUserID" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MedalEarned_Medals_MedalID",
                        column: x => x.MedalID,
                        principalSchema: "Infinite",
                        principalTable: "Medals",
                        principalColumn: "MedalID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchedKickVotes",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    VoterMatchedPlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    KickMatchedPlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedKickVotes", x => new { x.MatchId, x.VoterMatchedPlayerId });
                    table.ForeignKey(
                        name: "FK_MatchedKickVotes_MatchedMatches_MatchId",
                        column: x => x.MatchId,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchedMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchedKickVotes_MatchedPlayers_KickMatchedPlayerId",
                        column: x => x.KickMatchedPlayerId,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchedPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchedKickVotes_MatchedPlayers_VoterMatchedPlayerId",
                        column: x => x.VoterMatchedPlayerId,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchedPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchedWinnerVotes",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedPlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    WinnerVote = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedWinnerVotes", x => new { x.MatchId, x.MatchedPlayerId });
                    table.ForeignKey(
                        name: "FK_MatchedWinnerVotes_MatchedMatches_MatchId",
                        column: x => x.MatchId,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchedMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchedWinnerVotes_MatchedPlayers_MatchedPlayerId",
                        column: x => x.MatchedPlayerId,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchedPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignupAvailability",
                schema: "Event",
                columns: table => new
                {
                    SeasonSignupID = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailabilityOptionID = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignupAvailability", x => new { x.SeasonSignupID, x.AvailabilityOptionID });
                    table.ForeignKey(
                        name: "FK_SignupAvailability_AvailabilityOptions_AvailabilityOptionID",
                        column: x => x.AvailabilityOptionID,
                        principalSchema: "Event",
                        principalTable: "AvailabilityOptions",
                        principalColumn: "AvailabilityOptionID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignupAvailability_SeasonSignups_SeasonSignupID",
                        column: x => x.SeasonSignupID,
                        principalSchema: "Event",
                        principalTable: "SeasonSignups",
                        principalColumn: "SeasonSignupID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameParticipants",
                schema: "Recorded",
                columns: table => new
                {
                    RecordedGameParticipantID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecordedGameID = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedGameTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    Gamertag = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Kills = table.Column<int>(type: "INTEGER", nullable: false),
                    Deaths = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    Goals = table.Column<int>(type: "INTEGER", nullable: true),
                    CarrierKills = table.Column<int>(type: "INTEGER", nullable: true),
                    Betrayals = table.Column<int>(type: "INTEGER", nullable: true),
                    Suicides = table.Column<int>(type: "INTEGER", nullable: true),
                    HammerKills = table.Column<int>(type: "INTEGER", nullable: true),
                    SwordKills = table.Column<int>(type: "INTEGER", nullable: true),
                    BallKills = table.Column<int>(type: "INTEGER", nullable: true),
                    Punches = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameParticipants", x => x.RecordedGameParticipantID);
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

            migrationBuilder.CreateTable(
                name: "GamertagClaims",
                schema: "Event",
                columns: table => new
                {
                    GamertagClaimID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true),
                    ClaimantUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    Gamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    NormalizedGamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    PlaceholderUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrentGamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    LookedUpAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                        name: "FK_GamertagClaims_Users_ClaimantUserID",
                        column: x => x.ClaimantUserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id");
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
                name: "GameTeams",
                schema: "Recorded",
                columns: table => new
                {
                    RecordedGameTeamID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecordedGameID = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "MatchBracketInfo",
                schema: "Event",
                columns: table => new
                {
                    MatchBracketInfoID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeTeamSeedNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeTeamPreviousMatchBracketInfoID = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayTeamSeedNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayTeamPreviousMatchBracketInfoID = table.Column<int>(type: "INTEGER", nullable: true),
                    Bracket = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchBracketInfo", x => x.MatchBracketInfoID);
                    table.CheckConstraint("CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch", "(AwayTeamSeedNumber IS NOT NULL AND AwayTeamPreviousMatchBracketInfoID IS NULL) OR (AwayTeamPreviousMatchBracketInfoID IS NOT NULL AND AwayTeamSeedNumber IS NULL)");
                    table.CheckConstraint("CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch", "(HomeTeamSeedNumber IS NOT NULL AND HomeTeamPreviousMatchBracketInfoID IS NULL) OR (HomeTeamPreviousMatchBracketInfoID IS NOT NULL AND HomeTeamSeedNumber IS NULL)");
                    table.ForeignKey(
                        name: "FK_MatchBracketInfo_MatchBracketInfo_AwayTeamPreviousMatchBracketInfoID",
                        column: x => x.AwayTeamPreviousMatchBracketInfoID,
                        principalSchema: "Event",
                        principalTable: "MatchBracketInfo",
                        principalColumn: "MatchBracketInfoID");
                    table.ForeignKey(
                        name: "FK_MatchBracketInfo_MatchBracketInfo_HomeTeamPreviousMatchBracketInfoID",
                        column: x => x.HomeTeamPreviousMatchBracketInfoID,
                        principalSchema: "Event",
                        principalTable: "MatchBracketInfo",
                        principalColumn: "MatchBracketInfoID");
                });

            migrationBuilder.CreateTable(
                name: "MatchReschedules",
                columns: table => new
                {
                    MatchRescheduleID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalScheduledTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NewScheduledTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false, collation: "NOCASE"),
                    RequestedByUserID = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovedByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DiscordThreadID = table.Column<ulong>(type: "INTEGER", nullable: true),
                    CommissionerNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true, collation: "NOCASE"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchReschedules", x => x.MatchRescheduleID);
                    table.ForeignKey(
                        name: "FK_MatchReschedules_Users_ApprovedByUserID",
                        column: x => x.ApprovedByUserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchReschedules_Users_RequestedByUserID",
                        column: x => x.RequestedByUserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerChampionships",
                schema: "Event",
                columns: table => new
                {
                    PlayerChampionshipID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: true),
                    Tier = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCaptain = table.Column<bool>(type: "INTEGER", nullable: false),
                    GamertagUsed = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "PlayerSeasonStats",
                schema: "Event",
                columns: table => new
                {
                    PlayerSeasonStatID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    GameVersionID = table.Column<int>(type: "INTEGER", nullable: false),
                    Gamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    TradeNote = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    RoleNote = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    StintIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPlayoffs = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCombined = table.Column<bool>(type: "INTEGER", nullable: false),
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
                    ExtendedMetricsJson = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "SeasonMatches",
                schema: "Event",
                columns: table => new
                {
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    HomeTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeTeamScore = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeTeamResult = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayTeamID = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayTeamScore = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayTeamResult = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduledTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BestOf = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveRescheduleRequestId = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonMatches", x => x.SeasonMatchID);
                    table.CheckConstraint("CK_Event_SeasonMatches_MustBeDifferentTeams", "(HomeTeamID IS NULL) OR (AwayTeamID IS NULL) OR (HomeTeamID != AwayTeamID)");
                    table.ForeignKey(
                        name: "FK_SeasonMatches_MatchReschedules_ActiveRescheduleRequestId",
                        column: x => x.ActiveRescheduleRequestId,
                        principalTable: "MatchReschedules",
                        principalColumn: "MatchRescheduleID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeasonMatches_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeriesGames",
                schema: "Event",
                columns: table => new
                {
                    SeriesGameID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: false),
                    GameNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecordedGameID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Vods",
                schema: "Event",
                columns: table => new
                {
                    VodID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YouTubeVideoID = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, collation: "NOCASE"),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true, collation: "NOCASE"),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedAtIsApproximate = table.Column<bool>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    ChannelID = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false, collation: "NOCASE"),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE"),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "TeamAvailability",
                schema: "Event",
                columns: table => new
                {
                    TeamID = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailabilityOptionID = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamAvailability", x => new { x.TeamID, x.AvailabilityOptionID });
                    table.ForeignKey(
                        name: "FK_TeamAvailability_AvailabilityOptions_AvailabilityOptionID",
                        column: x => x.AvailabilityOptionID,
                        principalSchema: "Event",
                        principalTable: "AvailabilityOptions",
                        principalColumn: "AvailabilityOptionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamPlayers",
                schema: "Event",
                columns: table => new
                {
                    TeamPlayerID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: false),
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    DraftCaptainOrder = table.Column<int>(type: "INTEGER", nullable: true),
                    DraftRound = table.Column<int>(type: "INTEGER", nullable: true),
                    DraftPick = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamPlayers", x => x.TeamPlayerID);
                    table.ForeignKey(
                        name: "FK_TeamPlayers_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                schema: "Event",
                columns: table => new
                {
                    TeamID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    CaptainID = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedWins = table.Column<int>(type: "INTEGER", nullable: true),
                    RecordedLosses = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.TeamID);
                    table.ForeignKey(
                        name: "FK_Teams_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Teams_TeamPlayers_CaptainID",
                        column: x => x.CaptainID,
                        principalSchema: "Event",
                        principalTable: "TeamPlayers",
                        principalColumn: "TeamPlayerID");
                });

            migrationBuilder.CreateTable(
                name: "VodTeams",
                schema: "Event",
                columns: table => new
                {
                    VodID = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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

            migrationBuilder.InsertData(
                schema: "Other",
                table: "GameVersions",
                columns: new[] { "GameVesionID", "CreatedAt", "CreatedByID", "GameVersionName", "ModifiedAt", "ModifiedByID", "Slug" },
                values: new object[,]
                {
                    { 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Halo 3", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "halo3" },
                    { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Halo Reach", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "reach" },
                    { 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Halo Reach Dash", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "reach-dash" },
                    { 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Halo 4", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "halo4" },
                    { 5, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Halo 5", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "halo5" },
                    { 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Halo Infinite", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "infinite" }
                });

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

            migrationBuilder.InsertData(
                schema: "Other",
                table: "Regions",
                columns: new[] { "RegionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "RegionName" },
                values: new object[,]
                {
                    { 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "West North America" },
                    { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Central North America" },
                    { 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "East North America" },
                    { 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "North Europe" },
                    { 5, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "South Europe" },
                    { 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Australia" },
                    { 7, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Europe" }
                });

            migrationBuilder.InsertData(
                schema: "Auth",
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { 1, "cda78946-53d4-4154-b723-2b33b95341cc", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Sysadmin", "SYSADMIN" },
                    { 2, "37ff5fdf-5cb8-403c-bb9e-81db05a5fcdd", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Commissioner", "COMMISSIONER" },
                    { 3, "0be992c7-f824-40c7-8975-964068c7fbfe", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Player", "PLAYER" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityOptions_DayOfWeek_Time",
                schema: "Event",
                table: "AvailabilityOptions",
                columns: new[] { "DayOfWeek", "Time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityOptionsHistory_AvailabilityOptionID_PeriodEnd",
                schema: "Event",
                table: "AvailabilityOptionsHistory",
                columns: new[] { "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_DataProtectionKeysHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "DataProtectionKeysHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscordHistory_DiscordUserID_PeriodEnd",
                schema: "User",
                table: "DiscordHistory",
                columns: new[] { "DiscordUserID", "PeriodEnd" });

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
                name: "IX_GamertagClaims_TeamID",
                schema: "Event",
                table: "GamertagClaims",
                column: "TeamID");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaims_XboxUserID",
                schema: "Event",
                table: "GamertagClaims",
                column: "XboxUserID");

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaimsHistory_ClaimantUserID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory",
                columns: new[] { "ClaimantUserID", "PeriodEnd" });

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
                name: "IX_GamertagClaimsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GamertagClaimsHistory_XboxUserID_PeriodEnd",
                schema: "Event",
                table: "GamertagClaimsHistory",
                columns: new[] { "XboxUserID", "PeriodEnd" });

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
                name: "IX_GameVersions_GameVersionName",
                schema: "Other",
                table: "GameVersions",
                column: "GameVersionName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameVersions_Slug",
                schema: "Other",
                table: "GameVersions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameVersionsHistory_GameVesionID_PeriodEnd",
                schema: "Other",
                table: "GameVersionsHistory",
                columns: new[] { "GameVesionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalImportJobs_Status",
                schema: "Event",
                table: "HistoricalImportJobs",
                column: "Status");

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
                name: "IX_MatchBracketInfo_AwayTeamPreviousMatchBracketInfoID",
                schema: "Event",
                table: "MatchBracketInfo",
                column: "AwayTeamPreviousMatchBracketInfoID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchBracketInfo_HomeTeamPreviousMatchBracketInfoID",
                schema: "Event",
                table: "MatchBracketInfo",
                column: "HomeTeamPreviousMatchBracketInfoID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchBracketInfo_SeasonMatchID",
                schema: "Event",
                table: "MatchBracketInfo",
                column: "SeasonMatchID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchBracketInfoHistory_AwayTeamPreviousMatchBracketInfoID_PeriodEnd",
                schema: "Event",
                table: "MatchBracketInfoHistory",
                columns: new[] { "AwayTeamPreviousMatchBracketInfoID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchBracketInfoHistory_HomeTeamPreviousMatchBracketInfoID_PeriodEnd",
                schema: "Event",
                table: "MatchBracketInfoHistory",
                columns: new[] { "HomeTeamPreviousMatchBracketInfoID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchBracketInfoHistory_MatchBracketInfoID_PeriodEnd",
                schema: "Event",
                table: "MatchBracketInfoHistory",
                columns: new[] { "MatchBracketInfoID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchBracketInfoHistory_SeasonMatchID_PeriodEnd",
                schema: "Event",
                table: "MatchBracketInfoHistory",
                columns: new[] { "SeasonMatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedKickVotes_KickMatchedPlayerId",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                column: "KickMatchedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedKickVotes_VoterMatchedPlayerId",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                column: "VoterMatchedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedKickVotesHistory_KickMatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedKickVotesHistory",
                columns: new[] { "KickMatchedPlayerId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedKickVotesHistory_MatchId_VoterMatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedKickVotesHistory",
                columns: new[] { "MatchId", "VoterMatchedPlayerId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedKickVotesHistory_VoterMatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedKickVotesHistory",
                columns: new[] { "VoterMatchedPlayerId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatches_AwayTeamId",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "AwayTeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatches_HomeTeamId",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "HomeTeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatches_MatchID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                column: "MatchID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatchesHistory_AwayTeamId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                columns: new[] { "AwayTeamId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatchesHistory_HomeTeamId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                columns: new[] { "HomeTeamId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatchesHistory_Id_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatchesHistory_MatchID_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                columns: new[] { "MatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedPlayers_MatchedTeamID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "MatchedTeamID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedPlayers_UserID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedPlayersHistory_Id_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedPlayersHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedPlayersHistory_MatchedTeamID_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedPlayersHistory",
                columns: new[] { "MatchedTeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedPlayersHistory_UserID_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedPlayersHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedTeamsHistory_MatchedTeamId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedTeamsHistory",
                columns: new[] { "MatchedTeamId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedWinnerVotes_MatchedPlayerId",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                column: "MatchedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedWinnerVotesHistory_MatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedWinnerVotesHistory",
                columns: new[] { "MatchedPlayerId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedWinnerVotesHistory_MatchId_MatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedWinnerVotesHistory",
                columns: new[] { "MatchId", "MatchedPlayerId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchesHistory_MatchID_PeriodEnd",
                schema: "Infinite",
                table: "MatchesHistory",
                columns: new[] { "MatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_MatchID_TeamID",
                schema: "Infinite",
                table: "MatchParticipants",
                columns: new[] { "MatchID", "TeamID" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipants_XboxUserID",
                schema: "Infinite",
                table: "MatchParticipants",
                column: "XboxUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipantsHistory_MatchID_TeamID_PeriodEnd",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                columns: new[] { "MatchID", "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipantsHistory_MatchID_XboxUserID_PeriodEnd",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                columns: new[] { "MatchID", "XboxUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipantsHistory_XboxUserID_PeriodEnd",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                columns: new[] { "XboxUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_ApprovedByUserID",
                table: "MatchReschedules",
                column: "ApprovedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_DiscordThreadID",
                table: "MatchReschedules",
                column: "DiscordThreadID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_RequestedAt",
                table: "MatchReschedules",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_RequestedByUserID",
                table: "MatchReschedules",
                column: "RequestedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_SeasonMatchID",
                table: "MatchReschedules",
                column: "SeasonMatchID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_Status",
                table: "MatchReschedules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MatchTeamsHistory_MatchID_TeamID_PeriodEnd",
                schema: "Infinite",
                table: "MatchTeamsHistory",
                columns: new[] { "MatchID", "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalDifficultiesHistory_MedalDifficultyID_PeriodEnd",
                schema: "Infinite",
                table: "MedalDifficultiesHistory",
                columns: new[] { "MedalDifficultyID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalEarned_MatchID_XboxUserID",
                schema: "Infinite",
                table: "MedalEarned",
                columns: new[] { "MatchID", "XboxUserID" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalEarnedHistory_MatchID_XboxUserID_PeriodEnd",
                schema: "Infinite",
                table: "MedalEarnedHistory",
                columns: new[] { "MatchID", "XboxUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalEarnedHistory_MedalID_MatchID_XboxUserID_PeriodEnd",
                schema: "Infinite",
                table: "MedalEarnedHistory",
                columns: new[] { "MedalID", "MatchID", "XboxUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_Medals_MedalDifficultyID",
                schema: "Infinite",
                table: "Medals",
                column: "MedalDifficultyID");

            migrationBuilder.CreateIndex(
                name: "IX_Medals_MedalTypeID",
                schema: "Infinite",
                table: "Medals",
                column: "MedalTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_MedalsHistory_MedalDifficultyID_PeriodEnd",
                schema: "Infinite",
                table: "MedalsHistory",
                columns: new[] { "MedalDifficultyID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalsHistory_MedalID_PeriodEnd",
                schema: "Infinite",
                table: "MedalsHistory",
                columns: new[] { "MedalID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalsHistory_MedalTypeID_PeriodEnd",
                schema: "Infinite",
                table: "MedalsHistory",
                columns: new[] { "MedalTypeID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalTypesHistory_MedalTypeID_PeriodEnd",
                schema: "Infinite",
                table: "MedalTypesHistory",
                columns: new[] { "MedalTypeID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_FromDiscordUserId",
                schema: "Discord",
                table: "Messages",
                column: "FromDiscordUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ToDiscordUserId",
                schema: "Discord",
                table: "Messages",
                column: "ToDiscordUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagesHistory_FromDiscordUserId_PeriodEnd",
                schema: "Discord",
                table: "MessagesHistory",
                columns: new[] { "FromDiscordUserId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagesHistory_Id_PeriodEnd",
                schema: "Discord",
                table: "MessagesHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagesHistory_ToDiscordUserId_PeriodEnd",
                schema: "Discord",
                table: "MessagesHistory",
                columns: new[] { "ToDiscordUserId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_GroupKey_Sequence",
                schema: "Discord",
                table: "Outbox",
                columns: new[] { "GroupKey", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Status_NextAttemptAt",
                schema: "Discord",
                table: "Outbox",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetLinks_ExpiresAt",
                schema: "Auth",
                table: "PasswordResetLinks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetLinks_Token",
                schema: "Auth",
                table: "PasswordResetLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetLinks_UserId",
                schema: "Auth",
                table: "PasswordResetLinks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetLinksHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "PasswordResetLinksHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetLinksHistory_UserId_PeriodEnd",
                schema: "Auth",
                table: "PasswordResetLinksHistory",
                columns: new[] { "UserId", "PeriodEnd" });

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
                name: "IX_QueuedPlayersHistory_UserID_PeriodEnd",
                schema: "Matchmaking",
                table: "QueuedPlayersHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_RanksHistory_Id_PeriodEnd",
                schema: "Matchmaking",
                table: "RanksHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_Regions_RegionName",
                schema: "Other",
                table: "Regions",
                column: "RegionName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegionsHistory_RegionID_PeriodEnd",
                schema: "Other",
                table: "RegionsHistory",
                columns: new[] { "RegionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "Auth",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaimsHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "RoleClaimsHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaimsHistory_RoleId_PeriodEnd",
                schema: "Auth",
                table: "RoleClaimsHistory",
                columns: new[] { "RoleId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "Auth",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolesHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "RolesHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAvailability_AvailabilityOptionID",
                schema: "Event",
                table: "SeasonAvailability",
                column: "AvailabilityOptionID");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAvailabilityHistory_AvailabilityOptionID_PeriodEnd",
                schema: "Event",
                table: "SeasonAvailabilityHistory",
                columns: new[] { "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAvailabilityHistory_SeasonID_AvailabilityOptionID_PeriodEnd",
                schema: "Event",
                table: "SeasonAvailabilityHistory",
                columns: new[] { "SeasonID", "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatches_ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches",
                column: "ActiveRescheduleRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatches_AwayTeamID",
                schema: "Event",
                table: "SeasonMatches",
                column: "AwayTeamID");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatches_HomeTeamID",
                schema: "Event",
                table: "SeasonMatches",
                column: "HomeTeamID");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatches_SeasonID",
                schema: "Event",
                table: "SeasonMatches",
                column: "SeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatchesHistory_ActiveRescheduleRequestId_PeriodEnd",
                schema: "Event",
                table: "SeasonMatchesHistory",
                columns: new[] { "ActiveRescheduleRequestId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatchesHistory_AwayTeamID_PeriodEnd",
                schema: "Event",
                table: "SeasonMatchesHistory",
                columns: new[] { "AwayTeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatchesHistory_HomeTeamID_PeriodEnd",
                schema: "Event",
                table: "SeasonMatchesHistory",
                columns: new[] { "HomeTeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatchesHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "SeasonMatchesHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatchesHistory_SeasonMatchID_PeriodEnd",
                schema: "Event",
                table: "SeasonMatchesHistory",
                columns: new[] { "SeasonMatchID", "PeriodEnd" });

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
                name: "IX_SeasonsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "SeasonsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSignups_SeasonID",
                schema: "Event",
                table: "SeasonSignups",
                column: "SeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSignups_UserID_SeasonID",
                schema: "Event",
                table: "SeasonSignups",
                columns: new[] { "UserID", "SeasonID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSignupsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "SeasonSignupsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSignupsHistory_SeasonSignupID_PeriodEnd",
                schema: "Event",
                table: "SeasonSignupsHistory",
                columns: new[] { "SeasonSignupID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSignupsHistory_UserID_PeriodEnd",
                schema: "Event",
                table: "SeasonSignupsHistory",
                columns: new[] { "UserID", "PeriodEnd" });

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
                name: "IX_SignupAvailability_AvailabilityOptionID",
                schema: "Event",
                table: "SignupAvailability",
                column: "AvailabilityOptionID");

            migrationBuilder.CreateIndex(
                name: "IX_SignupAvailabilityHistory_AvailabilityOptionID_PeriodEnd",
                schema: "Event",
                table: "SignupAvailabilityHistory",
                columns: new[] { "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SignupAvailabilityHistory_SeasonSignupID_AvailabilityOptionID_PeriodEnd",
                schema: "Event",
                table: "SignupAvailabilityHistory",
                columns: new[] { "SeasonSignupID", "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamAvailability_AvailabilityOptionID",
                schema: "Event",
                table: "TeamAvailability",
                column: "AvailabilityOptionID");

            migrationBuilder.CreateIndex(
                name: "IX_TeamAvailabilityHistory_AvailabilityOptionID_PeriodEnd",
                schema: "Event",
                table: "TeamAvailabilityHistory",
                columns: new[] { "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamAvailabilityHistory_TeamID_AvailabilityOptionID_PeriodEnd",
                schema: "Event",
                table: "TeamAvailabilityHistory",
                columns: new[] { "TeamID", "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_TeamID",
                schema: "Event",
                table: "TeamPlayers",
                column: "TeamID");

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_UserID_TeamID",
                schema: "Event",
                table: "TeamPlayers",
                columns: new[] { "UserID", "TeamID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayersHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "TeamPlayersHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayersHistory_TeamPlayerID_PeriodEnd",
                schema: "Event",
                table: "TeamPlayersHistory",
                columns: new[] { "TeamPlayerID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayersHistory_UserID_PeriodEnd",
                schema: "Event",
                table: "TeamPlayersHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CaptainID",
                schema: "Event",
                table: "Teams",
                column: "CaptainID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_SeasonID",
                schema: "Event",
                table: "Teams",
                column: "SeasonID");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TeamName_SeasonID",
                schema: "Event",
                table: "Teams",
                columns: new[] { "TeamName", "SeasonID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamsHistory_CaptainID_PeriodEnd",
                schema: "Event",
                table: "TeamsHistory",
                columns: new[] { "CaptainID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "TeamsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "TeamsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "Auth",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaimsHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "UserClaimsHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserClaimsHistory_UserId_PeriodEnd",
                schema: "Auth",
                table: "UserClaimsHistory",
                columns: new[] { "UserId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserExperiences_GameVersionID",
                schema: "Other",
                table: "UserExperiences",
                column: "GameVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_UserExperiencesHistory_GameVersionID_PeriodEnd",
                schema: "Other",
                table: "UserExperiencesHistory",
                columns: new[] { "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserExperiencesHistory_UserID_GameVersionID_PeriodEnd",
                schema: "Other",
                table: "UserExperiencesHistory",
                columns: new[] { "UserID", "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "Auth",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginsHistory_LoginProvider_ProviderKey_PeriodEnd",
                schema: "Auth",
                table: "UserLoginsHistory",
                columns: new[] { "LoginProvider", "ProviderKey", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginsHistory_UserId_PeriodEnd",
                schema: "Auth",
                table: "UserLoginsHistory",
                columns: new[] { "UserId", "PeriodEnd" });

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

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "Auth",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRolesHistory_RoleId_PeriodEnd",
                schema: "Auth",
                table: "UserRolesHistory",
                columns: new[] { "RoleId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRolesHistory_UserId_RoleId_PeriodEnd",
                schema: "Auth",
                table: "UserRolesHistory",
                columns: new[] { "UserId", "RoleId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "Auth",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DiscordUserID",
                schema: "Auth",
                table: "Users",
                column: "DiscordUserID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RegionID",
                schema: "Auth",
                table: "Users",
                column: "RegionID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_XboxUserID",
                schema: "Auth",
                table: "Users",
                column: "XboxUserID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "Auth",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsersHistory_DiscordUserID_PeriodEnd",
                schema: "Auth",
                table: "UsersHistory",
                columns: new[] { "DiscordUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UsersHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "UsersHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UsersHistory_RegionID_PeriodEnd",
                schema: "Auth",
                table: "UsersHistory",
                columns: new[] { "RegionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UsersHistory_XboxUserID_PeriodEnd",
                schema: "Auth",
                table: "UsersHistory",
                columns: new[] { "XboxUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTokensHistory_UserId_LoginProvider_Name_PeriodEnd",
                schema: "Auth",
                table: "UserTokensHistory",
                columns: new[] { "UserId", "LoginProvider", "Name", "PeriodEnd" });

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

            migrationBuilder.CreateIndex(
                name: "IX_XboxUsersHistory_XboxUserID_PeriodEnd",
                schema: "Xbox",
                table: "XboxUsersHistory",
                columns: new[] { "XboxUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_XuidLookups_LookedUpAt",
                schema: "Xbox",
                table: "XuidLookups",
                column: "LookedUpAt");

            migrationBuilder.AddForeignKey(
                name: "FK_GameParticipants_GameTeams_RecordedGameTeamID",
                schema: "Recorded",
                table: "GameParticipants",
                column: "RecordedGameTeamID",
                principalSchema: "Recorded",
                principalTable: "GameTeams",
                principalColumn: "RecordedGameTeamID");

            migrationBuilder.AddForeignKey(
                name: "FK_GamertagClaims_Teams_TeamID",
                schema: "Event",
                table: "GamertagClaims",
                column: "TeamID",
                principalSchema: "Event",
                principalTable: "Teams",
                principalColumn: "TeamID");

            migrationBuilder.AddForeignKey(
                name: "FK_GameTeams_Teams_TeamID",
                schema: "Recorded",
                table: "GameTeams",
                column: "TeamID",
                principalSchema: "Event",
                principalTable: "Teams",
                principalColumn: "TeamID");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchBracketInfo_SeasonMatches_SeasonMatchID",
                schema: "Event",
                table: "MatchBracketInfo",
                column: "SeasonMatchID",
                principalSchema: "Event",
                principalTable: "SeasonMatches",
                principalColumn: "SeasonMatchID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchReschedules_SeasonMatches_SeasonMatchID",
                table: "MatchReschedules",
                column: "SeasonMatchID",
                principalSchema: "Event",
                principalTable: "SeasonMatches",
                principalColumn: "SeasonMatchID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerChampionships_Teams_TeamID",
                schema: "Event",
                table: "PlayerChampionships",
                column: "TeamID",
                principalSchema: "Event",
                principalTable: "Teams",
                principalColumn: "TeamID");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerSeasonStats_Teams_TeamID",
                schema: "Event",
                table: "PlayerSeasonStats",
                column: "TeamID",
                principalSchema: "Event",
                principalTable: "Teams",
                principalColumn: "TeamID");

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonMatches_Teams_AwayTeamID",
                schema: "Event",
                table: "SeasonMatches",
                column: "AwayTeamID",
                principalSchema: "Event",
                principalTable: "Teams",
                principalColumn: "TeamID");

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonMatches_Teams_HomeTeamID",
                schema: "Event",
                table: "SeasonMatches",
                column: "HomeTeamID",
                principalSchema: "Event",
                principalTable: "Teams",
                principalColumn: "TeamID");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamAvailability_Teams_TeamID",
                schema: "Event",
                table: "TeamAvailability",
                column: "TeamID",
                principalSchema: "Event",
                principalTable: "Teams",
                principalColumn: "TeamID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamPlayers_Teams_TeamID",
                schema: "Event",
                table: "TeamPlayers",
                column: "TeamID",
                principalSchema: "Event",
                principalTable: "Teams",
                principalColumn: "TeamID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("""
                CREATE TRIGGER "AvailabilityOptions_History_Update" AFTER UPDATE ON "AvailabilityOptions" FOR EACH ROW BEGIN
                    INSERT INTO "AvailabilityOptionsHistory" ("AvailabilityOptionID", "CreatedAt", "CreatedByID", "DayOfWeek", "ModifiedAt", "ModifiedByID", "PeriodStart", "Time", "PeriodEnd") VALUES (OLD."AvailabilityOptionID", OLD."CreatedAt", OLD."CreatedByID", OLD."DayOfWeek", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Time", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "AvailabilityOptions" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "AvailabilityOptions_History_Delete" AFTER DELETE ON "AvailabilityOptions" FOR EACH ROW BEGIN
                    INSERT INTO "AvailabilityOptionsHistory" ("AvailabilityOptionID", "CreatedAt", "CreatedByID", "DayOfWeek", "ModifiedAt", "ModifiedByID", "PeriodStart", "Time", "PeriodEnd") VALUES (OLD."AvailabilityOptionID", OLD."CreatedAt", OLD."CreatedByID", OLD."DayOfWeek", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Time", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "DataProtectionKeys_History_Update" AFTER UPDATE ON "DataProtectionKeys" FOR EACH ROW BEGIN
                    INSERT INTO "DataProtectionKeysHistory" ("FriendlyName", "Id", "PeriodStart", "Xml", "PeriodEnd") VALUES (OLD."FriendlyName", OLD."Id", OLD."PeriodStart", OLD."Xml", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "DataProtectionKeys" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "DataProtectionKeys_History_Delete" AFTER DELETE ON "DataProtectionKeys" FOR EACH ROW BEGIN
                    INSERT INTO "DataProtectionKeysHistory" ("FriendlyName", "Id", "PeriodStart", "Xml", "PeriodEnd") VALUES (OLD."FriendlyName", OLD."Id", OLD."PeriodStart", OLD."Xml", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Discord_History_Update" AFTER UPDATE ON "Discord" FOR EACH ROW BEGIN
                    INSERT INTO "DiscordHistory" ("CreatedAt", "CreatedByID", "DiscordUserID", "DiscordUsername", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DiscordUsername", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Discord" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Discord_History_Delete" AFTER DELETE ON "Discord" FOR EACH ROW BEGIN
                    INSERT INTO "DiscordHistory" ("CreatedAt", "CreatedByID", "DiscordUserID", "DiscordUsername", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DiscordUsername", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameParticipants_History_Update" AFTER UPDATE ON "GameParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "GameParticipantsHistory" ("Assists", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "Deaths", "Gamertag", "Goals", "HammerKills", "Kills", "ModifiedAt", "ModifiedByID", "PeriodStart", "Punches", "RecordedGameID", "RecordedGameParticipantID", "RecordedGameTeamID", "Score", "Suicides", "SwordKills", "UserID", "PeriodEnd") VALUES (OLD."Assists", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."Deaths", OLD."Gamertag", OLD."Goals", OLD."HammerKills", OLD."Kills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Punches", OLD."RecordedGameID", OLD."RecordedGameParticipantID", OLD."RecordedGameTeamID", OLD."Score", OLD."Suicides", OLD."SwordKills", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameParticipants" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameParticipants_History_Delete" AFTER DELETE ON "GameParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "GameParticipantsHistory" ("Assists", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "Deaths", "Gamertag", "Goals", "HammerKills", "Kills", "ModifiedAt", "ModifiedByID", "PeriodStart", "Punches", "RecordedGameID", "RecordedGameParticipantID", "RecordedGameTeamID", "Score", "Suicides", "SwordKills", "UserID", "PeriodEnd") VALUES (OLD."Assists", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."Deaths", OLD."Gamertag", OLD."Goals", OLD."HammerKills", OLD."Kills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Punches", OLD."RecordedGameID", OLD."RecordedGameParticipantID", OLD."RecordedGameTeamID", OLD."Score", OLD."Suicides", OLD."SwordKills", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameTeams_History_Update" AFTER UPDATE ON "GameTeams" FOR EACH ROW BEGIN
                    INSERT INTO "GameTeamsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "Outcome", "PeriodStart", "RecordedGameID", "RecordedGameTeamID", "Score", "Side", "TeamID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."Outcome", OLD."PeriodStart", OLD."RecordedGameID", OLD."RecordedGameTeamID", OLD."Score", OLD."Side", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameTeams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameTeams_History_Delete" AFTER DELETE ON "GameTeams" FOR EACH ROW BEGIN
                    INSERT INTO "GameTeamsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "Outcome", "PeriodStart", "RecordedGameID", "RecordedGameTeamID", "Score", "Side", "TeamID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."Outcome", OLD."PeriodStart", OLD."RecordedGameID", OLD."RecordedGameTeamID", OLD."Score", OLD."Side", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameVersions_History_Update" AFTER UPDATE ON "GameVersions" FOR EACH ROW BEGIN
                    INSERT INTO "GameVersionsHistory" ("CreatedAt", "CreatedByID", "GameVersionName", "GameVesionID", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionName", OLD."GameVesionID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameVersions" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameVersions_History_Delete" AFTER DELETE ON "GameVersions" FOR EACH ROW BEGIN
                    INSERT INTO "GameVersionsHistory" ("CreatedAt", "CreatedByID", "GameVersionName", "GameVesionID", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionName", OLD."GameVesionID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "GamertagClaims_History_Update" AFTER UPDATE ON "GamertagClaims" FOR EACH ROW BEGIN
                    INSERT INTO "GamertagClaimsHistory" ("ClaimantUserID", "CreatedAt", "CreatedByID", "CurrentGamertag", "FirstSeen", "Gamertag", "GamertagClaimID", "Kind", "LastSeen", "LookedUpAt", "ModifiedAt", "ModifiedByID", "NormalizedGamertag", "PeriodStart", "PlaceholderUserID", "RejectionReason", "SeasonID", "Status", "TeamID", "XboxUserID", "PeriodEnd") VALUES (OLD."ClaimantUserID", OLD."CreatedAt", OLD."CreatedByID", OLD."CurrentGamertag", OLD."FirstSeen", OLD."Gamertag", OLD."GamertagClaimID", OLD."Kind", OLD."LastSeen", OLD."LookedUpAt", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedGamertag", OLD."PeriodStart", OLD."PlaceholderUserID", OLD."RejectionReason", OLD."SeasonID", OLD."Status", OLD."TeamID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GamertagClaims" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GamertagClaims_History_Delete" AFTER DELETE ON "GamertagClaims" FOR EACH ROW BEGIN
                    INSERT INTO "GamertagClaimsHistory" ("ClaimantUserID", "CreatedAt", "CreatedByID", "CurrentGamertag", "FirstSeen", "Gamertag", "GamertagClaimID", "Kind", "LastSeen", "LookedUpAt", "ModifiedAt", "ModifiedByID", "NormalizedGamertag", "PeriodStart", "PlaceholderUserID", "RejectionReason", "SeasonID", "Status", "TeamID", "XboxUserID", "PeriodEnd") VALUES (OLD."ClaimantUserID", OLD."CreatedAt", OLD."CreatedByID", OLD."CurrentGamertag", OLD."FirstSeen", OLD."Gamertag", OLD."GamertagClaimID", OLD."Kind", OLD."LastSeen", OLD."LookedUpAt", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedGamertag", OLD."PeriodStart", OLD."PlaceholderUserID", OLD."RejectionReason", OLD."SeasonID", OLD."Status", OLD."TeamID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Games_History_Update" AFTER UPDATE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Games" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Games_History_Delete" AFTER DELETE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

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
                CREATE TRIGGER "MatchBracketInfo_History_Update" AFTER UPDATE ON "MatchBracketInfo" FOR EACH ROW BEGIN
                    INSERT INTO "MatchBracketInfoHistory" ("AwayTeamPreviousMatchBracketInfoID", "AwayTeamSeedNumber", "Bracket", "CreatedAt", "CreatedByID", "HomeTeamPreviousMatchBracketInfoID", "HomeTeamSeedNumber", "MatchBracketInfoID", "MatchNumber", "ModifiedAt", "ModifiedByID", "PeriodStart", "RoundNumber", "SeasonMatchID", "PeriodEnd") VALUES (OLD."AwayTeamPreviousMatchBracketInfoID", OLD."AwayTeamSeedNumber", OLD."Bracket", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamPreviousMatchBracketInfoID", OLD."HomeTeamSeedNumber", OLD."MatchBracketInfoID", OLD."MatchNumber", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RoundNumber", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchBracketInfo" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchBracketInfo_History_Delete" AFTER DELETE ON "MatchBracketInfo" FOR EACH ROW BEGIN
                    INSERT INTO "MatchBracketInfoHistory" ("AwayTeamPreviousMatchBracketInfoID", "AwayTeamSeedNumber", "Bracket", "CreatedAt", "CreatedByID", "HomeTeamPreviousMatchBracketInfoID", "HomeTeamSeedNumber", "MatchBracketInfoID", "MatchNumber", "ModifiedAt", "ModifiedByID", "PeriodStart", "RoundNumber", "SeasonMatchID", "PeriodEnd") VALUES (OLD."AwayTeamPreviousMatchBracketInfoID", OLD."AwayTeamSeedNumber", OLD."Bracket", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamPreviousMatchBracketInfoID", OLD."HomeTeamSeedNumber", OLD."MatchBracketInfoID", OLD."MatchNumber", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RoundNumber", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchParticipants_History_Update" AFTER UPDATE ON "MatchParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "MatchParticipantsHistory" ("Accuracy", "Assists", "AverageLife", "Betrayals", "CalloutAssists", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "FirstJoinedTime", "JoinedInProgress", "Kda", "Kills", "LastLeaveTime", "LeftInProgress", "MatchID", "MaxKillingSpree", "MeleeKills", "ModifiedAt", "ModifiedByID", "ObjectivesCompleted", "PeriodStart", "PersonalScore", "PowerWeaponKills", "PresentAtBeginning", "PresentAtCompletion", "Rank", "RoundsLost", "RoundsTied", "RoundsWon", "Score", "ShotsFired", "ShotsHit", "Spawns", "Suicides", "TeamID", "TimePlayed", "XboxUserID", "PeriodEnd") VALUES (OLD."Accuracy", OLD."Assists", OLD."AverageLife", OLD."Betrayals", OLD."CalloutAssists", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."FirstJoinedTime", OLD."JoinedInProgress", OLD."Kda", OLD."Kills", OLD."LastLeaveTime", OLD."LeftInProgress", OLD."MatchID", OLD."MaxKillingSpree", OLD."MeleeKills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."ObjectivesCompleted", OLD."PeriodStart", OLD."PersonalScore", OLD."PowerWeaponKills", OLD."PresentAtBeginning", OLD."PresentAtCompletion", OLD."Rank", OLD."RoundsLost", OLD."RoundsTied", OLD."RoundsWon", OLD."Score", OLD."ShotsFired", OLD."ShotsHit", OLD."Spawns", OLD."Suicides", OLD."TeamID", OLD."TimePlayed", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchParticipants" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchParticipants_History_Delete" AFTER DELETE ON "MatchParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "MatchParticipantsHistory" ("Accuracy", "Assists", "AverageLife", "Betrayals", "CalloutAssists", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "FirstJoinedTime", "JoinedInProgress", "Kda", "Kills", "LastLeaveTime", "LeftInProgress", "MatchID", "MaxKillingSpree", "MeleeKills", "ModifiedAt", "ModifiedByID", "ObjectivesCompleted", "PeriodStart", "PersonalScore", "PowerWeaponKills", "PresentAtBeginning", "PresentAtCompletion", "Rank", "RoundsLost", "RoundsTied", "RoundsWon", "Score", "ShotsFired", "ShotsHit", "Spawns", "Suicides", "TeamID", "TimePlayed", "XboxUserID", "PeriodEnd") VALUES (OLD."Accuracy", OLD."Assists", OLD."AverageLife", OLD."Betrayals", OLD."CalloutAssists", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."FirstJoinedTime", OLD."JoinedInProgress", OLD."Kda", OLD."Kills", OLD."LastLeaveTime", OLD."LeftInProgress", OLD."MatchID", OLD."MaxKillingSpree", OLD."MeleeKills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."ObjectivesCompleted", OLD."PeriodStart", OLD."PersonalScore", OLD."PowerWeaponKills", OLD."PresentAtBeginning", OLD."PresentAtCompletion", OLD."Rank", OLD."RoundsLost", OLD."RoundsTied", OLD."RoundsWon", OLD."Score", OLD."ShotsFired", OLD."ShotsHit", OLD."Spawns", OLD."Suicides", OLD."TeamID", OLD."TimePlayed", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchTeams_History_Update" AFTER UPDATE ON "MatchTeams" FOR EACH ROW BEGIN
                    INSERT INTO "MatchTeamsHistory" ("CreatedAt", "CreatedByID", "MatchID", "ModifiedAt", "ModifiedByID", "Outcome", "PeriodStart", "Score", "TeamID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Outcome", OLD."PeriodStart", OLD."Score", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchTeams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchTeams_History_Delete" AFTER DELETE ON "MatchTeams" FOR EACH ROW BEGIN
                    INSERT INTO "MatchTeamsHistory" ("CreatedAt", "CreatedByID", "MatchID", "ModifiedAt", "ModifiedByID", "Outcome", "PeriodStart", "Score", "TeamID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Outcome", OLD."PeriodStart", OLD."Score", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

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
                    INSERT INTO "MatchedMatchesHistory" ("Active", "AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartedAt", "ThreadID", "VoteMessageID", "PeriodEnd") VALUES (OLD."Active", OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartedAt", OLD."ThreadID", OLD."VoteMessageID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedMatches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedMatches_History_Delete" AFTER DELETE ON "MatchedMatches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedMatchesHistory" ("Active", "AwayTeamId", "CreatedAt", "CreatedByID", "HomeTeamId", "Id", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartedAt", "ThreadID", "VoteMessageID", "PeriodEnd") VALUES (OLD."Active", OLD."AwayTeamId", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamId", OLD."Id", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartedAt", OLD."ThreadID", OLD."VoteMessageID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedPlayers_History_Update" AFTER UPDATE ON "MatchedPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedPlayersHistory" ("CreatedAt", "CreatedByID", "Id", "Kicked", "MatchedTeamID", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."Kicked", OLD."MatchedTeamID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedPlayers" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedPlayers_History_Delete" AFTER DELETE ON "MatchedPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedPlayersHistory" ("CreatedAt", "CreatedByID", "Id", "Kicked", "MatchedTeamID", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."Kicked", OLD."MatchedTeamID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedTeams_History_Update" AFTER UPDATE ON "MatchedTeams" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedTeamsHistory" ("CreatedAt", "CreatedByID", "MatchedTeamId", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MatchedTeamId", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedTeams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedTeams_History_Delete" AFTER DELETE ON "MatchedTeams" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedTeamsHistory" ("CreatedAt", "CreatedByID", "MatchedTeamId", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MatchedTeamId", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedWinnerVotes_History_Update" AFTER UPDATE ON "MatchedWinnerVotes" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedWinnerVotesHistory" ("CreatedAt", "CreatedByID", "MatchId", "MatchedPlayerId", "ModifiedAt", "ModifiedByID", "PeriodStart", "WinnerVote", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MatchId", OLD."MatchedPlayerId", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."WinnerVote", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedWinnerVotes" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedWinnerVotes_History_Delete" AFTER DELETE ON "MatchedWinnerVotes" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedWinnerVotesHistory" ("CreatedAt", "CreatedByID", "MatchId", "MatchedPlayerId", "ModifiedAt", "ModifiedByID", "PeriodStart", "WinnerVote", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MatchId", OLD."MatchedPlayerId", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."WinnerVote", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Matches_History_Update" AFTER UPDATE ON "Matches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchesHistory" ("CreatedAt", "CreatedByID", "Duration", "EndTime", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartTime", "StatsPullDate", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Duration", OLD."EndTime", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartTime", OLD."StatsPullDate", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Matches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Matches_History_Delete" AFTER DELETE ON "Matches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchesHistory" ("CreatedAt", "CreatedByID", "Duration", "EndTime", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartTime", "StatsPullDate", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Duration", OLD."EndTime", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartTime", OLD."StatsPullDate", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MedalDifficulties_History_Update" AFTER UPDATE ON "MedalDifficulties" FOR EACH ROW BEGIN
                    INSERT INTO "MedalDifficultiesHistory" ("CreatedAt", "CreatedByID", "MedalDifficultyID", "MedalDifficultyName", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MedalDifficultyID", OLD."MedalDifficultyName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MedalDifficulties" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MedalDifficulties_History_Delete" AFTER DELETE ON "MedalDifficulties" FOR EACH ROW BEGIN
                    INSERT INTO "MedalDifficultiesHistory" ("CreatedAt", "CreatedByID", "MedalDifficultyID", "MedalDifficultyName", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MedalDifficultyID", OLD."MedalDifficultyName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MedalEarned_History_Update" AFTER UPDATE ON "MedalEarned" FOR EACH ROW BEGIN
                    INSERT INTO "MedalEarnedHistory" ("Count", "CreatedAt", "CreatedByID", "MatchID", "MedalID", "ModifiedAt", "ModifiedByID", "PeriodStart", "TotalPersonalScoreAwarded", "XboxUserID", "PeriodEnd") VALUES (OLD."Count", OLD."CreatedAt", OLD."CreatedByID", OLD."MatchID", OLD."MedalID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."TotalPersonalScoreAwarded", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MedalEarned" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MedalEarned_History_Delete" AFTER DELETE ON "MedalEarned" FOR EACH ROW BEGIN
                    INSERT INTO "MedalEarnedHistory" ("Count", "CreatedAt", "CreatedByID", "MatchID", "MedalID", "ModifiedAt", "ModifiedByID", "PeriodStart", "TotalPersonalScoreAwarded", "XboxUserID", "PeriodEnd") VALUES (OLD."Count", OLD."CreatedAt", OLD."CreatedByID", OLD."MatchID", OLD."MedalID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."TotalPersonalScoreAwarded", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MedalTypes_History_Update" AFTER UPDATE ON "MedalTypes" FOR EACH ROW BEGIN
                    INSERT INTO "MedalTypesHistory" ("CreatedAt", "CreatedByID", "MedalTypeID", "MedalTypeName", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MedalTypeID", OLD."MedalTypeName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MedalTypes" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MedalTypes_History_Delete" AFTER DELETE ON "MedalTypes" FOR EACH ROW BEGIN
                    INSERT INTO "MedalTypesHistory" ("CreatedAt", "CreatedByID", "MedalTypeID", "MedalTypeName", "ModifiedAt", "ModifiedByID", "PeriodStart", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."MedalTypeID", OLD."MedalTypeName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Medals_History_Update" AFTER UPDATE ON "Medals" FOR EACH ROW BEGIN
                    INSERT INTO "MedalsHistory" ("CreatedAt", "CreatedByID", "Description", "MedalDifficultyID", "MedalID", "MedalName", "MedalTypeID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PersonalScore", "SortingWeight", "SpriteIndex", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."MedalDifficultyID", OLD."MedalID", OLD."MedalName", OLD."MedalTypeID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PersonalScore", OLD."SortingWeight", OLD."SpriteIndex", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Medals" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Medals_History_Delete" AFTER DELETE ON "Medals" FOR EACH ROW BEGIN
                    INSERT INTO "MedalsHistory" ("CreatedAt", "CreatedByID", "Description", "MedalDifficultyID", "MedalID", "MedalName", "MedalTypeID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PersonalScore", "SortingWeight", "SpriteIndex", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."MedalDifficultyID", OLD."MedalID", OLD."MedalName", OLD."MedalTypeID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PersonalScore", OLD."SortingWeight", OLD."SpriteIndex", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Messages_History_Update" AFTER UPDATE ON "Messages" FOR EACH ROW BEGIN
                    INSERT INTO "MessagesHistory" ("Content", "CreatedAt", "CreatedByID", "FromDiscordUserId", "Id", "ModifiedAt", "ModifiedByID", "PeriodStart", "Timestamp", "ToDiscordUserId", "PeriodEnd") VALUES (OLD."Content", OLD."CreatedAt", OLD."CreatedByID", OLD."FromDiscordUserId", OLD."Id", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Timestamp", OLD."ToDiscordUserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Messages" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Messages_History_Delete" AFTER DELETE ON "Messages" FOR EACH ROW BEGIN
                    INSERT INTO "MessagesHistory" ("Content", "CreatedAt", "CreatedByID", "FromDiscordUserId", "Id", "ModifiedAt", "ModifiedByID", "PeriodStart", "Timestamp", "ToDiscordUserId", "PeriodEnd") VALUES (OLD."Content", OLD."CreatedAt", OLD."CreatedByID", OLD."FromDiscordUserId", OLD."Id", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Timestamp", OLD."ToDiscordUserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "PasswordResetLinks_History_Update" AFTER UPDATE ON "PasswordResetLinks" FOR EACH ROW BEGIN
                    INSERT INTO "PasswordResetLinksHistory" ("CreatedAt", "CreatedByID", "ExpiresAt", "Id", "IsUsed", "ModifiedAt", "ModifiedByID", "PeriodStart", "Token", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ExpiresAt", OLD."Id", OLD."IsUsed", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Token", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "PasswordResetLinks" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "PasswordResetLinks_History_Delete" AFTER DELETE ON "PasswordResetLinks" FOR EACH ROW BEGIN
                    INSERT INTO "PasswordResetLinksHistory" ("CreatedAt", "CreatedByID", "ExpiresAt", "Id", "IsUsed", "ModifiedAt", "ModifiedByID", "PeriodStart", "Token", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ExpiresAt", OLD."Id", OLD."IsUsed", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Token", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "PastGamertags_History_Update" AFTER UPDATE ON "PastGamertags" FOR EACH ROW BEGIN
                    INSERT INTO "PastGamertagsHistory" ("CreatedAt", "CreatedByID", "FirstSeen", "Gamertag", "LastSeen", "ModifiedAt", "ModifiedByID", "NormalizedGamertag", "Note", "PeriodStart", "Source", "XboxGamertagHistoryID", "XboxUserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."FirstSeen", OLD."Gamertag", OLD."LastSeen", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedGamertag", OLD."Note", OLD."PeriodStart", OLD."Source", OLD."XboxGamertagHistoryID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "PastGamertags" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "PastGamertags_History_Delete" AFTER DELETE ON "PastGamertags" FOR EACH ROW BEGIN
                    INSERT INTO "PastGamertagsHistory" ("CreatedAt", "CreatedByID", "FirstSeen", "Gamertag", "LastSeen", "ModifiedAt", "ModifiedByID", "NormalizedGamertag", "Note", "PeriodStart", "Source", "XboxGamertagHistoryID", "XboxUserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."FirstSeen", OLD."Gamertag", OLD."LastSeen", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedGamertag", OLD."Note", OLD."PeriodStart", OLD."Source", OLD."XboxGamertagHistoryID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "PlayerChampionships_History_Update" AFTER UPDATE ON "PlayerChampionships" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerChampionshipsHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "GamertagUsed", "IsCaptain", "ModifiedAt", "ModifiedByID", "PeriodStart", "PlayerChampionshipID", "SeasonID", "TeamID", "Tier", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."GamertagUsed", OLD."IsCaptain", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PlayerChampionshipID", OLD."SeasonID", OLD."TeamID", OLD."Tier", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "PlayerChampionships" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "PlayerChampionships_History_Delete" AFTER DELETE ON "PlayerChampionships" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerChampionshipsHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "GamertagUsed", "IsCaptain", "ModifiedAt", "ModifiedByID", "PeriodStart", "PlayerChampionshipID", "SeasonID", "TeamID", "Tier", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."GamertagUsed", OLD."IsCaptain", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PlayerChampionshipID", OLD."SeasonID", OLD."TeamID", OLD."Tier", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "PlayerSeasonStats_History_Update" AFTER UPDATE ON "PlayerSeasonStats" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerSeasonStatsHistory" ("Assists", "AverageLifeSeconds", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "ExtendedMetricsJson", "Exterminations", "GameVersionID", "Gamertag", "GamesPlayed", "Goals", "HammerKills", "IsCombined", "IsPlayoffs", "KdRatio", "KdSpread", "Kills", "Losses", "ModifiedAt", "ModifiedByID", "MultiKills", "PeriodStart", "PlayerSeasonStatID", "Punches", "RoleNote", "Score", "SeasonID", "Sprees", "StintIndex", "SwordKills", "TeamID", "TotalSecondsPlayed", "TradeNote", "UserID", "WinPercent", "Wins", "PeriodEnd") VALUES (OLD."Assists", OLD."AverageLifeSeconds", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."ExtendedMetricsJson", OLD."Exterminations", OLD."GameVersionID", OLD."Gamertag", OLD."GamesPlayed", OLD."Goals", OLD."HammerKills", OLD."IsCombined", OLD."IsPlayoffs", OLD."KdRatio", OLD."KdSpread", OLD."Kills", OLD."Losses", OLD."ModifiedAt", OLD."ModifiedByID", OLD."MultiKills", OLD."PeriodStart", OLD."PlayerSeasonStatID", OLD."Punches", OLD."RoleNote", OLD."Score", OLD."SeasonID", OLD."Sprees", OLD."StintIndex", OLD."SwordKills", OLD."TeamID", OLD."TotalSecondsPlayed", OLD."TradeNote", OLD."UserID", OLD."WinPercent", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "PlayerSeasonStats" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "PlayerSeasonStats_History_Delete" AFTER DELETE ON "PlayerSeasonStats" FOR EACH ROW BEGIN
                    INSERT INTO "PlayerSeasonStatsHistory" ("Assists", "AverageLifeSeconds", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "ExtendedMetricsJson", "Exterminations", "GameVersionID", "Gamertag", "GamesPlayed", "Goals", "HammerKills", "IsCombined", "IsPlayoffs", "KdRatio", "KdSpread", "Kills", "Losses", "ModifiedAt", "ModifiedByID", "MultiKills", "PeriodStart", "PlayerSeasonStatID", "Punches", "RoleNote", "Score", "SeasonID", "Sprees", "StintIndex", "SwordKills", "TeamID", "TotalSecondsPlayed", "TradeNote", "UserID", "WinPercent", "Wins", "PeriodEnd") VALUES (OLD."Assists", OLD."AverageLifeSeconds", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."ExtendedMetricsJson", OLD."Exterminations", OLD."GameVersionID", OLD."Gamertag", OLD."GamesPlayed", OLD."Goals", OLD."HammerKills", OLD."IsCombined", OLD."IsPlayoffs", OLD."KdRatio", OLD."KdSpread", OLD."Kills", OLD."Losses", OLD."ModifiedAt", OLD."ModifiedByID", OLD."MultiKills", OLD."PeriodStart", OLD."PlayerSeasonStatID", OLD."Punches", OLD."RoleNote", OLD."Score", OLD."SeasonID", OLD."Sprees", OLD."StintIndex", OLD."SwordKills", OLD."TeamID", OLD."TotalSecondsPlayed", OLD."TradeNote", OLD."UserID", OLD."WinPercent", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "QueuedPlayers_History_Update" AFTER UPDATE ON "QueuedPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "QueuedPlayersHistory" ("CreatedAt", "CreatedByID", "JoinedAt", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."JoinedAt", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "QueuedPlayers" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "QueuedPlayers_History_Delete" AFTER DELETE ON "QueuedPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "QueuedPlayersHistory" ("CreatedAt", "CreatedByID", "JoinedAt", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."JoinedAt", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Ranks_History_Update" AFTER UPDATE ON "Ranks" FOR EACH ROW BEGIN
                    INSERT INTO "RanksHistory" ("Color", "CreatedAt", "CreatedByID", "Description", "Icon", "Id", "MmrThreshold", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "PeriodEnd") VALUES (OLD."Color", OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."Icon", OLD."Id", OLD."MmrThreshold", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Ranks" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Ranks_History_Delete" AFTER DELETE ON "Ranks" FOR EACH ROW BEGIN
                    INSERT INTO "RanksHistory" ("Color", "CreatedAt", "CreatedByID", "Description", "Icon", "Id", "MmrThreshold", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "PeriodEnd") VALUES (OLD."Color", OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."Icon", OLD."Id", OLD."MmrThreshold", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Regions_History_Update" AFTER UPDATE ON "Regions" FOR EACH ROW BEGIN
                    INSERT INTO "RegionsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RegionID", "RegionName", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RegionID", OLD."RegionName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Regions" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Regions_History_Delete" AFTER DELETE ON "Regions" FOR EACH ROW BEGIN
                    INSERT INTO "RegionsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RegionID", "RegionName", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RegionID", OLD."RegionName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "RoleClaims_History_Update" AFTER UPDATE ON "RoleClaims" FOR EACH ROW BEGIN
                    INSERT INTO "RoleClaimsHistory" ("ClaimType", "ClaimValue", "Id", "PeriodStart", "RoleId", "PeriodEnd") VALUES (OLD."ClaimType", OLD."ClaimValue", OLD."Id", OLD."PeriodStart", OLD."RoleId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "RoleClaims" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "RoleClaims_History_Delete" AFTER DELETE ON "RoleClaims" FOR EACH ROW BEGIN
                    INSERT INTO "RoleClaimsHistory" ("ClaimType", "ClaimValue", "Id", "PeriodStart", "RoleId", "PeriodEnd") VALUES (OLD."ClaimType", OLD."ClaimValue", OLD."Id", OLD."PeriodStart", OLD."RoleId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Roles_History_Update" AFTER UPDATE ON "Roles" FOR EACH ROW BEGIN
                    INSERT INTO "RolesHistory" ("ConcurrencyStamp", "CreatedAt", "CreatedByID", "Id", "ModifiedAt", "ModifiedByID", "Name", "NormalizedName", "PeriodStart", "PeriodEnd") VALUES (OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."NormalizedName", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Roles" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Roles_History_Delete" AFTER DELETE ON "Roles" FOR EACH ROW BEGIN
                    INSERT INTO "RolesHistory" ("ConcurrencyStamp", "CreatedAt", "CreatedByID", "Id", "ModifiedAt", "ModifiedByID", "Name", "NormalizedName", "PeriodStart", "PeriodEnd") VALUES (OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."NormalizedName", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeasonAvailability_History_Update" AFTER UPDATE ON "SeasonAvailability" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonAvailabilityHistory" ("AvailabilityOptionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "SeasonID", "PeriodEnd") VALUES (OLD."AvailabilityOptionID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SeasonID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeasonAvailability" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeasonAvailability_History_Delete" AFTER DELETE ON "SeasonAvailability" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonAvailabilityHistory" ("AvailabilityOptionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "SeasonID", "PeriodEnd") VALUES (OLD."AvailabilityOptionID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SeasonID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeasonMatches_History_Update" AFTER UPDATE ON "SeasonMatches" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonMatchesHistory" ("ActiveRescheduleRequestId", "AwayTeamID", "AwayTeamResult", "AwayTeamScore", "BestOf", "CreatedAt", "CreatedByID", "HomeTeamID", "HomeTeamResult", "HomeTeamScore", "ModifiedAt", "ModifiedByID", "PeriodStart", "ScheduledTime", "SeasonID", "SeasonMatchID", "PeriodEnd") VALUES (OLD."ActiveRescheduleRequestId", OLD."AwayTeamID", OLD."AwayTeamResult", OLD."AwayTeamScore", OLD."BestOf", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamID", OLD."HomeTeamResult", OLD."HomeTeamScore", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ScheduledTime", OLD."SeasonID", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeasonMatches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeasonMatches_History_Delete" AFTER DELETE ON "SeasonMatches" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonMatchesHistory" ("ActiveRescheduleRequestId", "AwayTeamID", "AwayTeamResult", "AwayTeamScore", "BestOf", "CreatedAt", "CreatedByID", "HomeTeamID", "HomeTeamResult", "HomeTeamScore", "ModifiedAt", "ModifiedByID", "PeriodStart", "ScheduledTime", "SeasonID", "SeasonMatchID", "PeriodEnd") VALUES (OLD."ActiveRescheduleRequestId", OLD."AwayTeamID", OLD."AwayTeamResult", OLD."AwayTeamScore", OLD."BestOf", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamID", OLD."HomeTeamResult", OLD."HomeTeamScore", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ScheduledTime", OLD."SeasonID", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeasonSignups_History_Update" AFTER UPDATE ON "SeasonSignups" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonSignupsHistory" ("Approved", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RequiresAssistanceDrafting", "SeasonID", "SeasonSignupID", "TeamName", "Timestamp", "UserID", "WillCaptain", "PeriodEnd") VALUES (OLD."Approved", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RequiresAssistanceDrafting", OLD."SeasonID", OLD."SeasonSignupID", OLD."TeamName", OLD."Timestamp", OLD."UserID", OLD."WillCaptain", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeasonSignups" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeasonSignups_History_Delete" AFTER DELETE ON "SeasonSignups" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonSignupsHistory" ("Approved", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RequiresAssistanceDrafting", "SeasonID", "SeasonSignupID", "TeamName", "Timestamp", "UserID", "WillCaptain", "PeriodEnd") VALUES (OLD."Approved", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RequiresAssistanceDrafting", OLD."SeasonID", OLD."SeasonSignupID", OLD."TeamName", OLD."Timestamp", OLD."UserID", OLD."WillCaptain", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Seasons_History_Update" AFTER UPDATE ON "Seasons" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonsHistory" ("CaptainsLocked", "CreatedAt", "CreatedByID", "Division", "DraftStart", "Format", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "LeagueID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublicAt", "SeasonEnd", "SeasonID", "SeasonName", "SeasonStart", "SignupsClose", "SignupsOpen", "PeriodEnd") VALUES (OLD."CaptainsLocked", OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."DraftStart", OLD."Format", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."LeagueID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublicAt", OLD."SeasonEnd", OLD."SeasonID", OLD."SeasonName", OLD."SeasonStart", OLD."SignupsClose", OLD."SignupsOpen", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Seasons" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Seasons_History_Delete" AFTER DELETE ON "Seasons" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonsHistory" ("CaptainsLocked", "CreatedAt", "CreatedByID", "Division", "DraftStart", "Format", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "LeagueID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublicAt", "SeasonEnd", "SeasonID", "SeasonName", "SeasonStart", "SignupsClose", "SignupsOpen", "PeriodEnd") VALUES (OLD."CaptainsLocked", OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."DraftStart", OLD."Format", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."LeagueID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublicAt", OLD."SeasonEnd", OLD."SeasonID", OLD."SeasonName", OLD."SeasonStart", OLD."SignupsClose", OLD."SignupsOpen", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeriesGames_History_Update" AFTER UPDATE ON "SeriesGames" FOR EACH ROW BEGIN
                    INSERT INTO "SeriesGamesHistory" ("CreatedAt", "CreatedByID", "GameNumber", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedGameID", "SeasonMatchID", "SeriesGameID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameNumber", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedGameID", OLD."SeasonMatchID", OLD."SeriesGameID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeriesGames" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeriesGames_History_Delete" AFTER DELETE ON "SeriesGames" FOR EACH ROW BEGIN
                    INSERT INTO "SeriesGamesHistory" ("CreatedAt", "CreatedByID", "GameNumber", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedGameID", "SeasonMatchID", "SeriesGameID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameNumber", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedGameID", OLD."SeasonMatchID", OLD."SeriesGameID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "SignupAvailability_History_Update" AFTER UPDATE ON "SignupAvailability" FOR EACH ROW BEGIN
                    INSERT INTO "SignupAvailabilityHistory" ("AvailabilityOptionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "SeasonSignupID", "PeriodEnd") VALUES (OLD."AvailabilityOptionID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SeasonSignupID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SignupAvailability" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SignupAvailability_History_Delete" AFTER DELETE ON "SignupAvailability" FOR EACH ROW BEGIN
                    INSERT INTO "SignupAvailabilityHistory" ("AvailabilityOptionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "SeasonSignupID", "PeriodEnd") VALUES (OLD."AvailabilityOptionID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SeasonSignupID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "TeamAvailability_History_Update" AFTER UPDATE ON "TeamAvailability" FOR EACH ROW BEGIN
                    INSERT INTO "TeamAvailabilityHistory" ("AvailabilityOptionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "TeamID", "PeriodEnd") VALUES (OLD."AvailabilityOptionID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "TeamAvailability" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "TeamAvailability_History_Delete" AFTER DELETE ON "TeamAvailability" FOR EACH ROW BEGIN
                    INSERT INTO "TeamAvailabilityHistory" ("AvailabilityOptionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "TeamID", "PeriodEnd") VALUES (OLD."AvailabilityOptionID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "TeamPlayers_History_Update" AFTER UPDATE ON "TeamPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "TeamPlayersHistory" ("CreatedAt", "CreatedByID", "DraftCaptainOrder", "DraftPick", "DraftRound", "ModifiedAt", "ModifiedByID", "PeriodStart", "TeamID", "TeamPlayerID", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."DraftCaptainOrder", OLD."DraftPick", OLD."DraftRound", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."TeamID", OLD."TeamPlayerID", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "TeamPlayers" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "TeamPlayers_History_Delete" AFTER DELETE ON "TeamPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "TeamPlayersHistory" ("CreatedAt", "CreatedByID", "DraftCaptainOrder", "DraftPick", "DraftRound", "ModifiedAt", "ModifiedByID", "PeriodStart", "TeamID", "TeamPlayerID", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."DraftCaptainOrder", OLD."DraftPick", OLD."DraftRound", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."TeamID", OLD."TeamPlayerID", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Teams_History_Update" AFTER UPDATE ON "Teams" FOR EACH ROW BEGIN
                    INSERT INTO "TeamsHistory" ("CaptainID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedLosses", "RecordedWins", "SeasonID", "TeamID", "TeamName", "PeriodEnd") VALUES (OLD."CaptainID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedLosses", OLD."RecordedWins", OLD."SeasonID", OLD."TeamID", OLD."TeamName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Teams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Teams_History_Delete" AFTER DELETE ON "Teams" FOR EACH ROW BEGIN
                    INSERT INTO "TeamsHistory" ("CaptainID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedLosses", "RecordedWins", "SeasonID", "TeamID", "TeamName", "PeriodEnd") VALUES (OLD."CaptainID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedLosses", OLD."RecordedWins", OLD."SeasonID", OLD."TeamID", OLD."TeamName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "UserClaims_History_Update" AFTER UPDATE ON "UserClaims" FOR EACH ROW BEGIN
                    INSERT INTO "UserClaimsHistory" ("ClaimType", "ClaimValue", "CreatedAt", "CreatedByID", "Id", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserId", "PeriodEnd") VALUES (OLD."ClaimType", OLD."ClaimValue", OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "UserClaims" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "UserClaims_History_Delete" AFTER DELETE ON "UserClaims" FOR EACH ROW BEGIN
                    INSERT INTO "UserClaimsHistory" ("ClaimType", "ClaimValue", "CreatedAt", "CreatedByID", "Id", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserId", "PeriodEnd") VALUES (OLD."ClaimType", OLD."ClaimValue", OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "UserExperiences_History_Update" AFTER UPDATE ON "UserExperiences" FOR EACH ROW BEGIN
                    INSERT INTO "UserExperiencesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "UserExperiences" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "UserExperiences_History_Delete" AFTER DELETE ON "UserExperiences" FOR EACH ROW BEGIN
                    INSERT INTO "UserExperiencesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "UserLogins_History_Update" AFTER UPDATE ON "UserLogins" FOR EACH ROW BEGIN
                    INSERT INTO "UserLoginsHistory" ("CreatedAt", "CreatedByID", "LoginProvider", "ModifiedAt", "ModifiedByID", "PeriodStart", "ProviderDisplayName", "ProviderKey", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LoginProvider", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ProviderDisplayName", OLD."ProviderKey", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "UserLogins" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "UserLogins_History_Delete" AFTER DELETE ON "UserLogins" FOR EACH ROW BEGIN
                    INSERT INTO "UserLoginsHistory" ("CreatedAt", "CreatedByID", "LoginProvider", "ModifiedAt", "ModifiedByID", "PeriodStart", "ProviderDisplayName", "ProviderKey", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LoginProvider", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ProviderDisplayName", OLD."ProviderKey", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "UserRoles_History_Update" AFTER UPDATE ON "UserRoles" FOR EACH ROW BEGIN
                    INSERT INTO "UserRolesHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RoleId", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RoleId", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "UserRoles" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "UserRoles_History_Delete" AFTER DELETE ON "UserRoles" FOR EACH ROW BEGIN
                    INSERT INTO "UserRolesHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RoleId", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RoleId", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "UserTokens_History_Update" AFTER UPDATE ON "UserTokens" FOR EACH ROW BEGIN
                    INSERT INTO "UserTokensHistory" ("LoginProvider", "Name", "PeriodStart", "UserId", "Value", "PeriodEnd") VALUES (OLD."LoginProvider", OLD."Name", OLD."PeriodStart", OLD."UserId", OLD."Value", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "UserTokens" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "UserTokens_History_Delete" AFTER DELETE ON "UserTokens" FOR EACH ROW BEGIN
                    INSERT INTO "UserTokensHistory" ("LoginProvider", "Name", "PeriodStart", "UserId", "Value", "PeriodEnd") VALUES (OLD."LoginProvider", OLD."Name", OLD."PeriodStart", OLD."UserId", OLD."Value", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Users_History_Update" AFTER UPDATE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "RegionID", "RegionSource", "SecurityStamp", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Users" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Users_History_Delete" AFTER DELETE ON "Users" FOR EACH ROW BEGIN
                    INSERT INTO "UsersHistory" ("AccessFailedCount", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "DiscordUserID", "DisplayName", "Email", "EmailConfirmed", "Id", "IsDummyUser", "LockoutEnabled", "LockoutEnd", "LossStreak", "Losses", "MMR", "ModifiedAt", "ModifiedByID", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PeriodStart", "PhoneNumber", "PhoneNumberConfirmed", "RegionID", "RegionSource", "SecurityStamp", "TwoFactorEnabled", "UserName", "WinStreak", "Wins", "XboxUserID", "PeriodEnd") VALUES (OLD."AccessFailedCount", OLD."ConcurrencyStamp", OLD."CreatedAt", OLD."CreatedByID", OLD."DiscordUserID", OLD."DisplayName", OLD."Email", OLD."EmailConfirmed", OLD."Id", OLD."IsDummyUser", OLD."LockoutEnabled", OLD."LockoutEnd", OLD."LossStreak", OLD."Losses", OLD."MMR", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedEmail", OLD."NormalizedUserName", OLD."PasswordHash", OLD."PeriodStart", OLD."PhoneNumber", OLD."PhoneNumberConfirmed", OLD."RegionID", OLD."RegionSource", OLD."SecurityStamp", OLD."TwoFactorEnabled", OLD."UserName", OLD."WinStreak", OLD."Wins", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "VodTeams_History_Update" AFTER UPDATE ON "VodTeams" FOR EACH ROW BEGIN
                    INSERT INTO "VodTeamsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "TeamID", "VodID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."TeamID", OLD."VodID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "VodTeams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "VodTeams_History_Delete" AFTER DELETE ON "VodTeams" FOR EACH ROW BEGIN
                    INSERT INTO "VodTeamsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "TeamID", "VodID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."TeamID", OLD."VodID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Vods_History_Update" AFTER UPDATE ON "Vods" FOR EACH ROW BEGIN
                    INSERT INTO "VodsHistory" ("ChannelID", "CreatedAt", "CreatedByID", "Description", "DurationSeconds", "MatchNote", "MatchStatus", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublishedAt", "PublishedAtIsApproximate", "SeasonID", "SeasonMatchID", "Title", "VodID", "YouTubeVideoID", "PeriodEnd") VALUES (OLD."ChannelID", OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."DurationSeconds", OLD."MatchNote", OLD."MatchStatus", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublishedAt", OLD."PublishedAtIsApproximate", OLD."SeasonID", OLD."SeasonMatchID", OLD."Title", OLD."VodID", OLD."YouTubeVideoID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Vods" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Vods_History_Delete" AFTER DELETE ON "Vods" FOR EACH ROW BEGIN
                    INSERT INTO "VodsHistory" ("ChannelID", "CreatedAt", "CreatedByID", "Description", "DurationSeconds", "MatchNote", "MatchStatus", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublishedAt", "PublishedAtIsApproximate", "SeasonID", "SeasonMatchID", "Title", "VodID", "YouTubeVideoID", "PeriodEnd") VALUES (OLD."ChannelID", OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."DurationSeconds", OLD."MatchNote", OLD."MatchStatus", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublishedAt", OLD."PublishedAtIsApproximate", OLD."SeasonID", OLD."SeasonMatchID", OLD."Title", OLD."VodID", OLD."YouTubeVideoID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "XboxUsers_History_Update" AFTER UPDATE ON "XboxUsers" FOR EACH ROW BEGIN
                    INSERT INTO "XboxUsersHistory" ("CreatedAt", "CreatedByID", "Gamertag", "ModifiedAt", "ModifiedByID", "PeriodStart", "XboxUserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Gamertag", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "XboxUsers" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "XboxUsers_History_Delete" AFTER DELETE ON "XboxUsers" FOR EACH ROW BEGIN
                    INSERT INTO "XboxUsersHistory" ("CreatedAt", "CreatedByID", "Gamertag", "ModifiedAt", "ModifiedByID", "PeriodStart", "XboxUserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Gamertag", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "AvailabilityOptions_History_Update";
                DROP TRIGGER IF EXISTS "AvailabilityOptions_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "DataProtectionKeys_History_Update";
                DROP TRIGGER IF EXISTS "DataProtectionKeys_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Discord_History_Update";
                DROP TRIGGER IF EXISTS "Discord_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameParticipants_History_Update";
                DROP TRIGGER IF EXISTS "GameParticipants_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameTeams_History_Update";
                DROP TRIGGER IF EXISTS "GameTeams_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameVersions_History_Update";
                DROP TRIGGER IF EXISTS "GameVersions_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GamertagClaims_History_Update";
                DROP TRIGGER IF EXISTS "GamertagClaims_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Games_History_Update";
                DROP TRIGGER IF EXISTS "Games_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Leagues_History_Update";
                DROP TRIGGER IF EXISTS "Leagues_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchBracketInfo_History_Update";
                DROP TRIGGER IF EXISTS "MatchBracketInfo_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchParticipants_History_Update";
                DROP TRIGGER IF EXISTS "MatchParticipants_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchTeams_History_Update";
                DROP TRIGGER IF EXISTS "MatchTeams_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedKickVotes_History_Update";
                DROP TRIGGER IF EXISTS "MatchedKickVotes_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Update";
                DROP TRIGGER IF EXISTS "MatchedMatches_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedPlayers_History_Update";
                DROP TRIGGER IF EXISTS "MatchedPlayers_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedTeams_History_Update";
                DROP TRIGGER IF EXISTS "MatchedTeams_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedWinnerVotes_History_Update";
                DROP TRIGGER IF EXISTS "MatchedWinnerVotes_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Matches_History_Update";
                DROP TRIGGER IF EXISTS "Matches_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MedalDifficulties_History_Update";
                DROP TRIGGER IF EXISTS "MedalDifficulties_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MedalEarned_History_Update";
                DROP TRIGGER IF EXISTS "MedalEarned_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MedalTypes_History_Update";
                DROP TRIGGER IF EXISTS "MedalTypes_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Medals_History_Update";
                DROP TRIGGER IF EXISTS "Medals_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Messages_History_Update";
                DROP TRIGGER IF EXISTS "Messages_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PasswordResetLinks_History_Update";
                DROP TRIGGER IF EXISTS "PasswordResetLinks_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PastGamertags_History_Update";
                DROP TRIGGER IF EXISTS "PastGamertags_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PlayerChampionships_History_Update";
                DROP TRIGGER IF EXISTS "PlayerChampionships_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PlayerSeasonStats_History_Update";
                DROP TRIGGER IF EXISTS "PlayerSeasonStats_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "QueuedPlayers_History_Update";
                DROP TRIGGER IF EXISTS "QueuedPlayers_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Ranks_History_Update";
                DROP TRIGGER IF EXISTS "Ranks_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Regions_History_Update";
                DROP TRIGGER IF EXISTS "Regions_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "RoleClaims_History_Update";
                DROP TRIGGER IF EXISTS "RoleClaims_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Roles_History_Update";
                DROP TRIGGER IF EXISTS "Roles_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeasonAvailability_History_Update";
                DROP TRIGGER IF EXISTS "SeasonAvailability_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeasonMatches_History_Update";
                DROP TRIGGER IF EXISTS "SeasonMatches_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeasonSignups_History_Update";
                DROP TRIGGER IF EXISTS "SeasonSignups_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Seasons_History_Update";
                DROP TRIGGER IF EXISTS "Seasons_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeriesGames_History_Update";
                DROP TRIGGER IF EXISTS "SeriesGames_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SignupAvailability_History_Update";
                DROP TRIGGER IF EXISTS "SignupAvailability_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TeamAvailability_History_Update";
                DROP TRIGGER IF EXISTS "TeamAvailability_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TeamPlayers_History_Update";
                DROP TRIGGER IF EXISTS "TeamPlayers_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Teams_History_Update";
                DROP TRIGGER IF EXISTS "Teams_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "UserClaims_History_Update";
                DROP TRIGGER IF EXISTS "UserClaims_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "UserExperiences_History_Update";
                DROP TRIGGER IF EXISTS "UserExperiences_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "UserLogins_History_Update";
                DROP TRIGGER IF EXISTS "UserLogins_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "UserRoles_History_Update";
                DROP TRIGGER IF EXISTS "UserRoles_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "UserTokens_History_Update";
                DROP TRIGGER IF EXISTS "UserTokens_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Users_History_Update";
                DROP TRIGGER IF EXISTS "Users_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "VodTeams_History_Update";
                DROP TRIGGER IF EXISTS "VodTeams_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Vods_History_Update";
                DROP TRIGGER IF EXISTS "Vods_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "XboxUsers_History_Update";
                DROP TRIGGER IF EXISTS "XboxUsers_History_Delete";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MatchReschedules_Users_ApprovedByUserID",
                table: "MatchReschedules");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchReschedules_Users_RequestedByUserID",
                table: "MatchReschedules");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamPlayers_Users_UserID",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonMatches_Seasons_SeasonID",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Seasons_SeasonID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonMatches_Teams_AwayTeamID",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonMatches_Teams_HomeTeamID",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamPlayers_Teams_TeamID",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropForeignKey(
                name: "FK_MatchReschedules_SeasonMatches_SeasonMatchID",
                table: "MatchReschedules");

            migrationBuilder.DropTable(
                name: "AvailabilityOptionsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "DataProtectionKeysHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "DiscordHistory",
                schema: "User");

            migrationBuilder.DropTable(
                name: "DuplicateDismissals",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "GameParticipants",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "GameParticipantsHistory",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "GamertagClaims",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "GamertagClaimsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "GamesHistory",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "GameTeamsHistory",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "GameVersionsHistory",
                schema: "Other");

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
                name: "LeaguesHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchBracketInfo",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchBracketInfoHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchedKickVotes",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedKickVotesHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedMatchesHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedPlayersHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedTeamsHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedWinnerVotes",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedWinnerVotesHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchesHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MatchParticipantsHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MatchTeamsHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalDifficultiesHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalEarned",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalEarnedHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalsHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalTypesHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "Discord");

            migrationBuilder.DropTable(
                name: "MessagesHistory",
                schema: "Discord");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "Discord");

            migrationBuilder.DropTable(
                name: "PasswordResetLinks",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "PasswordResetLinksHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "PastGamertags",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "PastGamertagsHistory",
                schema: "Xbox");

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
                name: "QueuedPlayers",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "QueuedPlayersHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "Ranks",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "RanksHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "RegionsHistory",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "RoleClaimsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "RolesHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "SeasonAvailability",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonAvailabilityHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonMatchesHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonSignupsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeriesGames",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeriesGamesHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SignupAvailability",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SignupAvailabilityHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamAvailability",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamAvailabilityHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamPlayersHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserClaimsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserExperiences",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "UserExperiencesHistory",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserLoginsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserMerges",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserRolesHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UsersHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserTokensHistory",
                schema: "Auth");

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
                name: "XboxUsersHistory",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "XuidLookups",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "GameTeams",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "HistoricalImportJobs",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "HistoricalSources",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchedMatches",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedPlayers",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchParticipants",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "Medals",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "SeasonSignups",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "AvailabilityOptions",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "Vods",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "Games",
                schema: "Recorded");

            migrationBuilder.DropTable(
                name: "MatchedTeams",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchTeams",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalDifficulties",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalTypes",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "Matches",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "Discord",
                schema: "User");

            migrationBuilder.DropTable(
                name: "Regions",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "XboxUsers",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "Seasons",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "GameVersions",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "Leagues",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "Teams",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamPlayers",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonMatches",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchReschedules");
        }
    }
}
