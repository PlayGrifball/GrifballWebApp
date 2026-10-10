using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRowHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XboxUsers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserTokens",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "Users",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserRoles",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserLogins",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Other",
                table: "UserExperiences",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserClaims",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "Teams",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamPlayers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamAvailability",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SignupAvailability",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonSignups",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "Seasons",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonMatches",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonAvailability",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "Roles",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "RoleClaims",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Other",
                table: "Regions",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "Ranks",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "QueuedPlayers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Discord",
                table: "Messages",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalTypes",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Medals",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalEarned",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalDifficulties",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchTeams",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchLinks",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Matches",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedTeams",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchBracketInfo",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Other",
                table: "GameVersions",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "User",
                table: "Discord",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "DataProtectionKeys",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "AvailabilityOptions",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'UTC'");

            migrationBuilder.CreateTable(
                name: "AvailabilityOptionsHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityOptionsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeysHistory",
                schema: "Auth",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    Xml = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeysHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "DiscordHistory",
                schema: "User",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: false),
                    DiscordUsername = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "GameVersionsHistory",
                schema: "Other",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    GameVersionName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu"),
                    GameVesionID = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameVersionsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchBracketInfoHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AwayTeamPreviousMatchBracketInfoID = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamSeedNumber = table.Column<int>(type: "integer", nullable: true),
                    Bracket = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamPreviousMatchBracketInfoID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamSeedNumber = table.Column<int>(type: "integer", nullable: true),
                    MatchBracketInfoID = table.Column<int>(type: "integer", nullable: false),
                    MatchNumber = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    RoundNumber = table.Column<int>(type: "integer", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchBracketInfoHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchedKickVotesHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    KickMatchedPlayerId = table.Column<int>(type: "integer", nullable: true),
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    VoterMatchedPlayerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedKickVotesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchedMatchesHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Active = table.Column<bool>(type: "boolean", nullable: true),
                    AwayTeamId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamId = table.Column<int>(type: "integer", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ThreadID = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    VoteMessageID = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedMatchesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchedPlayersHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Kicked = table.Column<bool>(type: "boolean", nullable: true),
                    MatchedTeamID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedPlayersHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchedTeamsHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchedTeamId = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedTeamsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchedWinnerVotesHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    MatchedPlayerId = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    WinnerVote = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedWinnerVotesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchesHistory",
                schema: "Infinite",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StatsPullDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchLinksHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchLinkID = table.Column<int>(type: "integer", nullable: false),
                    MatchNumber = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLinksHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchParticipantsHistory",
                schema: "Infinite",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Accuracy = table.Column<float>(type: "real", nullable: true),
                    Assists = table.Column<int>(type: "integer", nullable: true),
                    AverageLife = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Betrayals = table.Column<int>(type: "integer", nullable: true),
                    CalloutAssists = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DamageDealt = table.Column<int>(type: "integer", nullable: true),
                    DamageTaken = table.Column<int>(type: "integer", nullable: true),
                    Deaths = table.Column<int>(type: "integer", nullable: true),
                    FirstJoinedTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    JoinedInProgress = table.Column<bool>(type: "boolean", nullable: true),
                    Kda = table.Column<float>(type: "real", nullable: true),
                    Kills = table.Column<int>(type: "integer", nullable: true),
                    LastLeaveTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LeftInProgress = table.Column<bool>(type: "boolean", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxKillingSpree = table.Column<int>(type: "integer", nullable: true),
                    MeleeKills = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    ObjectivesCompleted = table.Column<int>(type: "integer", nullable: true),
                    PersonalScore = table.Column<int>(type: "integer", nullable: true),
                    PowerWeaponKills = table.Column<int>(type: "integer", nullable: true),
                    PresentAtBeginning = table.Column<bool>(type: "boolean", nullable: true),
                    PresentAtCompletion = table.Column<bool>(type: "boolean", nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    RoundsLost = table.Column<int>(type: "integer", nullable: true),
                    RoundsTied = table.Column<int>(type: "integer", nullable: true),
                    RoundsWon = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    ShotsFired = table.Column<int>(type: "integer", nullable: true),
                    ShotsHit = table.Column<int>(type: "integer", nullable: true),
                    Spawns = table.Column<int>(type: "integer", nullable: true),
                    Suicides = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    TimePlayed = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchParticipantsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MatchTeamsHistory",
                schema: "Infinite",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchTeamsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MedalDifficultiesHistory",
                schema: "Infinite",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MedalDifficultyID = table.Column<int>(type: "integer", nullable: false),
                    MedalDifficultyName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalDifficultiesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MedalEarnedHistory",
                schema: "Infinite",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Count = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    MedalID = table.Column<long>(type: "bigint", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    TotalPersonalScoreAwarded = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalEarnedHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MedalsHistory",
                schema: "Infinite",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    MedalDifficultyID = table.Column<int>(type: "integer", nullable: true),
                    MedalID = table.Column<long>(type: "bigint", nullable: false),
                    MedalName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    MedalTypeID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PersonalScore = table.Column<int>(type: "integer", nullable: true),
                    SortingWeight = table.Column<int>(type: "integer", nullable: true),
                    SpriteIndex = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MedalTypesHistory",
                schema: "Infinite",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MedalTypeID = table.Column<int>(type: "integer", nullable: false),
                    MedalTypeName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalTypesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "MessagesHistory",
                schema: "Discord",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Content = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    FromDiscordUserId = table.Column<long>(type: "bigint", nullable: true),
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ToDiscordUserId = table.Column<long>(type: "bigint", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetLinksHistory",
                schema: "Auth",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, collation: "und-x-icu"),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetLinksHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "QueuedPlayersHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedPlayersHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "RanksHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Color = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Icon = table.Column<byte[]>(type: "bytea", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MmrThreshold = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RanksHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "RegionsHistory",
                schema: "Other",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    RegionID = table.Column<int>(type: "integer", nullable: false),
                    RegionName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "und-x-icu"),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaimsHistory",
                schema: "Auth",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimType = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ClaimValue = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaimsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "RolesHistory",
                schema: "Auth",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "SeasonAvailabilityHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonAvailabilityHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "SeasonMatchesHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActiveRescheduleRequestId = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamID = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamResult = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamScore = table.Column<int>(type: "integer", nullable: true),
                    BestOf = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamResult = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamScore = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    ScheduledTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonMatchesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "SeasonsHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CaptainsLocked = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DraftStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PublicAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SeasonEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    SeasonName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu"),
                    SeasonStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SignupsClose = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SignupsOpen = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "SeasonSignupsHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Approved = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    RequiresAssistanceDrafting = table.Column<bool>(type: "boolean", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    SeasonSignupID = table.Column<int>(type: "integer", nullable: false),
                    TeamName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    WillCaptain = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonSignupsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "SignupAvailabilityHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    SeasonSignupID = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignupAvailabilityHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "TeamAvailabilityHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamAvailabilityHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "TeamPlayersHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DraftCaptainOrder = table.Column<int>(type: "integer", nullable: true),
                    DraftPick = table.Column<int>(type: "integer", nullable: true),
                    DraftRound = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    TeamPlayerID = table.Column<int>(type: "integer", nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamPlayersHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "TeamsHistory",
                schema: "Event",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CaptainID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: false),
                    TeamName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu"),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "UserClaimsHistory",
                schema: "Auth",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimType = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ClaimValue = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaimsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "UserExperiencesHistory",
                schema: "Other",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    GameVersionID = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExperiencesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "UserLoginsHistory",
                schema: "Auth",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    LoginProvider = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ProviderKey = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginsHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "UserRolesHistory",
                schema: "Auth",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRolesHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "UsersHistory",
                schema: "Auth",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    IsDummyUser = table.Column<bool>(type: "boolean", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LossStreak = table.Column<int>(type: "integer", nullable: true),
                    Losses = table.Column<int>(type: "integer", nullable: true),
                    MMR = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    PasswordHash = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: true),
                    RegionID = table.Column<int>(type: "integer", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    WinStreak = table.Column<int>(type: "integer", nullable: true),
                    Wins = table.Column<int>(type: "integer", nullable: true),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsersHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "UserTokensHistory",
                schema: "Auth",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoginProvider = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokensHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateTable(
                name: "XboxUsersHistory",
                schema: "Xbox",
                columns: table => new
                {
                    HistoryID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Gamertag = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Valid = table.Column<NpgsqlRange<DateTime>>(type: "tsrange", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XboxUsersHistory", x => x.HistoryID);
                });

            migrationBuilder.CreateIndex(
                name: "AK_AvailabilityOptionsHistory_Valid",
                schema: "Event",
                table: "AvailabilityOptionsHistory",
                columns: new[] { "AvailabilityOptionID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_DataProtectionKeysHistory_Valid",
                schema: "Auth",
                table: "DataProtectionKeysHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_DiscordHistory_Valid",
                schema: "User",
                table: "DiscordHistory",
                columns: new[] { "DiscordUserID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_GameVersionsHistory_Valid",
                schema: "Other",
                table: "GameVersionsHistory",
                columns: new[] { "GameVesionID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchBracketInfoHistory_Valid",
                schema: "Event",
                table: "MatchBracketInfoHistory",
                columns: new[] { "MatchBracketInfoID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchedKickVotesHistory_Valid",
                schema: "Matchmaking",
                table: "MatchedKickVotesHistory",
                columns: new[] { "MatchId", "VoterMatchedPlayerId", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchedMatchesHistory_Valid",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchedPlayersHistory_Valid",
                schema: "Matchmaking",
                table: "MatchedPlayersHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchedTeamsHistory_Valid",
                schema: "Matchmaking",
                table: "MatchedTeamsHistory",
                columns: new[] { "MatchedTeamId", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchedWinnerVotesHistory_Valid",
                schema: "Matchmaking",
                table: "MatchedWinnerVotesHistory",
                columns: new[] { "MatchId", "MatchedPlayerId", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchesHistory_Valid",
                schema: "Infinite",
                table: "MatchesHistory",
                columns: new[] { "MatchID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchLinksHistory_Valid",
                schema: "Event",
                table: "MatchLinksHistory",
                columns: new[] { "MatchLinkID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchParticipantsHistory_Valid",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                columns: new[] { "MatchID", "XboxUserID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MatchTeamsHistory_Valid",
                schema: "Infinite",
                table: "MatchTeamsHistory",
                columns: new[] { "MatchID", "TeamID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MedalDifficultiesHistory_Valid",
                schema: "Infinite",
                table: "MedalDifficultiesHistory",
                columns: new[] { "MedalDifficultyID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MedalEarnedHistory_Valid",
                schema: "Infinite",
                table: "MedalEarnedHistory",
                columns: new[] { "MedalID", "MatchID", "XboxUserID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MedalsHistory_Valid",
                schema: "Infinite",
                table: "MedalsHistory",
                columns: new[] { "MedalID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MedalTypesHistory_Valid",
                schema: "Infinite",
                table: "MedalTypesHistory",
                columns: new[] { "MedalTypeID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_MessagesHistory_Valid",
                schema: "Discord",
                table: "MessagesHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_PasswordResetLinksHistory_Valid",
                schema: "Auth",
                table: "PasswordResetLinksHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_QueuedPlayersHistory_Valid",
                schema: "Matchmaking",
                table: "QueuedPlayersHistory",
                columns: new[] { "UserID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_RanksHistory_Valid",
                schema: "Matchmaking",
                table: "RanksHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_RegionsHistory_Valid",
                schema: "Other",
                table: "RegionsHistory",
                columns: new[] { "RegionID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_RoleClaimsHistory_Valid",
                schema: "Auth",
                table: "RoleClaimsHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_RolesHistory_Valid",
                schema: "Auth",
                table: "RolesHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_SeasonAvailabilityHistory_Valid",
                schema: "Event",
                table: "SeasonAvailabilityHistory",
                columns: new[] { "SeasonID", "AvailabilityOptionID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_SeasonMatchesHistory_Valid",
                schema: "Event",
                table: "SeasonMatchesHistory",
                columns: new[] { "SeasonMatchID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_SeasonsHistory_Valid",
                schema: "Event",
                table: "SeasonsHistory",
                columns: new[] { "SeasonID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_SeasonSignupsHistory_Valid",
                schema: "Event",
                table: "SeasonSignupsHistory",
                columns: new[] { "SeasonSignupID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_SignupAvailabilityHistory_Valid",
                schema: "Event",
                table: "SignupAvailabilityHistory",
                columns: new[] { "SeasonSignupID", "AvailabilityOptionID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_TeamAvailabilityHistory_Valid",
                schema: "Event",
                table: "TeamAvailabilityHistory",
                columns: new[] { "TeamID", "AvailabilityOptionID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_TeamPlayersHistory_Valid",
                schema: "Event",
                table: "TeamPlayersHistory",
                columns: new[] { "TeamPlayerID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_TeamsHistory_Valid",
                schema: "Event",
                table: "TeamsHistory",
                columns: new[] { "TeamID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_UserClaimsHistory_Valid",
                schema: "Auth",
                table: "UserClaimsHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_UserExperiencesHistory_Valid",
                schema: "Other",
                table: "UserExperiencesHistory",
                columns: new[] { "UserID", "GameVersionID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_UserLoginsHistory_Valid",
                schema: "Auth",
                table: "UserLoginsHistory",
                columns: new[] { "LoginProvider", "ProviderKey", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_UserRolesHistory_Valid",
                schema: "Auth",
                table: "UserRolesHistory",
                columns: new[] { "UserId", "RoleId", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_UsersHistory_Valid",
                schema: "Auth",
                table: "UsersHistory",
                columns: new[] { "Id", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_UserTokensHistory_Valid",
                schema: "Auth",
                table: "UserTokensHistory",
                columns: new[] { "UserId", "LoginProvider", "Name", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "AK_XboxUsersHistory_Valid",
                schema: "Xbox",
                table: "XboxUsersHistory",
                columns: new[] { "XboxUserID", "Valid" })
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvailabilityOptionsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "DataProtectionKeysHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "DiscordHistory",
                schema: "User");

            migrationBuilder.DropTable(
                name: "GameVersionsHistory",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "MatchBracketInfoHistory",
                schema: "Event");

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
                name: "MatchedWinnerVotesHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchesHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MatchLinksHistory",
                schema: "Event");

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
                name: "MedalEarnedHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalsHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalTypesHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MessagesHistory",
                schema: "Discord");

            migrationBuilder.DropTable(
                name: "PasswordResetLinksHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "QueuedPlayersHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "RanksHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "RegionsHistory",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "RoleClaimsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "RolesHistory",
                schema: "Auth");

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
                name: "SignupAvailabilityHistory",
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
                name: "UserClaimsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserExperiencesHistory",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "UserLoginsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserRolesHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UsersHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserTokensHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "XboxUsersHistory",
                schema: "Xbox");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XboxUsers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserTokens");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Other",
                table: "UserExperiences");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserClaims");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SignupAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "RoleClaims");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "QueuedPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Discord",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalTypes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Medals");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalEarned");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalDifficulties");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchTeams");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedTeams");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "DataProtectionKeys");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "AvailabilityOptions");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");
        }
    }
}
