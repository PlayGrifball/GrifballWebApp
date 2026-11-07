using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedGameOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Games_History_Update";
                DROP TRIGGER IF EXISTS "Games_History_Delete";
                """);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                schema: "Recorded",
                table: "GamesHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                schema: "Recorded",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // Games entered by hand before Origin existed (manual match entry, test and preview environments): told by their
            // key then. Before the history triggers are back, as the column was added
            migrationBuilder.Sql("UPDATE \"Games\" SET \"Origin\" = 2 WHERE \"SourceKey\" LIKE 'manual/%';");

            // Games entered by hand before Origin existed (manual match entry, test and preview environments): told by their
            // key then. Before the history triggers are back, as the column was added
            migrationBuilder.Sql("UPDATE \"Games\" SET \"Origin\" = 2 WHERE \"SourceKey\" LIKE 'manual/%';");

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
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Games_History_Update";
                DROP TRIGGER IF EXISTS "Games_History_Delete";
                """);

            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "Recorded",
                table: "GamesHistory");

            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Games_History_Update" AFTER UPDATE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Games" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Games_History_Delete" AFTER DELETE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
