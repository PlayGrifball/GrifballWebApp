using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomScoreDecoding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchParticipants_History_Update";
                DROP TRIGGER IF EXISTS "MatchParticipants_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Matches_History_Update";
                DROP TRIGGER IF EXISTS "Matches_History_Delete";
                """);

            migrationBuilder.AddColumn<long>(
                name: "BallHeldTime",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BallPunches",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarrierKills",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoalAssists",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Goals",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassIns",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlateStops",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SwordKills",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BallHeldTime",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BallPunches",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarrierKills",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoalAssists",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Goals",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassIns",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlateStops",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SwordKills",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "MatchesHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "Matches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchParticipants_History_Update" AFTER UPDATE ON "MatchParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "MatchParticipantsHistory" ("Accuracy", "Assists", "AverageLife", "BallHeldTime", "BallPunches", "Betrayals", "CalloutAssists", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "FirstJoinedTime", "GoalAssists", "Goals", "JoinedInProgress", "Kda", "Kills", "LastLeaveTime", "LeftInProgress", "MatchID", "MaxKillingSpree", "MeleeKills", "ModifiedAt", "ModifiedByID", "ObjectivesCompleted", "PassIns", "PeriodStart", "PersonalScore", "PlateStops", "PowerWeaponKills", "PresentAtBeginning", "PresentAtCompletion", "Rank", "RoundsLost", "RoundsTied", "RoundsWon", "Score", "ShotsFired", "ShotsHit", "Spawns", "Suicides", "SwordKills", "TeamID", "TimePlayed", "XboxUserID", "PeriodEnd") VALUES (OLD."Accuracy", OLD."Assists", OLD."AverageLife", OLD."BallHeldTime", OLD."BallPunches", OLD."Betrayals", OLD."CalloutAssists", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."FirstJoinedTime", OLD."GoalAssists", OLD."Goals", OLD."JoinedInProgress", OLD."Kda", OLD."Kills", OLD."LastLeaveTime", OLD."LeftInProgress", OLD."MatchID", OLD."MaxKillingSpree", OLD."MeleeKills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."ObjectivesCompleted", OLD."PassIns", OLD."PeriodStart", OLD."PersonalScore", OLD."PlateStops", OLD."PowerWeaponKills", OLD."PresentAtBeginning", OLD."PresentAtCompletion", OLD."Rank", OLD."RoundsLost", OLD."RoundsTied", OLD."RoundsWon", OLD."Score", OLD."ShotsFired", OLD."ShotsHit", OLD."Spawns", OLD."Suicides", OLD."SwordKills", OLD."TeamID", OLD."TimePlayed", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchParticipants" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchParticipants_History_Delete" AFTER DELETE ON "MatchParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "MatchParticipantsHistory" ("Accuracy", "Assists", "AverageLife", "BallHeldTime", "BallPunches", "Betrayals", "CalloutAssists", "CarrierKills", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "FirstJoinedTime", "GoalAssists", "Goals", "JoinedInProgress", "Kda", "Kills", "LastLeaveTime", "LeftInProgress", "MatchID", "MaxKillingSpree", "MeleeKills", "ModifiedAt", "ModifiedByID", "ObjectivesCompleted", "PassIns", "PeriodStart", "PersonalScore", "PlateStops", "PowerWeaponKills", "PresentAtBeginning", "PresentAtCompletion", "Rank", "RoundsLost", "RoundsTied", "RoundsWon", "Score", "ShotsFired", "ShotsHit", "Spawns", "Suicides", "SwordKills", "TeamID", "TimePlayed", "XboxUserID", "PeriodEnd") VALUES (OLD."Accuracy", OLD."Assists", OLD."AverageLife", OLD."BallHeldTime", OLD."BallPunches", OLD."Betrayals", OLD."CalloutAssists", OLD."CarrierKills", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."FirstJoinedTime", OLD."GoalAssists", OLD."Goals", OLD."JoinedInProgress", OLD."Kda", OLD."Kills", OLD."LastLeaveTime", OLD."LeftInProgress", OLD."MatchID", OLD."MaxKillingSpree", OLD."MeleeKills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."ObjectivesCompleted", OLD."PassIns", OLD."PeriodStart", OLD."PersonalScore", OLD."PlateStops", OLD."PowerWeaponKills", OLD."PresentAtBeginning", OLD."PresentAtCompletion", OLD."Rank", OLD."RoundsLost", OLD."RoundsTied", OLD."RoundsWon", OLD."Score", OLD."ShotsFired", OLD."ShotsHit", OLD."Spawns", OLD."Suicides", OLD."SwordKills", OLD."TeamID", OLD."TimePlayed", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Matches_History_Update" AFTER UPDATE ON "Matches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchesHistory" ("CreatedAt", "CreatedByID", "Duration", "EndTime", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "ScoreLayout", "StartTime", "StatsPullDate", "UgcGameVariantAssetID", "UgcGameVariantVersionID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Duration", OLD."EndTime", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ScoreLayout", OLD."StartTime", OLD."StatsPullDate", OLD."UgcGameVariantAssetID", OLD."UgcGameVariantVersionID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Matches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Matches_History_Delete" AFTER DELETE ON "Matches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchesHistory" ("CreatedAt", "CreatedByID", "Duration", "EndTime", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "ScoreLayout", "StartTime", "StatsPullDate", "UgcGameVariantAssetID", "UgcGameVariantVersionID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Duration", OLD."EndTime", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ScoreLayout", OLD."StartTime", OLD."StatsPullDate", OLD."UgcGameVariantAssetID", OLD."UgcGameVariantVersionID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchParticipants_History_Update";
                DROP TRIGGER IF EXISTS "MatchParticipants_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Matches_History_Update";
                DROP TRIGGER IF EXISTS "Matches_History_Delete";
                """);

            migrationBuilder.DropColumn(
                name: "BallHeldTime",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "BallPunches",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "CarrierKills",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "GoalAssists",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "Goals",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "PassIns",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "PlateStops",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "SwordKills",
                schema: "Infinite",
                table: "MatchParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "BallHeldTime",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "BallPunches",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "CarrierKills",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "GoalAssists",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "Goals",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "PassIns",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "PlateStops",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "SwordKills",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "MatchesHistory");

            migrationBuilder.DropColumn(
                name: "ScoreLayout",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchParticipants_History_Update" AFTER UPDATE ON "MatchParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "MatchParticipantsHistory" ("Accuracy", "Assists", "AverageLife", "Betrayals", "CalloutAssists", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "FirstJoinedTime", "JoinedInProgress", "Kda", "Kills", "LastLeaveTime", "LeftInProgress", "MatchID", "MaxKillingSpree", "MeleeKills", "ModifiedAt", "ModifiedByID", "ObjectivesCompleted", "PeriodStart", "PersonalScore", "PowerWeaponKills", "PresentAtBeginning", "PresentAtCompletion", "Rank", "RoundsLost", "RoundsTied", "RoundsWon", "Score", "ShotsFired", "ShotsHit", "Spawns", "Suicides", "TeamID", "TimePlayed", "XboxUserID", "PeriodEnd") VALUES (OLD."Accuracy", OLD."Assists", OLD."AverageLife", OLD."Betrayals", OLD."CalloutAssists", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."FirstJoinedTime", OLD."JoinedInProgress", OLD."Kda", OLD."Kills", OLD."LastLeaveTime", OLD."LeftInProgress", OLD."MatchID", OLD."MaxKillingSpree", OLD."MeleeKills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."ObjectivesCompleted", OLD."PeriodStart", OLD."PersonalScore", OLD."PowerWeaponKills", OLD."PresentAtBeginning", OLD."PresentAtCompletion", OLD."Rank", OLD."RoundsLost", OLD."RoundsTied", OLD."RoundsWon", OLD."Score", OLD."ShotsFired", OLD."ShotsHit", OLD."Spawns", OLD."Suicides", OLD."TeamID", OLD."TimePlayed", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchParticipants" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchParticipants_History_Delete" AFTER DELETE ON "MatchParticipants" FOR EACH ROW BEGIN
                    INSERT INTO "MatchParticipantsHistory" ("Accuracy", "Assists", "AverageLife", "Betrayals", "CalloutAssists", "CreatedAt", "CreatedByID", "DamageDealt", "DamageTaken", "Deaths", "FirstJoinedTime", "JoinedInProgress", "Kda", "Kills", "LastLeaveTime", "LeftInProgress", "MatchID", "MaxKillingSpree", "MeleeKills", "ModifiedAt", "ModifiedByID", "ObjectivesCompleted", "PeriodStart", "PersonalScore", "PowerWeaponKills", "PresentAtBeginning", "PresentAtCompletion", "Rank", "RoundsLost", "RoundsTied", "RoundsWon", "Score", "ShotsFired", "ShotsHit", "Spawns", "Suicides", "TeamID", "TimePlayed", "XboxUserID", "PeriodEnd") VALUES (OLD."Accuracy", OLD."Assists", OLD."AverageLife", OLD."Betrayals", OLD."CalloutAssists", OLD."CreatedAt", OLD."CreatedByID", OLD."DamageDealt", OLD."DamageTaken", OLD."Deaths", OLD."FirstJoinedTime", OLD."JoinedInProgress", OLD."Kda", OLD."Kills", OLD."LastLeaveTime", OLD."LeftInProgress", OLD."MatchID", OLD."MaxKillingSpree", OLD."MeleeKills", OLD."ModifiedAt", OLD."ModifiedByID", OLD."ObjectivesCompleted", OLD."PeriodStart", OLD."PersonalScore", OLD."PowerWeaponKills", OLD."PresentAtBeginning", OLD."PresentAtCompletion", OLD."Rank", OLD."RoundsLost", OLD."RoundsTied", OLD."RoundsWon", OLD."Score", OLD."ShotsFired", OLD."ShotsHit", OLD."Spawns", OLD."Suicides", OLD."TeamID", OLD."TimePlayed", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Matches_History_Update" AFTER UPDATE ON "Matches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchesHistory" ("CreatedAt", "CreatedByID", "Duration", "EndTime", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartTime", "StatsPullDate", "UgcGameVariantAssetID", "UgcGameVariantVersionID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Duration", OLD."EndTime", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartTime", OLD."StatsPullDate", OLD."UgcGameVariantAssetID", OLD."UgcGameVariantVersionID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Matches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Matches_History_Delete" AFTER DELETE ON "Matches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchesHistory" ("CreatedAt", "CreatedByID", "Duration", "EndTime", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartTime", "StatsPullDate", "UgcGameVariantAssetID", "UgcGameVariantVersionID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Duration", OLD."EndTime", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartTime", OLD."StatsPullDate", OLD."UgcGameVariantAssetID", OLD."UgcGameVariantVersionID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
