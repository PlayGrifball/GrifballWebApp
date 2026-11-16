using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchmakingLadderRanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LadderRanks",
                schema: "Matchmaking",
                columns: table => new
                {
                    MatchmakingLadderRankID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchmakingSeasonID = table.Column<int>(type: "int", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    Gamertag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: true),
                    Division = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    League = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    LeaguePoints = table.Column<int>(type: "int", nullable: true),
                    Streak = table.Column<int>(type: "int", nullable: true),
                    Mmr = table.Column<int>(type: "int", nullable: true),
                    Wins = table.Column<int>(type: "int", nullable: true),
                    Losses = table.Column<int>(type: "int", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    CreatedByID = table.Column<int>(type: "int", nullable: true),
                    ModifiedByID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LadderRanks", x => x.MatchmakingLadderRankID);
                    table.CheckConstraint("CK_Matchmaking_LadderRanks_PositionFromOne", "Position IS NULL OR Position >= 1");
                    table.CheckConstraint("CK_Matchmaking_LadderRanks_RecordNotNegative", "(Wins IS NULL OR Wins >= 0) AND (Losses IS NULL OR Losses >= 0)");
                    table.ForeignKey(
                        name: "FK_LadderRanks_MatchmakingSeasons_MatchmakingSeasonID",
                        column: x => x.MatchmakingSeasonID,
                        principalSchema: "Matchmaking",
                        principalTable: "MatchmakingSeasons",
                        principalColumn: "MatchmakingSeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LadderRanks_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "LadderRanksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_LadderRanks_MatchmakingSeasonID_UserID",
                schema: "Matchmaking",
                table: "LadderRanks",
                columns: new[] { "MatchmakingSeasonID", "UserID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LadderRanks_UserID",
                schema: "Matchmaking",
                table: "LadderRanks",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LadderRanks",
                schema: "Matchmaking")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "LadderRanksHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Matchmaking")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
