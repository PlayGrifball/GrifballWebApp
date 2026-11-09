using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RecordedGameOneLinePerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One line a player in a game: every line of a user in a game but the oldest (lowest key) goes, as
            // RecordedGame.SetPlayers keeps it, before the unique index. Such a line came from a merge of two accounts that
            // both played the game, and counted the player twice. Deleted while the history triggers are still on, so the
            // history keeps the deleted lines; the triggers are recreated after the index.
            migrationBuilder.Sql("""
                DELETE FROM "GameParticipants"
                WHERE EXISTS (SELECT 1 FROM "GameParticipants" AS k
                    WHERE k."RecordedGameID" = "GameParticipants"."RecordedGameID" AND k."UserID" = "GameParticipants"."UserID" AND k."RecordedGameParticipantID" < "GameParticipants"."RecordedGameParticipantID");
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameParticipants_History_Update";
                DROP TRIGGER IF EXISTS "GameParticipants_History_Delete";
                """);

            migrationBuilder.DropIndex(
                name: "IX_GameParticipants_RecordedGameID",
                schema: "Recorded",
                table: "GameParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_RecordedGameID_UserID",
                schema: "Recorded",
                table: "GameParticipants",
                columns: new[] { "RecordedGameID", "UserID" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameParticipants_History_Update" AFTER UPDATE ON "GameParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "GameParticipantsHistory" ("Assists", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "Deaths", "Gamertag", "Goals", "HammerKills", "Kills", "ModifiedAt", "ModifiedByID", "PeriodStart", "Punches", "RecordedGameID", "RecordedGameParticipantID", "RecordedGameTeamID", "Score", "Suicides", "SwordKills", "UserID", "PeriodEnd") VALUES (OLD."Assists", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."Deaths", OLD."Gamertag", OLD."Goals", OLD."HammerKills", OLD."Kills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Punches", OLD."RecordedGameID", OLD."RecordedGameParticipantID", OLD."RecordedGameTeamID", OLD."Score", OLD."Suicides", OLD."SwordKills", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameParticipants" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameParticipants_History_Delete" AFTER DELETE ON "GameParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "GameParticipantsHistory" ("Assists", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "Deaths", "Gamertag", "Goals", "HammerKills", "Kills", "ModifiedAt", "ModifiedByID", "PeriodStart", "Punches", "RecordedGameID", "RecordedGameParticipantID", "RecordedGameTeamID", "Score", "Suicides", "SwordKills", "UserID", "PeriodEnd") VALUES (OLD."Assists", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."Deaths", OLD."Gamertag", OLD."Goals", OLD."HammerKills", OLD."Kills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Punches", OLD."RecordedGameID", OLD."RecordedGameParticipantID", OLD."RecordedGameTeamID", OLD."Score", OLD."Suicides", OLD."SwordKills", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The deleted lines stay gone: a second line of one player in a game is no state a recorded game can have
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameParticipants_History_Update";
                DROP TRIGGER IF EXISTS "GameParticipants_History_Delete";
                """);

            migrationBuilder.DropIndex(
                name: "IX_GameParticipants_RecordedGameID_UserID",
                schema: "Recorded",
                table: "GameParticipants");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_RecordedGameID",
                schema: "Recorded",
                table: "GameParticipants",
                column: "RecordedGameID");

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameParticipants_History_Update" AFTER UPDATE ON "GameParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "GameParticipantsHistory" ("Assists", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "Deaths", "Gamertag", "Goals", "HammerKills", "Kills", "ModifiedAt", "ModifiedByID", "PeriodStart", "Punches", "RecordedGameID", "RecordedGameParticipantID", "RecordedGameTeamID", "Score", "Suicides", "SwordKills", "UserID", "PeriodEnd") VALUES (OLD."Assists", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."Deaths", OLD."Gamertag", OLD."Goals", OLD."HammerKills", OLD."Kills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Punches", OLD."RecordedGameID", OLD."RecordedGameParticipantID", OLD."RecordedGameTeamID", OLD."Score", OLD."Suicides", OLD."SwordKills", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameParticipants" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameParticipants_History_Delete" AFTER DELETE ON "GameParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "GameParticipantsHistory" ("Assists", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "Deaths", "Gamertag", "Goals", "HammerKills", "Kills", "ModifiedAt", "ModifiedByID", "PeriodStart", "Punches", "RecordedGameID", "RecordedGameParticipantID", "RecordedGameTeamID", "Score", "Suicides", "SwordKills", "UserID", "PeriodEnd") VALUES (OLD."Assists", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."Deaths", OLD."Gamertag", OLD."Goals", OLD."HammerKills", OLD."Kills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Punches", OLD."RecordedGameID", OLD."RecordedGameParticipantID", OLD."RecordedGameTeamID", OLD."Score", OLD."Suicides", OLD."SwordKills", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
