using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Xbox",
                table: "XboxUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Xbox",
                table: "XboxUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Xbox",
                table: "XboxUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Xbox",
                table: "XboxUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Auth",
                table: "Users",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Auth",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Auth",
                table: "Users",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Auth",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Auth",
                table: "UserRoles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Auth",
                table: "UserRoles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Auth",
                table: "UserRoles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Auth",
                table: "UserRoles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Auth",
                table: "UserLogins",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Auth",
                table: "UserLogins",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Auth",
                table: "UserLogins",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Auth",
                table: "UserLogins",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Other",
                table: "UserExperiences",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Other",
                table: "UserExperiences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Other",
                table: "UserExperiences",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Other",
                table: "UserExperiences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Auth",
                table: "UserClaims",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Auth",
                table: "UserClaims",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Auth",
                table: "UserClaims",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Auth",
                table: "UserClaims",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "Teams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "Teams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "TeamPlayers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "TeamPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "TeamPlayers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "TeamPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "TeamAvailability",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "TeamAvailability",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "TeamAvailability",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "TeamAvailability",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "SignupAvailability",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "SignupAvailability",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "SignupAvailability",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "SignupAvailability",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "SeasonSignups",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "SeasonSignups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "SeasonSignups",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "SeasonSignups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "Seasons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "Seasons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "SeasonMatches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "SeasonMatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "SeasonMatches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "SeasonMatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "SeasonAvailability",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "SeasonAvailability",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "SeasonAvailability",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "SeasonAvailability",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Auth",
                table: "Roles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Auth",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Auth",
                table: "Roles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Auth",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Other",
                table: "Regions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Other",
                table: "Regions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Other",
                table: "Regions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Other",
                table: "Regions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "Ranks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "Ranks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "Ranks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "Ranks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "QueuedPlayers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "QueuedPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "QueuedPlayers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "QueuedPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Discord",
                table: "Messages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Discord",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Discord",
                table: "Messages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Discord",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MedalTypes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MedalTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MedalTypes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MedalTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Infinite",
                table: "Medals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Infinite",
                table: "Medals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "Medals",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "Medals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MedalEarned",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MedalEarned",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MedalEarned",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MedalEarned",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MedalDifficulties",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MedalDifficulties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MedalDifficulties",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MedalDifficulties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MatchTeams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MatchTeams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MatchTeams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MatchTeams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "MatchLinks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "MatchLinks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "MatchLinks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "MatchLinks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Infinite",
                table: "Matches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Infinite",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "Matches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "MatchedTeams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedTeams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedTeams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedTeams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "MatchBracketInfo",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "MatchBracketInfo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "MatchBracketInfo",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "MatchBracketInfo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Other",
                table: "GameVersions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Other",
                table: "GameVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Other",
                table: "GameVersions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Other",
                table: "GameVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "User",
                table: "Discord",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "User",
                table: "Discord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "User",
                table: "Discord",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "User",
                table: "Discord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "AvailabilityOptions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "AvailabilityOptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "AvailabilityOptions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "AvailabilityOptions",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 1,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 4,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 5,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "GameVersions",
                keyColumn: "GameVesionID",
                keyValue: 6,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "Regions",
                keyColumn: "RegionID",
                keyValue: 1,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "Regions",
                keyColumn: "RegionID",
                keyValue: 2,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "Regions",
                keyColumn: "RegionID",
                keyValue: 3,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "Regions",
                keyColumn: "RegionID",
                keyValue: 4,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "Regions",
                keyColumn: "RegionID",
                keyValue: 5,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Other",
                table: "Regions",
                keyColumn: "RegionID",
                keyValue: 6,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Auth",
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Auth",
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });

            migrationBuilder.UpdateData(
                schema: "Auth",
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID" },
                values: new object[] { new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Xbox",
                table: "XboxUsers");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Xbox",
                table: "XboxUsers");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Xbox",
                table: "XboxUsers");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Xbox",
                table: "XboxUsers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Auth",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Auth",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Auth",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Auth",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Auth",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Auth",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Auth",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Auth",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Other",
                table: "UserExperiences");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Other",
                table: "UserExperiences");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Other",
                table: "UserExperiences");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Other",
                table: "UserExperiences");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Auth",
                table: "UserClaims");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Auth",
                table: "UserClaims");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Auth",
                table: "UserClaims");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Auth",
                table: "UserClaims");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "TeamAvailability");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "TeamAvailability");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "TeamAvailability");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "TeamAvailability");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "SignupAvailability");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "SignupAvailability");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "SignupAvailability");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "SignupAvailability");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "SeasonAvailability");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "SeasonAvailability");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "SeasonAvailability");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "SeasonAvailability");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Auth",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Auth",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Auth",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Auth",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "QueuedPlayers");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "QueuedPlayers");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "QueuedPlayers");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "QueuedPlayers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Discord",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Discord",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Discord",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Discord",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MedalTypes");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MedalTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MedalTypes");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MedalTypes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Infinite",
                table: "Medals");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Infinite",
                table: "Medals");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "Medals");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "Medals");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MedalEarned");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MedalEarned");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MedalEarned");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MedalEarned");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MedalDifficulties");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MedalDifficulties");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MedalDifficulties");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MedalDifficulties");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MatchTeams");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MatchTeams");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MatchTeams");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MatchTeams");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "MatchedTeams");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedTeams");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedTeams");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedTeams");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "AvailabilityOptions");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "AvailabilityOptions");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "AvailabilityOptions");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "AvailabilityOptions");
        }
    }
}
