using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Event");

            migrationBuilder.EnsureSchema(
                name: "Auth");

            migrationBuilder.EnsureSchema(
                name: "Other");

            migrationBuilder.EnsureSchema(
                name: "Infinite");

            migrationBuilder.EnsureSchema(
                name: "Xbox");

            migrationBuilder.CreateTable(
                name: "AvailabilityOptions",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityOptions", x => x.AvailabilityOptionID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AvailabilityOptionsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "DataProtectionKeysHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "GameVersions",
                schema: "Other",
                columns: table => new
                {
                    GameVesionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameVersionName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameVersions", x => x.GameVesionID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GameVersionsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Other")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Matches",
                schema: "Infinite",
                columns: table => new
                {
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "time", nullable: false),
                    StatsPullDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.MatchID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MedalDifficulties",
                schema: "Infinite",
                columns: table => new
                {
                    MedalDifficultyID = table.Column<int>(type: "int", nullable: false),
                    MedalDifficultyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalDifficulties", x => x.MedalDifficultyID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MedalDifficultiesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MedalTypes",
                schema: "Infinite",
                columns: table => new
                {
                    MedalTypeID = table.Column<int>(type: "int", nullable: false),
                    MedalTypeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedalTypes", x => x.MedalTypeID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MedalTypesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Regions",
                schema: "Other",
                columns: table => new
                {
                    RegionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.RegionID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "RegionsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Other")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "RolesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Seasons",
                schema: "Event",
                columns: table => new
                {
                    SeasonID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CaptainsLocked = table.Column<bool>(type: "bit", nullable: false),
                    SignupsOpen = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignupsClose = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DraftStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SeasonStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SeasonEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.SeasonID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "XboxUsers",
                schema: "Xbox",
                columns: table => new
                {
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    Gamertag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XboxUsers", x => x.XboxUserID);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "XboxUsersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchTeams",
                schema: "Infinite",
                columns: table => new
                {
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamID = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Medals",
                schema: "Infinite",
                columns: table => new
                {
                    MedalID = table.Column<long>(type: "bigint", nullable: false),
                    MedalName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpriteIndex = table.Column<int>(type: "int", nullable: false),
                    SortingWeight = table.Column<int>(type: "int", nullable: false),
                    MedalDifficultyID = table.Column<int>(type: "int", nullable: false),
                    MedalTypeID = table.Column<int>(type: "int", nullable: false),
                    PersonalScore = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "RoleClaimsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "SeasonAvailability",
                schema: "Event",
                columns: table => new
                {
                    SeasonID = table.Column<int>(type: "int", nullable: false),
                    AvailabilityOptionID = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonAvailabilityHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegionID = table.Column<int>(type: "int", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true),
                    IsDummyUser = table.Column<bool>(type: "bit", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UsersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchParticipants",
                schema: "Infinite",
                columns: table => new
                {
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamID = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    PersonalScore = table.Column<int>(type: "int", nullable: false),
                    Kills = table.Column<int>(type: "int", nullable: false),
                    Deaths = table.Column<int>(type: "int", nullable: false),
                    Assists = table.Column<int>(type: "int", nullable: false),
                    Kda = table.Column<float>(type: "real", nullable: false),
                    Suicides = table.Column<int>(type: "int", nullable: false),
                    Betrayals = table.Column<int>(type: "int", nullable: false),
                    AverageLife = table.Column<TimeSpan>(type: "time", nullable: false),
                    MeleeKills = table.Column<int>(type: "int", nullable: false),
                    PowerWeaponKills = table.Column<int>(type: "int", nullable: false),
                    ShotsFired = table.Column<int>(type: "int", nullable: false),
                    ShotsHit = table.Column<int>(type: "int", nullable: false),
                    Accuracy = table.Column<float>(type: "real", nullable: false),
                    DamageDealt = table.Column<int>(type: "int", nullable: false),
                    CalloutAssists = table.Column<int>(type: "int", nullable: false),
                    MaxKillingSpree = table.Column<int>(type: "int", nullable: false),
                    DamageTaken = table.Column<int>(type: "int", nullable: false),
                    FirstJoinedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLeaveTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JoinedInProgress = table.Column<bool>(type: "bit", nullable: false),
                    LeftInProgress = table.Column<bool>(type: "bit", nullable: false),
                    PresentAtBeginning = table.Column<bool>(type: "bit", nullable: false),
                    PresentAtCompletion = table.Column<bool>(type: "bit", nullable: false),
                    TimePlayed = table.Column<TimeSpan>(type: "time", nullable: false),
                    RoundsWon = table.Column<int>(type: "int", nullable: false),
                    RoundsLost = table.Column<int>(type: "int", nullable: false),
                    RoundsTied = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "SeasonSignups",
                schema: "Event",
                columns: table => new
                {
                    SeasonSignupID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    SeasonID = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WillCaptain = table.Column<bool>(type: "bit", nullable: false),
                    RequiresAssistanceDrafting = table.Column<bool>(type: "bit", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonSignupsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "Auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserClaimsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "UserExperiences",
                schema: "Other",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false),
                    GameVersionID = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserExperiencesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Other")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "Auth",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserLoginsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "Auth",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserRolesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "Auth",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserTokensHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MedalEarned",
                schema: "Infinite",
                columns: table => new
                {
                    MedalID = table.Column<long>(type: "bigint", nullable: false),
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    TotalPersonalScoreAwarded = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MedalEarnedHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "SignupAvailability",
                schema: "Event",
                columns: table => new
                {
                    SeasonSignupID = table.Column<int>(type: "int", nullable: false),
                    AvailabilityOptionID = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SignupAvailabilityHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchBracketInfo",
                schema: "Event",
                columns: table => new
                {
                    MatchBracketInfoID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonMatchID = table.Column<int>(type: "int", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    MatchNumber = table.Column<int>(type: "int", nullable: false),
                    HomeTeamSeedNumber = table.Column<int>(type: "int", nullable: true),
                    HomeTeamPreviousMatchBracketInfoID = table.Column<int>(type: "int", nullable: true),
                    AwayTeamSeedNumber = table.Column<int>(type: "int", nullable: true),
                    AwayTeamPreviousMatchBracketInfoID = table.Column<int>(type: "int", nullable: true),
                    Bracket = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchBracketInfo", x => x.MatchBracketInfoID);
                    table.CheckConstraint("CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch", "\r\n(AwayTeamSeedNumber IS NOT NULL AND AwayTeamPreviousMatchBracketInfoID IS NULL) OR\r\n(AwayTeamPreviousMatchBracketInfoID IS NOT NULL AND AwayTeamSeedNumber IS NULL)\r\n");
                    table.CheckConstraint("CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch", "\r\n(HomeTeamSeedNumber IS NOT NULL AND HomeTeamPreviousMatchBracketInfoID IS NULL) OR\r\n(HomeTeamPreviousMatchBracketInfoID IS NOT NULL AND HomeTeamSeedNumber IS NULL)\r\n");
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchBracketInfoHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchLinks",
                schema: "Event",
                columns: table => new
                {
                    MatchLinkID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonMatchID = table.Column<int>(type: "int", nullable: false),
                    MatchNumber = table.Column<int>(type: "int", nullable: false),
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "SeasonMatches",
                schema: "Event",
                columns: table => new
                {
                    SeasonMatchID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonID = table.Column<int>(type: "int", nullable: false),
                    HomeTeamID = table.Column<int>(type: "int", nullable: true),
                    HomeTeamScore = table.Column<int>(type: "int", nullable: true),
                    HomeTeamResult = table.Column<int>(type: "int", nullable: true),
                    AwayTeamID = table.Column<int>(type: "int", nullable: true),
                    AwayTeamScore = table.Column<int>(type: "int", nullable: true),
                    AwayTeamResult = table.Column<int>(type: "int", nullable: true),
                    ScheduledTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BestOf = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonMatches", x => x.SeasonMatchID);
                    table.CheckConstraint("CK_Event_SeasonMatches_MustBeDifferentTeams", "\r\n(HomeTeamID IS NULL) OR\r\n(AwayTeamID IS NULL) OR\r\n(HomeTeamID != AwayTeamID)\r\n");
                    table.ForeignKey(
                        name: "FK_SeasonMatches_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "TeamAvailability",
                schema: "Event",
                columns: table => new
                {
                    TeamID = table.Column<int>(type: "int", nullable: false),
                    AvailabilityOptionID = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamAvailabilityHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "TeamPlayers",
                schema: "Event",
                columns: table => new
                {
                    TeamPlayerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamID = table.Column<int>(type: "int", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    DraftCaptainOrder = table.Column<int>(type: "int", nullable: true),
                    DraftRound = table.Column<int>(type: "int", nullable: true),
                    DraftPick = table.Column<int>(type: "int", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Teams",
                schema: "Event",
                columns: table => new
                {
                    TeamID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonID = table.Column<int>(type: "int", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CaptainID = table.Column<int>(type: "int", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.InsertData(
                schema: "Other",
                table: "GameVersions",
                columns: new[] { "GameVesionID", "GameVersionName" },
                values: new object[,]
                {
                    { 1, "Halo 3" },
                    { 2, "Halo Reach" },
                    { 3, "Halo Reach Dash" },
                    { 4, "Halo 4" },
                    { 5, "Halo 5" },
                    { 6, "Halo Infinite" }
                });

            migrationBuilder.InsertData(
                schema: "Other",
                table: "Regions",
                columns: new[] { "RegionID", "RegionName" },
                values: new object[,]
                {
                    { 1, "West North America" },
                    { 2, "Central North America" },
                    { 3, "East North America" },
                    { 4, "North Europe" },
                    { 5, "South Europe" },
                    { 6, "Australia" }
                });

            migrationBuilder.InsertData(
                schema: "Auth",
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { 1, "cda78946-53d4-4154-b723-2b33b95341cc", "Sysadmin", "SYSADMIN" },
                    { 2, "37ff5fdf-5cb8-403c-bb9e-81db05a5fcdd", "Commissioner", "COMMISSIONER" },
                    { 3, "0be992c7-f824-40c7-8975-964068c7fbfe", "Player", "PLAYER" }
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
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAvailability_AvailabilityOptionID",
                schema: "Event",
                table: "SeasonAvailability",
                column: "AvailabilityOptionID");

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
                unique: true,
                filter: "[CaptainID] IS NOT NULL");

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
                unique: true,
                filter: "[TeamName] IS NOT NULL");

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
                name: "IX_Users_RegionID",
                schema: "Auth",
                table: "Users",
                column: "RegionID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_XboxUserID",
                schema: "Auth",
                table: "Users",
                column: "XboxUserID",
                unique: true,
                filter: "[XboxUserID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "Auth",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

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
                name: "FK_Users_XboxUsers_XboxUserID",
                schema: "Auth",
                table: "Users");

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
                schema: "Auth")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "DataProtectionKeysHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MatchBracketInfo",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchBracketInfoHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MatchLinks",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MedalEarned",
                schema: "Infinite")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MedalEarnedHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "Auth")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "RoleClaimsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "SeasonAvailability",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonAvailabilityHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "SignupAvailability",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SignupAvailabilityHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "TeamAvailability",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamAvailabilityHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "Auth")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserClaimsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "UserExperiences",
                schema: "Other")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserExperiencesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Other")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "Auth")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserLoginsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "Auth")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserRolesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "Auth")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserTokensHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "SeasonMatches",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MatchParticipants",
                schema: "Infinite")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Medals",
                schema: "Infinite")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "SeasonSignups",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonSignupsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "AvailabilityOptions",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AvailabilityOptionsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "GameVersions",
                schema: "Other")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "GameVersionsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Other")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "Auth")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "RolesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MatchTeams",
                schema: "Infinite")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MedalDifficulties",
                schema: "Infinite")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MedalDifficultiesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MedalTypes",
                schema: "Infinite")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MedalTypesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Matches",
                schema: "Infinite")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "XboxUsers",
                schema: "Xbox")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "XboxUsersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Seasons",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Teams",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "TeamPlayers",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "Auth")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UsersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Auth")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Regions",
                schema: "Other")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "RegionsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Other")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
