using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityOptions", x => x.AvailabilityOptionID);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Xml = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Discord",
                schema: "User",
                columns: table => new
                {
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: false),
                    DiscordUsername = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discord", x => x.DiscordUserID);
                });

            migrationBuilder.CreateTable(
                name: "GameVersions",
                schema: "Other",
                columns: table => new
                {
                    GameVesionID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameVersionName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, collation: "und-x-icu"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameVersions", x => x.GameVesionID);
                });

            migrationBuilder.CreateTable(
                name: "MatchedTeams",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchedTeamId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedTeams", x => x.MatchedTeamId);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                schema: "Infinite",
                columns: table => new
                {
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    StatsPullDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.MatchID);
                });

            migrationBuilder.CreateTable(
                name: "MedalDifficulties",
                schema: "Infinite",
                columns: table => new
                {
                    MedalDifficultyID = table.Column<int>(type: "integer", nullable: false),
                    MedalDifficultyName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalDifficulties", x => x.MedalDifficultyID);
                });

            migrationBuilder.CreateTable(
                name: "MedalTypes",
                schema: "Infinite",
                columns: table => new
                {
                    MedalTypeID = table.Column<int>(type: "integer", nullable: false),
                    MedalTypeName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalTypes", x => x.MedalTypeID);
                });

            migrationBuilder.CreateTable(
                name: "Ranks",
                schema: "Matchmaking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    MmrThreshold = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    Description = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    Icon = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ranks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                schema: "Other",
                columns: table => new
                {
                    RegionID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegionName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "und-x-icu"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.RegionID);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                schema: "Event",
                columns: table => new
                {
                    SeasonID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, collation: "und-x-icu"),
                    CaptainsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    PublicAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SignupsOpen = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SignupsClose = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DraftStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SeasonStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SeasonEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.SeasonID);
                });

            migrationBuilder.CreateTable(
                name: "XboxUsers",
                schema: "Xbox",
                columns: table => new
                {
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    Gamertag = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XboxUsers", x => x.XboxUserID);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "Discord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FromDiscordUserId = table.Column<long>(type: "bigint", nullable: false),
                    ToDiscordUserId = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                name: "MatchedMatches",
                schema: "Matchmaking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HomeTeamId = table.Column<int>(type: "integer", nullable: false),
                    AwayTeamId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    ThreadID = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    VoteMessageID = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamID = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    MedalID = table.Column<long>(type: "bigint", nullable: false),
                    MedalName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    SpriteIndex = table.Column<int>(type: "integer", nullable: false),
                    SortingWeight = table.Column<int>(type: "integer", nullable: false),
                    MedalDifficultyID = table.Column<int>(type: "integer", nullable: false),
                    MedalTypeID = table.Column<int>(type: "integer", nullable: false),
                    PersonalScore = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ClaimValue = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
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
                name: "SeasonAvailability",
                schema: "Event",
                columns: table => new
                {
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                name: "Users",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegionID = table.Column<int>(type: "integer", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu"),
                    IsDummyUser = table.Column<bool>(type: "boolean", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true),
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: true),
                    MMR = table.Column<int>(type: "integer", nullable: false),
                    WinStreak = table.Column<int>(type: "integer", nullable: false),
                    LossStreak = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
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
                name: "MatchParticipants",
                schema: "Infinite",
                columns: table => new
                {
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamID = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    PersonalScore = table.Column<int>(type: "integer", nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Deaths = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    Kda = table.Column<float>(type: "real", nullable: false),
                    Suicides = table.Column<int>(type: "integer", nullable: false),
                    Betrayals = table.Column<int>(type: "integer", nullable: false),
                    AverageLife = table.Column<TimeSpan>(type: "interval", nullable: false),
                    MeleeKills = table.Column<int>(type: "integer", nullable: false),
                    PowerWeaponKills = table.Column<int>(type: "integer", nullable: false),
                    ShotsFired = table.Column<int>(type: "integer", nullable: false),
                    ShotsHit = table.Column<int>(type: "integer", nullable: false),
                    Accuracy = table.Column<float>(type: "real", nullable: false),
                    DamageDealt = table.Column<int>(type: "integer", nullable: false),
                    CalloutAssists = table.Column<int>(type: "integer", nullable: false),
                    MaxKillingSpree = table.Column<int>(type: "integer", nullable: false),
                    DamageTaken = table.Column<int>(type: "integer", nullable: false),
                    FirstJoinedTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastLeaveTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    JoinedInProgress = table.Column<bool>(type: "boolean", nullable: false),
                    LeftInProgress = table.Column<bool>(type: "boolean", nullable: false),
                    PresentAtBeginning = table.Column<bool>(type: "boolean", nullable: false),
                    PresentAtCompletion = table.Column<bool>(type: "boolean", nullable: false),
                    TimePlayed = table.Column<TimeSpan>(type: "interval", nullable: false),
                    RoundsWon = table.Column<int>(type: "integer", nullable: false),
                    RoundsLost = table.Column<int>(type: "integer", nullable: false),
                    RoundsTied = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Spawns = table.Column<int>(type: "integer", nullable: false),
                    ObjectivesCompleted = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    Kicked = table.Column<bool>(type: "boolean", nullable: false),
                    MatchedTeamID = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, collation: "und-x-icu"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    SeasonSignupID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TeamName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    WillCaptain = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresAssistanceDrafting = table.Column<bool>(type: "boolean", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ClaimValue = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
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
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    GameVersionID = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    LoginProvider = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    ProviderKey = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    UserId = table.Column<int>(type: "integer", nullable: false)
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
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "und-x-icu"),
                    Value = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
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
                name: "MedalEarned",
                schema: "Infinite",
                columns: table => new
                {
                    MedalID = table.Column<long>(type: "bigint", nullable: false),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    TotalPersonalScoreAwarded = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    VoterMatchedPlayerId = table.Column<int>(type: "integer", nullable: false),
                    KickMatchedPlayerId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    MatchId = table.Column<int>(type: "integer", nullable: false),
                    MatchedPlayerId = table.Column<int>(type: "integer", nullable: false),
                    WinnerVote = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    SeasonSignupID = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                name: "MatchBracketInfo",
                schema: "Event",
                columns: table => new
                {
                    MatchBracketInfoID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    MatchNumber = table.Column<int>(type: "integer", nullable: false),
                    HomeTeamSeedNumber = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamPreviousMatchBracketInfoID = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamSeedNumber = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamPreviousMatchBracketInfoID = table.Column<int>(type: "integer", nullable: true),
                    Bracket = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchBracketInfo", x => x.MatchBracketInfoID);
                    table.CheckConstraint("CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch", "(\"AwayTeamSeedNumber\" IS NOT NULL AND \"AwayTeamPreviousMatchBracketInfoID\" IS NULL) OR (\"AwayTeamPreviousMatchBracketInfoID\" IS NOT NULL AND \"AwayTeamSeedNumber\" IS NULL)");
                    table.CheckConstraint("CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch", "(\"HomeTeamSeedNumber\" IS NOT NULL AND \"HomeTeamPreviousMatchBracketInfoID\" IS NULL) OR (\"HomeTeamPreviousMatchBracketInfoID\" IS NOT NULL AND \"HomeTeamSeedNumber\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_MatchBracketInfo_MatchBracketInfo_AwayTeamPreviousMatchBrac~",
                        column: x => x.AwayTeamPreviousMatchBracketInfoID,
                        principalSchema: "Event",
                        principalTable: "MatchBracketInfo",
                        principalColumn: "MatchBracketInfoID");
                    table.ForeignKey(
                        name: "FK_MatchBracketInfo_MatchBracketInfo_HomeTeamPreviousMatchBrac~",
                        column: x => x.HomeTeamPreviousMatchBracketInfoID,
                        principalSchema: "Event",
                        principalTable: "MatchBracketInfo",
                        principalColumn: "MatchBracketInfoID");
                });

            migrationBuilder.CreateTable(
                name: "MatchLinks",
                schema: "Event",
                columns: table => new
                {
                    MatchLinkID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: false),
                    MatchNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "MatchReschedules",
                columns: table => new
                {
                    MatchRescheduleID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: false),
                    OriginalScheduledTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NewScheduledTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false, collation: "und-x-icu"),
                    RequestedByUserID = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserID = table.Column<int>(type: "integer", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DiscordThreadID = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CommissionerNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "und-x-icu"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                name: "SeasonMatches",
                schema: "Event",
                columns: table => new
                {
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    HomeTeamID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamScore = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamResult = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamID = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamScore = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamResult = table.Column<int>(type: "integer", nullable: true),
                    ScheduledTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BestOf = table.Column<int>(type: "integer", nullable: false),
                    ActiveRescheduleRequestId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonMatches", x => x.SeasonMatchID);
                    table.CheckConstraint("CK_Event_SeasonMatches_MustBeDifferentTeams", "(\"HomeTeamID\" IS NULL) OR (\"AwayTeamID\" IS NULL) OR (\"HomeTeamID\" != \"AwayTeamID\")");
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
                name: "TeamAvailability",
                schema: "Event",
                columns: table => new
                {
                    TeamID = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: false),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    TeamPlayerID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamID = table.Column<int>(type: "integer", nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    DraftCaptainOrder = table.Column<int>(type: "integer", nullable: true),
                    DraftRound = table.Column<int>(type: "integer", nullable: true),
                    DraftPick = table.Column<int>(type: "integer", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    TeamID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    TeamName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu"),
                    CaptainID = table.Column<int>(type: "integer", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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

            migrationBuilder.InsertData(
                schema: "Other",
                table: "GameVersions",
                columns: new[] { "GameVesionID", "CreatedAt", "CreatedByID", "GameVersionName", "ModifiedAt", "ModifiedByID" },
                values: new object[,]
                {
                    { 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Halo 3", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null },
                    { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Halo Reach", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null },
                    { 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Halo Reach Dash", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null },
                    { 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Halo 4", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null },
                    { 5, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Halo 5", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null },
                    { 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Halo Infinite", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null }
                });

            migrationBuilder.InsertData(
                schema: "Other",
                table: "Regions",
                columns: new[] { "RegionID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "RegionName" },
                values: new object[,]
                {
                    { 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "West North America" },
                    { 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Central North America" },
                    { 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "East North America" },
                    { 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "North Europe" },
                    { 5, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "South Europe" },
                    { 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Australia" }
                });

            migrationBuilder.InsertData(
                schema: "Auth",
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { 1, "cda78946-53d4-4154-b723-2b33b95341cc", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Sysadmin", "SYSADMIN" },
                    { 2, "37ff5fdf-5cb8-403c-bb9e-81db05a5fcdd", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Commissioner", "COMMISSIONER" },
                    { 3, "0be992c7-f824-40c7-8975-964068c7fbfe", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "Player", "PLAYER" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityOptions_DayOfWeek_Time",
                schema: "Event",
                table: "AvailabilityOptions",
                columns: new[] { "DayOfWeek", "Time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameVersions_GameVersionName",
                schema: "Other",
                table: "GameVersions",
                column: "GameVersionName",
                unique: true);

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
                name: "IX_MatchedWinnerVotes_MatchedPlayerId",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                column: "MatchedPlayerId");

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
                name: "IX_MedalEarned_MatchID_XboxUserID",
                schema: "Infinite",
                table: "MedalEarned",
                columns: new[] { "MatchID", "XboxUserID" });

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
                name: "IX_Regions_RegionName",
                schema: "Other",
                table: "Regions",
                column: "RegionName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "Auth",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "Auth",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAvailability_AvailabilityOptionID",
                schema: "Event",
                table: "SeasonAvailability",
                column: "AvailabilityOptionID");

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
                name: "IX_Seasons_SeasonName",
                schema: "Event",
                table: "Seasons",
                column: "SeasonName",
                unique: true);

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
                name: "IX_SignupAvailability_AvailabilityOptionID",
                schema: "Event",
                table: "SignupAvailability",
                column: "AvailabilityOptionID");

            migrationBuilder.CreateIndex(
                name: "IX_TeamAvailability_AvailabilityOptionID",
                schema: "Event",
                table: "TeamAvailability",
                column: "AvailabilityOptionID");

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
                name: "IX_UserClaims_UserId",
                schema: "Auth",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserExperiences_GameVersionID",
                schema: "Other",
                table: "UserExperiences",
                column: "GameVersionID");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "Auth",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "Auth",
                table: "UserRoles",
                column: "RoleId");

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
                name: "FK_MatchLinks_SeasonMatches_SeasonMatchID",
                schema: "Event",
                table: "MatchLinks",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchReschedules_SeasonMatches_SeasonMatchID",
                table: "MatchReschedules");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamPlayers_Users_UserID",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Seasons_SeasonID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamPlayers_Teams_TeamID",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "MatchBracketInfo",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchedKickVotes",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedWinnerVotes",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchLinks",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MedalEarned",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "Discord");

            migrationBuilder.DropTable(
                name: "PasswordResetLinks",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "QueuedPlayers",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "Ranks",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "SeasonAvailability",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SignupAvailability",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamAvailability",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserExperiences",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "Auth");

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
                name: "GameVersions",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "Auth");

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
                name: "SeasonMatches",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchReschedules");

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
                name: "Teams",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamPlayers",
                schema: "Event");
        }
    }
}
