using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SeasonDatesInOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Imported seasons first, as ScheduleArchived settles them: the old import kept a start after the import time and
            // cut the end back to it, so such a season ended at its import. All its dates go back to that end, so it stays
            // ended (rather than its end moving forward to a future start).
            migrationBuilder.Sql("UPDATE \"Seasons\" SET \"SignupsOpen\" = \"SeasonEnd\", \"SignupsClose\" = \"SeasonEnd\", \"DraftStart\" = \"SeasonEnd\", \"SeasonStart\" = \"SeasonEnd\", \"PublicAt\" = \"SeasonEnd\" WHERE \"HistoricalSourceID\" IS NOT NULL AND \"SeasonEnd\" < \"SeasonStart\";");

            // Settle the seasons the constraint would refuse (the editor took dates in any order): signups that closed before
            // they opened close as they open, a season that ended before it started ends as it starts, and a draft after the
            // start is at the start. Each row is otherwise as it was, and its history keeps the old dates.
            migrationBuilder.Sql("UPDATE \"Seasons\" SET \"SignupsClose\" = \"SignupsOpen\" WHERE \"SignupsClose\" < \"SignupsOpen\";");
            migrationBuilder.Sql("UPDATE \"Seasons\" SET \"SeasonEnd\" = \"SeasonStart\" WHERE \"SeasonEnd\" < \"SeasonStart\";");
            migrationBuilder.Sql("UPDATE \"Seasons\" SET \"DraftStart\" = \"SeasonStart\" WHERE \"DraftStart\" > \"SeasonStart\";");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Seasons_History_Update";
                DROP TRIGGER IF EXISTS "Seasons_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Seasons_DatesInOrder",
                schema: "Event",
                table: "Seasons",
                sql: "SignupsOpen <= SignupsClose AND DraftStart <= SeasonStart AND SeasonStart <= SeasonEnd");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Seasons_History_Update" AFTER UPDATE ON "Seasons" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonsHistory" ("CaptainsLocked", "CreatedAt", "CreatedByID", "Division", "DraftStart", "Format", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "LeagueID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublicAt", "SeasonEnd", "SeasonID", "SeasonName", "SeasonStart", "SignupsClose", "SignupsOpen", "PeriodEnd") VALUES (OLD."CaptainsLocked", OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."DraftStart", OLD."Format", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."LeagueID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublicAt", OLD."SeasonEnd", OLD."SeasonID", OLD."SeasonName", OLD."SeasonStart", OLD."SignupsClose", OLD."SignupsOpen", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Seasons" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Seasons_History_Delete" AFTER DELETE ON "Seasons" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonsHistory" ("CaptainsLocked", "CreatedAt", "CreatedByID", "Division", "DraftStart", "Format", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "LeagueID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublicAt", "SeasonEnd", "SeasonID", "SeasonName", "SeasonStart", "SignupsClose", "SignupsOpen", "PeriodEnd") VALUES (OLD."CaptainsLocked", OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."DraftStart", OLD."Format", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."LeagueID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublicAt", OLD."SeasonEnd", OLD."SeasonID", OLD."SeasonName", OLD."SeasonStart", OLD."SignupsClose", OLD."SignupsOpen", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled dates stay settled: they were in no order a season can have
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Seasons_History_Update";
                DROP TRIGGER IF EXISTS "Seasons_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Seasons_DatesInOrder",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Seasons_History_Update" AFTER UPDATE ON "Seasons" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonsHistory" ("CaptainsLocked", "CreatedAt", "CreatedByID", "Division", "DraftStart", "Format", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "LeagueID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublicAt", "SeasonEnd", "SeasonID", "SeasonName", "SeasonStart", "SignupsClose", "SignupsOpen", "PeriodEnd") VALUES (OLD."CaptainsLocked", OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."DraftStart", OLD."Format", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."LeagueID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublicAt", OLD."SeasonEnd", OLD."SeasonID", OLD."SeasonName", OLD."SeasonStart", OLD."SignupsClose", OLD."SignupsOpen", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Seasons" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Seasons_History_Delete" AFTER DELETE ON "Seasons" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonsHistory" ("CaptainsLocked", "CreatedAt", "CreatedByID", "Division", "DraftStart", "Format", "GameVersionID", "HistoricalSourceID", "HistoricalSourceUrl", "LeagueID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublicAt", "SeasonEnd", "SeasonID", "SeasonName", "SeasonStart", "SignupsClose", "SignupsOpen", "PeriodEnd") VALUES (OLD."CaptainsLocked", OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."DraftStart", OLD."Format", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceUrl", OLD."LeagueID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublicAt", OLD."SeasonEnd", OLD."SeasonID", OLD."SeasonName", OLD."SeasonStart", OLD."SignupsClose", OLD."SignupsOpen", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
