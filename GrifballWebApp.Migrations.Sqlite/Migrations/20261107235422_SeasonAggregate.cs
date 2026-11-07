using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SeasonAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the seasons the constraint would refuse first: a source link on a season of the app's (none was ever
            // written, as only the import sets one; the link is dropped, the season is otherwise as it was)
            migrationBuilder.Sql("UPDATE \"Seasons\" SET \"HistoricalSourceUrl\" = NULL WHERE \"HistoricalSourceID\" IS NULL AND \"HistoricalSourceUrl\" IS NOT NULL;");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Seasons_History_Update";
                DROP TRIGGER IF EXISTS "Seasons_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Seasons_SourceUrlOnlyImported",
                schema: "Event",
                table: "Seasons",
                sql: "HistoricalSourceUrl IS NULL OR HistoricalSourceID IS NOT NULL");

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
            // A link the constraint settled stays dropped: no season of the app's had one to keep
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Seasons_History_Update";
                DROP TRIGGER IF EXISTS "Seasons_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Seasons_SourceUrlOnlyImported",
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
