using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedGameGrabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameParticipants_History_Update";
                DROP TRIGGER IF EXISTS "GameParticipants_History_Delete";
                """);

            migrationBuilder.AddColumn<int>(
                name: "Grabs",
                schema: "Recorded",
                table: "GameParticipantsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Grabs",
                schema: "Recorded",
                table: "GameParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameParticipants_History_Update" AFTER UPDATE ON "GameParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "GameParticipantsHistory" ("Assists", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "Deaths", "Gamertag", "Goals", "Grabs", "HammerKills", "Kills", "ModifiedAt", "ModifiedByID", "PeriodStart", "Punches", "RecordedGameID", "RecordedGameParticipantID", "RecordedGameTeamID", "Score", "Suicides", "SwordKills", "UserID", "PeriodEnd") VALUES (OLD."Assists", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."Deaths", OLD."Gamertag", OLD."Goals", OLD."Grabs", OLD."HammerKills", OLD."Kills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Punches", OLD."RecordedGameID", OLD."RecordedGameParticipantID", OLD."RecordedGameTeamID", OLD."Score", OLD."Suicides", OLD."SwordKills", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameParticipants" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameParticipants_History_Delete" AFTER DELETE ON "GameParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "GameParticipantsHistory" ("Assists", "BallKills", "Betrayals", "CarrierKills", "CreatedAt", "CreatedByID", "Deaths", "Gamertag", "Goals", "Grabs", "HammerKills", "Kills", "ModifiedAt", "ModifiedByID", "PeriodStart", "Punches", "RecordedGameID", "RecordedGameParticipantID", "RecordedGameTeamID", "Score", "Suicides", "SwordKills", "UserID", "PeriodEnd") VALUES (OLD."Assists", OLD."BallKills", OLD."Betrayals", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."Deaths", OLD."Gamertag", OLD."Goals", OLD."Grabs", OLD."HammerKills", OLD."Kills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Punches", OLD."RecordedGameID", OLD."RecordedGameParticipantID", OLD."RecordedGameTeamID", OLD."Score", OLD."Suicides", OLD."SwordKills", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameParticipants_History_Update";
                DROP TRIGGER IF EXISTS "GameParticipants_History_Delete";
                """);

            migrationBuilder.DropColumn(
                name: "Grabs",
                schema: "Recorded",
                table: "GameParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "Grabs",
                schema: "Recorded",
                table: "GameParticipants");

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
