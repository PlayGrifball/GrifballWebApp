using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchGameVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Matches_History_Update";
                DROP TRIGGER IF EXISTS "Matches_History_Delete";
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "UgcGameVariantAssetID",
                schema: "Infinite",
                table: "MatchesHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "MatchesHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UgcGameVariantAssetID",
                schema: "Infinite",
                table: "Matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "Matches",
                type: "TEXT",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Matches_History_Update";
                DROP TRIGGER IF EXISTS "Matches_History_Delete";
                """);

            migrationBuilder.DropColumn(
                name: "UgcGameVariantAssetID",
                schema: "Infinite",
                table: "MatchesHistory");

            migrationBuilder.DropColumn(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "MatchesHistory");

            migrationBuilder.DropColumn(
                name: "UgcGameVariantAssetID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Matches_History_Update" AFTER UPDATE ON "Matches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchesHistory" ("CreatedAt", "CreatedByID", "Duration", "EndTime", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartTime", "StatsPullDate", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Duration", OLD."EndTime", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartTime", OLD."StatsPullDate", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Matches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Matches_History_Delete" AFTER DELETE ON "Matches" FOR EACH ROW BEGIN
                    INSERT INTO "MatchesHistory" ("CreatedAt", "CreatedByID", "Duration", "EndTime", "MatchID", "ModifiedAt", "ModifiedByID", "PeriodStart", "StartTime", "StatsPullDate", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."Duration", OLD."EndTime", OLD."MatchID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."StartTime", OLD."StatsPullDate", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
