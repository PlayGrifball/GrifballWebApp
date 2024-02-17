using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Infinite");

            migrationBuilder.EnsureSchema(
                name: "Event");

            migrationBuilder.EnsureSchema(
                name: "Xbox");

            migrationBuilder.CreateTable(
                name: "Matches",
                schema: "Infinite",
                columns: table => new
                {
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
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
                    MedalDifficultyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalDifficultiesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    MedalDifficultyName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalDifficultiesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalDifficultiesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalDifficultiesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
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
                    MedalTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalTypesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    MedalTypeName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalTypesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalTypesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalTypesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
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
                name: "Seasons",
                schema: "Event",
                columns: table => new
                {
                    SeasonID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    SeasonName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
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
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "XboxUsersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Gamertag = table.Column<string>(type: "nvarchar(max)", nullable: true)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "XboxUsersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "XboxUsersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "XboxUsersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
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
                name: "Medals",
                schema: "Infinite",
                columns: table => new
                {
                    MedalID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    MedalName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    SpriteIndex = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    SortingWeight = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    MedalDifficultyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    MedalTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PersonalScore = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
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
                name: "Teams",
                schema: "Event",
                columns: table => new
                {
                    TeamID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    SeasonID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    TeamName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchParticipants",
                schema: "Infinite",
                columns: table => new
                {
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    TeamID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Kills = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Deaths = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Assists = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Kda = table.Column<float>(type: "real", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Suicides = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Betrayals = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    AverageLife = table.Column<TimeSpan>(type: "time", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    MeleeKills = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PowerWeaponKills = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    ShotsFired = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    ShotsHit = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Accuracy = table.Column<float>(type: "real", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    DamageDealt = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    CalloutAssists = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    MaxKillingSpree = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    DamageTaken = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchParticipantsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchParticipants", x => new { x.MatchID, x.XboxUserID });
                    table.ForeignKey(
                        name: "FK_MatchParticipants_Matches_MatchID",
                        column: x => x.MatchID,
                        principalSchema: "Infinite",
                        principalTable: "Matches",
                        principalColumn: "MatchID",
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
                name: "Persons",
                schema: "Event",
                columns: table => new
                {
                    PersonID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "PersonsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "PersonsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "PersonsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "PersonsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "PersonsHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.PersonID);
                    table.ForeignKey(
                        name: "FK_Persons_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PersonsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "SeasonMatches",
                schema: "Event",
                columns: table => new
                {
                    SeasonMatchID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    SeasonID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    HomeTeamID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    AwayTeamID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    ScheduledTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonMatches", x => x.SeasonMatchID);
                    table.ForeignKey(
                        name: "FK_SeasonMatches_Seasons_SeasonMatchID",
                        column: x => x.SeasonMatchID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonMatches_Teams_AwayTeamID",
                        column: x => x.AwayTeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID");
                    table.ForeignKey(
                        name: "FK_SeasonMatches_Teams_HomeTeamID",
                        column: x => x.HomeTeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "SeasonMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MedalEarned",
                schema: "Infinite",
                columns: table => new
                {
                    MedalID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalEarnedHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalEarnedHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalEarnedHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    Count = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalEarnedHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    TotalPersonalScoreAwarded = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalEarnedHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalEarnedHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MedalEarnedHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Infinite")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
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
                name: "TeamPlayers",
                schema: "Event",
                columns: table => new
                {
                    TeamPlayerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    TeamID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    IsCaptain = table.Column<bool>(type: "bit", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PlayerID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamPlayers", x => x.TeamPlayerID);
                    table.ForeignKey(
                        name: "FK_TeamPlayers_Persons_PlayerID",
                        column: x => x.PlayerID,
                        principalSchema: "Event",
                        principalTable: "Persons",
                        principalColumn: "PersonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamPlayers_Teams_TeamID",
                        column: x => x.TeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchLinks",
                schema: "Event",
                columns: table => new
                {
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    SeasonMatchID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:IsTemporal", true)
                        .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                        .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                        .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                        .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLinks", x => new { x.MatchID, x.SeasonMatchID });
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
                        principalColumn: "SeasonMatchID");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchLinksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinks_MatchID",
                schema: "Event",
                table: "MatchLinks",
                column: "MatchID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinks_SeasonMatchID",
                schema: "Event",
                table: "MatchLinks",
                column: "SeasonMatchID",
                unique: true);

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
                name: "IX_Persons_Name",
                schema: "Event",
                table: "Persons",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Persons_XboxUserID",
                schema: "Event",
                table: "Persons",
                column: "XboxUserID",
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
                name: "IX_Seasons_SeasonName",
                schema: "Event",
                table: "Seasons",
                column: "SeasonName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_PlayerID",
                schema: "Event",
                table: "TeamPlayers",
                column: "PlayerID");

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_TeamID",
                schema: "Event",
                table: "TeamPlayers",
                column: "TeamID");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "TeamPlayers",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "TeamPlayersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Event")
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
                name: "Persons",
                schema: "Event")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PersonsHistory")
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
                name: "Matches",
                schema: "Infinite")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchesHistory")
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
        }
    }
}
