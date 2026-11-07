using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MatchedPlayerUserRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedPlayers_History_Update";
                DROP TRIGGER IF EXISTS "MatchedPlayers_History_Delete";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedPlayers_Users_UserID",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedPlayers_Users_UserID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "UserID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedPlayers_History_Update" AFTER UPDATE ON "MatchedPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedPlayersHistory" ("CreatedAt", "CreatedByID", "Id", "Kicked", "MatchedTeamID", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."Kicked", OLD."MatchedTeamID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedPlayers" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedPlayers_History_Delete" AFTER DELETE ON "MatchedPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedPlayersHistory" ("CreatedAt", "CreatedByID", "Id", "Kicked", "MatchedTeamID", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."Kicked", OLD."MatchedTeamID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchedPlayers_History_Update";
                DROP TRIGGER IF EXISTS "MatchedPlayers_History_Delete";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MatchedPlayers_Users_UserID",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchedPlayers_Users_UserID",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                column: "UserID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchedPlayers_History_Update" AFTER UPDATE ON "MatchedPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedPlayersHistory" ("CreatedAt", "CreatedByID", "Id", "Kicked", "MatchedTeamID", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."Kicked", OLD."MatchedTeamID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchedPlayers" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchedPlayers_History_Delete" AFTER DELETE ON "MatchedPlayers" FOR EACH ROW BEGIN
                    INSERT INTO "MatchedPlayersHistory" ("CreatedAt", "CreatedByID", "Id", "Kicked", "MatchedTeamID", "ModifiedAt", "ModifiedByID", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Id", OLD."Kicked", OLD."MatchedTeamID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
