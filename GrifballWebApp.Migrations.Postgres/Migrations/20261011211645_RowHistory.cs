using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RowHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Xbox",
                table: "XboxUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XboxUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserRoles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserRoles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserLogins",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserLogins",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Other",
                table: "UserExperiences",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Other",
                table: "UserExperiences",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserClaims",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserClaims",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "Teams",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "Teams",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "TeamPlayers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamPlayers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "TeamAvailability",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamAvailability",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "SignupAvailability",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SignupAvailability",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "SeasonSignups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonSignups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "Seasons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "Seasons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "SeasonMatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonMatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "SeasonAvailability",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonAvailability",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "Roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "Roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "RoleClaims",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "RoleClaims",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Other",
                table: "Regions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Other",
                table: "Regions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "Ranks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "Ranks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "QueuedPlayers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "QueuedPlayers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Discord",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Discord",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MedalTypes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalTypes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "Medals",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Medals",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MedalEarned",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalEarned",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MedalDifficulties",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalDifficulties",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MatchTeams",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchTeams",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "MatchLinks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchLinks",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "Matches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Matches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedTeams",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedTeams",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "MatchBracketInfo",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchBracketInfo",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Other",
                table: "GameVersions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Other",
                table: "GameVersions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "User",
                table: "Discord",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "User",
                table: "Discord",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "DataProtectionKeys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "DataProtectionKeys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "AvailabilityOptions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "AvailabilityOptions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Xbox",
                table: "XboxUsers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XboxUsers");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserTokens");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserTokens");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Other",
                table: "UserExperiences");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Other",
                table: "UserExperiences");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserClaims");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserClaims");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "TeamAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "SignupAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SignupAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "SeasonAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "RoleClaims");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "RoleClaims");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "QueuedPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "QueuedPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Discord",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Discord",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MedalTypes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalTypes");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "Medals");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Medals");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MedalEarned");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalEarned");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MedalDifficulties");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalDifficulties");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MatchTeams");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchTeams");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedTeams");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedTeams");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "DataProtectionKeys");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "DataProtectionKeys");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "AvailabilityOptions");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "AvailabilityOptions");
        }
    }
}
