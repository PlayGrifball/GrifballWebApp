using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RecordedGameAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the rows the constraints would refuse first (the old model's public setters took any state): an origin
            // the model doesn't know is the import's (every game before manual match entry was); a game entered by hand
            // leaves a season of its own (it counts through its series game); a side's unknown result is none (DidNotFinish,
            // which the stats count as neither a win nor a loss, as before)
            migrationBuilder.Sql("""
                UPDATE "Games" SET "Origin" = 1 WHERE "Origin" NOT IN (1, 2);
                UPDATE "Games" SET "SeasonID" = NULL WHERE "Origin" = 2 AND "SeasonID" IS NOT NULL;
                UPDATE "GameTeams" SET "Outcome" = 4 WHERE "Outcome" NOT IN (1, 2, 3, 4);
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameTeams_History_Update";
                DROP TRIGGER IF EXISTS "GameTeams_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Games_History_Update";
                DROP TRIGGER IF EXISTS "Games_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recorded_GameTeams_KnownOutcome",
                schema: "Recorded",
                table: "GameTeams",
                sql: "Outcome IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recorded_Games_KnownOrigin",
                schema: "Recorded",
                table: "Games",
                sql: "Origin IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recorded_Games_ManualHasNoSeason",
                schema: "Recorded",
                table: "Games",
                sql: "Origin <> 2 OR SeasonID IS NULL");

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameTeams_History_Update" AFTER UPDATE ON "GameTeams" FOR EACH ROW BEGIN
                    INSERT INTO "GameTeamsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "Outcome", "PeriodStart", "RecordedGameID", "RecordedGameTeamID", "Score", "Side", "TeamID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."Outcome", OLD."PeriodStart", OLD."RecordedGameID", OLD."RecordedGameTeamID", OLD."Score", OLD."Side", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameTeams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameTeams_History_Delete" AFTER DELETE ON "GameTeams" FOR EACH ROW BEGIN
                    INSERT INTO "GameTeamsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "Outcome", "PeriodStart", "RecordedGameID", "RecordedGameTeamID", "Score", "Side", "TeamID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."Outcome", OLD."PeriodStart", OLD."RecordedGameID", OLD."RecordedGameTeamID", OLD."Score", OLD."Side", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Games_History_Update" AFTER UPDATE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "Origin", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Origin", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Games" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Games_History_Delete" AFTER DELETE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "Origin", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Origin", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled rows stay settled: they were in no state a recorded game can have
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameTeams_History_Update";
                DROP TRIGGER IF EXISTS "GameTeams_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Games_History_Update";
                DROP TRIGGER IF EXISTS "Games_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recorded_GameTeams_KnownOutcome",
                schema: "Recorded",
                table: "GameTeams");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recorded_Games_KnownOrigin",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recorded_Games_ManualHasNoSeason",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameTeams_History_Update" AFTER UPDATE ON "GameTeams" FOR EACH ROW BEGIN
                    INSERT INTO "GameTeamsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "Outcome", "PeriodStart", "RecordedGameID", "RecordedGameTeamID", "Score", "Side", "TeamID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."Outcome", OLD."PeriodStart", OLD."RecordedGameID", OLD."RecordedGameTeamID", OLD."Score", OLD."Side", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameTeams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameTeams_History_Delete" AFTER DELETE ON "GameTeams" FOR EACH ROW BEGIN
                    INSERT INTO "GameTeamsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "Name", "Outcome", "PeriodStart", "RecordedGameID", "RecordedGameTeamID", "Score", "Side", "TeamID", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."Outcome", OLD."PeriodStart", OLD."RecordedGameID", OLD."RecordedGameTeamID", OLD."Score", OLD."Side", OLD."TeamID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Games_History_Update" AFTER UPDATE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "Origin", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Origin", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Games" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Games_History_Delete" AFTER DELETE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "Origin", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Origin", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
