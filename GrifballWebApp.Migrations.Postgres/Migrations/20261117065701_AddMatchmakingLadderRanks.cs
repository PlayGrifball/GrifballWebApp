using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                    MatchmakingLadderRankID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchmakingSeasonID = table.Column<int>(type: "integer", nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "case_insensitive"),
                    Position = table.Column<int>(type: "integer", nullable: true),
                    Division = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, collation: "case_insensitive"),
                    League = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    LeaguePoints = table.Column<int>(type: "integer", nullable: true),
                    Streak = table.Column<int>(type: "integer", nullable: true),
                    Mmr = table.Column<int>(type: "integer", nullable: true),
                    Wins = table.Column<int>(type: "integer", nullable: true),
                    Losses = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LadderRanks", x => x.MatchmakingLadderRankID);
                    table.CheckConstraint("CK_Matchmaking_LadderRanks_PositionFromOne", "\"Position\" IS NULL OR \"Position\" >= 1");
                    table.CheckConstraint("CK_Matchmaking_LadderRanks_RecordNotNegative", "(\"Wins\" IS NULL OR \"Wins\" >= 0) AND (\"Losses\" IS NULL OR \"Losses\" >= 0)");
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
                });

            migrationBuilder.CreateTable(
                name: "LadderRanksHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Division = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, collation: "case_insensitive"),
                    Gamertag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    League = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    LeaguePoints = table.Column<int>(type: "integer", nullable: true),
                    Losses = table.Column<int>(type: "integer", nullable: true),
                    MatchmakingLadderRankID = table.Column<int>(type: "integer", nullable: true),
                    MatchmakingSeasonID = table.Column<int>(type: "integer", nullable: true),
                    Mmr = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    Streak = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    Wins = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_LadderRanksHistory_MatchmakingLadderRankID_PeriodEnd",
                schema: "Matchmaking",
                table: "LadderRanksHistory",
                columns: new[] { "MatchmakingLadderRankID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_LadderRanksHistory_MatchmakingSeasonID_PeriodEnd",
                schema: "Matchmaking",
                table: "LadderRanksHistory",
                columns: new[] { "MatchmakingSeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_LadderRanksHistory_UserID_PeriodEnd",
                schema: "Matchmaking",
                table: "LadderRanksHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"LadderRanks\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"LadderRanks\"');");

            migrationBuilder.DropTable(
                name: "LadderRanks",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "LadderRanksHistory",
                schema: "Matchmaking");
        }
    }
}
