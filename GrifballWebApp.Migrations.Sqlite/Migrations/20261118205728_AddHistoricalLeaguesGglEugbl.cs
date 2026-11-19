using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalLeaguesGglEugbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Leagues_History_Update";
                DROP TRIGGER IF EXISTS "Leagues_History_Delete";
                """);

            migrationBuilder.InsertData(
                schema: "Event",
                table: "Leagues",
                columns: new[] { "LeagueID", "CreatedAt", "CreatedByID", "LeagueName", "ModifiedAt", "ModifiedByID", "Slug" },
                values: new object[,]
                {
                    { 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "GGL", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "ggl" },
                    { 7, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "EUGBL", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "eugbl" }
                });

            migrationBuilder.Sql("""
                CREATE TRIGGER "Leagues_History_Update" AFTER UPDATE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Leagues" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Leagues_History_Delete" AFTER DELETE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Leagues_History_Update";
                DROP TRIGGER IF EXISTS "Leagues_History_Delete";
                """);

            migrationBuilder.DeleteData(
                schema: "Event",
                table: "Leagues",
                keyColumn: "LeagueID",
                keyValue: 6);

            migrationBuilder.DeleteData(
                schema: "Event",
                table: "Leagues",
                keyColumn: "LeagueID",
                keyValue: 7);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Leagues_History_Update" AFTER UPDATE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Leagues" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Leagues_History_Delete" AFTER DELETE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
