using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class DiscordSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "User");

            migrationBuilder.EnsureSchema(
                name: "Matchmaking");

            migrationBuilder.AlterColumn<string>(
                name: "Gamertag",
                schema: "Xbox",
                table: "XboxUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Discord",
                schema: "User",
                columns: table => new
                {
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: false),
                    DiscordUsername = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true),
                    MMR = table.Column<int>(type: "int", nullable: false),
                    WinStreak = table.Column<int>(type: "int", nullable: false),
                    LossStreak = table.Column<int>(type: "int", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discord", x => x.DiscordUserID);
                    table.ForeignKey(
                        name: "FK_Discord_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "DiscordHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "User")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchedTeams",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchedTeamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedTeams", x => x.MatchedTeamId);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "Ranks",
                schema: "Matchmaking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MmrThreshold = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ranks", x => x.Id);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "RanksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "QueuedPlayers",
                schema: "Matchmaking",
                columns: table => new
                {
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueuedPlayers", x => x.DiscordUserID);
                    table.ForeignKey(
                        name: "FK_QueuedPlayers_Discord_DiscordUserID",
                        column: x => x.DiscordUserID,
                        principalSchema: "User",
                        principalTable: "Discord",
                        principalColumn: "DiscordUserID");
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "QueuedPlayersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchedMatches",
                schema: "Matchmaking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    MatchID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ThreadID = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    VoteMessageID = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchedPlayers",
                schema: "Matchmaking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: false),
                    Kicked = table.Column<bool>(type: "bit", nullable: false),
                    MatchedTeamID = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchedPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchedPlayers_Discord_DiscordUserID",
                        column: x => x.DiscordUserID,
                        principalSchema: "User",
                        principalTable: "Discord",
                        principalColumn: "DiscordUserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchedPlayers_MatchedTeams_MatchedTeamID",
                        column: x => x.MatchedTeamID,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchedTeams",
                        principalColumn: "MatchedTeamId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedPlayersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchedKickVotes",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    VoterMatchedPlayerId = table.Column<int>(type: "int", nullable: false),
                    KickMatchedPlayerId = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedKickVotesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateTable(
                name: "MatchedWinnerVotes",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    MatchedPlayerId = table.Column<int>(type: "int", nullable: false),
                    WinnerVote = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedWinnerVotesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_Discord_XboxUserID",
                schema: "User",
                table: "Discord",
                column: "XboxUserID",
                unique: true,
                filter: "[XboxUserID] IS NOT NULL");

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
                unique: true,
                filter: "[MatchID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedPlayers_DiscordUserID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "DiscordUserID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedPlayers_MatchedTeamID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "MatchedTeamID");

            migrationBuilder.CreateIndex(
                name: "IX_MatchedWinnerVotes_MatchedPlayerId",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                column: "MatchedPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchedKickVotes",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedKickVotesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MatchedWinnerVotes",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedWinnerVotesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "QueuedPlayers",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "QueuedPlayersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Ranks",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "RanksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MatchedMatches",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedMatchesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MatchedPlayers",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedPlayersHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "Discord",
                schema: "User")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "DiscordHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "User")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropTable(
                name: "MatchedTeams",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MatchedTeamsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.AlterColumn<string>(
                name: "Gamertag",
                schema: "Xbox",
                table: "XboxUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
