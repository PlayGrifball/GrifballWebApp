using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MatchmakingSeasonAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the seasons the constraints would refuse first, as the old model's public setters could leave them.
            // An imported season the old import dated after its import (a start past the import, its end cut back to it)
            // starts at that end, as DateArchived dates one now, so it stays ended; any other that ends before it starts
            // ends as it starts; a source link on a season not the archive's goes. History keeps the old values.
            migrationBuilder.Sql("UPDATE \"MatchmakingSeasons\" SET \"StartsAt\" = \"EndsAt\" WHERE \"HistoricalSourceID\" IS NOT NULL AND \"EndsAt\" < \"StartsAt\";");
            migrationBuilder.Sql("UPDATE \"MatchmakingSeasons\" SET \"EndsAt\" = \"StartsAt\" WHERE \"EndsAt\" < \"StartsAt\";");
            migrationBuilder.Sql("UPDATE \"MatchmakingSeasons\" SET \"HistoricalSourceUrl\" = NULL WHERE \"HistoricalSourceUrl\" IS NOT NULL AND \"HistoricalSourceID\" IS NULL;");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchmakingSeasons_History_Update";
                DROP TRIGGER IF EXISTS "MatchmakingSeasons_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchmakingSeasons_DatesInOrder",
                schema: "Matchmaking",
                table: "MatchmakingSeasons",
                sql: "StartsAt <= EndsAt");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_MatchmakingSeasons_SourceUrlOnlyImported",
                schema: "Matchmaking",
                table: "MatchmakingSeasons",
                sql: "HistoricalSourceUrl IS NULL OR HistoricalSourceID IS NOT NULL");

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchmakingSeasons_History_Update" AFTER UPDATE ON "MatchmakingSeasons" FOR EACH ROW BEGIN
                    INSERT INTO "MatchmakingSeasonsHistory" ("CreatedAt", "CreatedByID", "EndsAt", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "StartsAt", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."EndsAt", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", OLD."StartsAt", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchmakingSeasons" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchmakingSeasons_History_Delete" AFTER DELETE ON "MatchmakingSeasons" FOR EACH ROW BEGIN
                    INSERT INTO "MatchmakingSeasonsHistory" ("CreatedAt", "CreatedByID", "EndsAt", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "StartsAt", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."EndsAt", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", OLD."StartsAt", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled rows stay settled: they were in no state a season can have
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "MatchmakingSeasons_History_Update";
                DROP TRIGGER IF EXISTS "MatchmakingSeasons_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchmakingSeasons_DatesInOrder",
                schema: "Matchmaking",
                table: "MatchmakingSeasons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_MatchmakingSeasons_SourceUrlOnlyImported",
                schema: "Matchmaking",
                table: "MatchmakingSeasons");

            migrationBuilder.Sql("""
                CREATE TRIGGER "MatchmakingSeasons_History_Update" AFTER UPDATE ON "MatchmakingSeasons" FOR EACH ROW BEGIN
                    INSERT INTO "MatchmakingSeasonsHistory" ("CreatedAt", "CreatedByID", "EndsAt", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "StartsAt", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."EndsAt", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", OLD."StartsAt", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "MatchmakingSeasons" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "MatchmakingSeasons_History_Delete" AFTER DELETE ON "MatchmakingSeasons" FOR EACH ROW BEGIN
                    INSERT INTO "MatchmakingSeasonsHistory" ("CreatedAt", "CreatedByID", "EndsAt", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "MatchmakingSeasonID", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "StartsAt", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."EndsAt", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."MatchmakingSeasonID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", OLD."StartsAt", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
