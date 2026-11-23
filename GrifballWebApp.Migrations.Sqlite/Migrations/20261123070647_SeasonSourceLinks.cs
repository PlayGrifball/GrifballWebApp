using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SeasonSourceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Seasons_History_Update";
                DROP TRIGGER IF EXISTS "Seasons_History_Delete";
                """);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceLinks",
                schema: "Event",
                table: "SeasonsHistory",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceLinks",
                schema: "Event",
                table: "Seasons",
                type: "TEXT",
                maxLength: 4000,
                nullable: true,
                defaultValueSql: "NULL");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Seasons_History_Update" AFTER UPDATE ON "Seasons" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonsHistory" ("CaptainsLocked", "CreatedAt", "CreatedByID", "Division", "DraftStart", "Format", "GameVersionID", "HistoricalSourceID", "HistoricalSourceLinks", "HistoricalSourceUrl", "LeagueID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublicAt", "SeasonEnd", "SeasonID", "SeasonName", "SeasonStart", "SignupsClose", "SignupsOpen", "PeriodEnd") VALUES (OLD."CaptainsLocked", OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."DraftStart", OLD."Format", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceLinks", OLD."HistoricalSourceUrl", OLD."LeagueID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublicAt", OLD."SeasonEnd", OLD."SeasonID", OLD."SeasonName", OLD."SeasonStart", OLD."SignupsClose", OLD."SignupsOpen", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Seasons" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Seasons_History_Delete" AFTER DELETE ON "Seasons" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonsHistory" ("CaptainsLocked", "CreatedAt", "CreatedByID", "Division", "DraftStart", "Format", "GameVersionID", "HistoricalSourceID", "HistoricalSourceLinks", "HistoricalSourceUrl", "LeagueID", "ModifiedAt", "ModifiedByID", "PeriodStart", "PublicAt", "SeasonEnd", "SeasonID", "SeasonName", "SeasonStart", "SignupsClose", "SignupsOpen", "PeriodEnd") VALUES (OLD."CaptainsLocked", OLD."CreatedAt", OLD."CreatedByID", OLD."Division", OLD."DraftStart", OLD."Format", OLD."GameVersionID", OLD."HistoricalSourceID", OLD."HistoricalSourceLinks", OLD."HistoricalSourceUrl", OLD."LeagueID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PublicAt", OLD."SeasonEnd", OLD."SeasonID", OLD."SeasonName", OLD."SeasonStart", OLD."SignupsClose", OLD."SignupsOpen", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Seasons_History_Update";
                DROP TRIGGER IF EXISTS "Seasons_History_Delete";
                """);

            migrationBuilder.DropColumn(
                name: "HistoricalSourceLinks",
                schema: "Event",
                table: "SeasonsHistory");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceLinks",
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
