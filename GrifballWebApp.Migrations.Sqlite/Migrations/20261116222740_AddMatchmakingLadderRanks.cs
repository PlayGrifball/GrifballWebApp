using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
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
                    MatchmakingLadderRankID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MatchmakingSeasonID = table.Column<int>(type: "INTEGER", nullable: false),
                    UserID = table.Column<int>(type: "INTEGER", nullable: false),
                    Gamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    Position = table.Column<int>(type: "INTEGER", nullable: true),
                    Division = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true, collation: "NOCASE"),
                    League = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    Rank = table.Column<int>(type: "INTEGER", nullable: true),
                    LeaguePoints = table.Column<int>(type: "INTEGER", nullable: true),
                    Streak = table.Column<int>(type: "INTEGER", nullable: true),
                    Mmr = table.Column<int>(type: "INTEGER", nullable: true),
                    Wins = table.Column<int>(type: "INTEGER", nullable: true),
                    Losses = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false, computedColumnSql: "'9999-12-31 23:59:59.9999999'", stored: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')"),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "LadderRanksHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Division = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true, collation: "NOCASE"),
                    Gamertag = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    League = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    LeaguePoints = table.Column<int>(type: "INTEGER", nullable: true),
                    Losses = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchmakingLadderRankID = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchmakingSeasonID = table.Column<int>(type: "INTEGER", nullable: true),
                    Mmr = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: true),
                    Rank = table.Column<int>(type: "INTEGER", nullable: true),
                    Streak = table.Column<int>(type: "INTEGER", nullable: true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true),
                    Wins = table.Column<int>(type: "INTEGER", nullable: true)
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

            migrationBuilder.Sql("""
                CREATE TRIGGER "LadderRanks_History_Update" AFTER UPDATE ON "LadderRanks" FOR EACH ROW BEGIN
                    INSERT INTO "LadderRanksHistory" ("CreatedAt", "CreatedByID", "Division", "Gamertag", "League", "LeaguePoints", "Losses", "MatchmakingLadderRankID", "MatchmakingSeasonID", "Mmr", "ModifiedAt", "ModifiedByID", "PeriodStart", "Position", "Rank", "Streak", "UserID", "Wins", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."Gamertag", OLD."League", OLD."LeaguePoints", OLD."Losses", OLD."MatchmakingLadderRankID", OLD."MatchmakingSeasonID", OLD."Mmr", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Position", OLD."Rank", OLD."Streak", OLD."UserID", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "LadderRanks" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "LadderRanks_History_Delete" AFTER DELETE ON "LadderRanks" FOR EACH ROW BEGIN
                    INSERT INTO "LadderRanksHistory" ("CreatedAt", "CreatedByID", "Division", "Gamertag", "League", "LeaguePoints", "Losses", "MatchmakingLadderRankID", "MatchmakingSeasonID", "Mmr", "ModifiedAt", "ModifiedByID", "PeriodStart", "Position", "Rank", "Streak", "UserID", "Wins", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."Gamertag", OLD."League", OLD."LeaguePoints", OLD."Losses", OLD."MatchmakingLadderRankID", OLD."MatchmakingSeasonID", OLD."Mmr", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Position", OLD."Rank", OLD."Streak", OLD."UserID", OLD."Wins", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "LadderRanks_History_Update";
                DROP TRIGGER IF EXISTS "LadderRanks_History_Delete";
                """);

            migrationBuilder.DropTable(
                name: "LadderRanks",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "LadderRanksHistory",
                schema: "Matchmaking");
        }
    }
}
