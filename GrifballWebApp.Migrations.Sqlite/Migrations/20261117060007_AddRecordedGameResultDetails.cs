using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedGameResultDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Games_History_Update";
                DROP TRIGGER IF EXISTS "Games_History_Delete";
                """);

            migrationBuilder.AddColumn<string>(
                name: "Host",
                schema: "Recorded",
                table: "GamesHistory",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.AddColumn<bool>(
                name: "IsForfeit",
                schema: "Recorded",
                table: "GamesHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vod",
                schema: "Recorded",
                table: "GamesHistory",
                type: "TEXT",
                maxLength: 500,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "Host",
                schema: "Recorded",
                table: "Games",
                type: "TEXT",
                maxLength: 100,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.AddColumn<bool>(
                name: "IsForfeit",
                schema: "Recorded",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Vod",
                schema: "Recorded",
                table: "Games",
                type: "TEXT",
                maxLength: 500,
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Games_History_Update" AFTER UPDATE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "Host", "IsForfeit", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "Origin", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "Vod", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."Host", OLD."IsForfeit", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Origin", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", OLD."Vod", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Games" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Games_History_Delete" AFTER DELETE ON "Games" FOR EACH ROW BEGIN
                    INSERT INTO "GamesHistory" ("CreatedAt", "CreatedByID", "GameVersionID", "Gametype", "Host", "IsForfeit", "IsPlayoffs", "Map", "ModifiedAt", "ModifiedByID", "Origin", "PeriodStart", "PlayedAt", "RecordedGameID", "SeasonID", "SourceKey", "Vod", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionID", OLD."Gametype", OLD."Host", OLD."IsForfeit", OLD."IsPlayoffs", OLD."Map", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Origin", OLD."PeriodStart", OLD."PlayedAt", OLD."RecordedGameID", OLD."SeasonID", OLD."SourceKey", OLD."Vod", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
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
                name: "Host",
                schema: "Recorded",
                table: "GamesHistory");

            migrationBuilder.DropColumn(
                name: "IsForfeit",
                schema: "Recorded",
                table: "GamesHistory");

            migrationBuilder.DropColumn(
                name: "Vod",
                schema: "Recorded",
                table: "GamesHistory");

            migrationBuilder.DropColumn(
                name: "Host",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsForfeit",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Vod",
                schema: "Recorded",
                table: "Games");

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
