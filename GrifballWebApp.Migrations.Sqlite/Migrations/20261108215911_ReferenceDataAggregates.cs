using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ReferenceDataAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameVersions_History_Update";
                DROP TRIGGER IF EXISTS "GameVersions_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Leagues_History_Update";
                DROP TRIGGER IF EXISTS "Leagues_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Ranks_History_Update";
                DROP TRIGGER IF EXISTS "Ranks_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Regions_History_Update";
                DROP TRIGGER IF EXISTS "Regions_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Other_Regions_NameNotBlank",
                schema: "Other",
                table: "Regions",
                sql: "LTRIM(RTRIM(RegionName)) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Matchmaking_Ranks_ThresholdNotNegative",
                schema: "Matchmaking",
                table: "Ranks",
                sql: "MmrThreshold >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Leagues_NameAndSlugNotBlank",
                schema: "Event",
                table: "Leagues",
                sql: "LTRIM(RTRIM(LeagueName)) <> '' AND LTRIM(RTRIM(Slug)) <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Other_GameVersions_NameAndSlugNotBlank",
                schema: "Other",
                table: "GameVersions",
                sql: "LTRIM(RTRIM(GameVersionName)) <> '' AND LTRIM(RTRIM(Slug)) <> ''");

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameVersions_History_Update" AFTER UPDATE ON "GameVersions" FOR EACH ROW BEGIN
                    INSERT INTO "GameVersionsHistory" ("CreatedAt", "CreatedByID", "GameVersionName", "GameVesionID", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionName", OLD."GameVesionID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameVersions" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameVersions_History_Delete" AFTER DELETE ON "GameVersions" FOR EACH ROW BEGIN
                    INSERT INTO "GameVersionsHistory" ("CreatedAt", "CreatedByID", "GameVersionName", "GameVesionID", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionName", OLD."GameVesionID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Leagues_History_Update" AFTER UPDATE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Leagues" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Leagues_History_Delete" AFTER DELETE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Ranks_History_Update" AFTER UPDATE ON "Ranks" FOR EACH ROW BEGIN
                    INSERT INTO "RanksHistory" ("Color", "CreatedAt", "CreatedByID", "Description", "Icon", "Id", "MmrThreshold", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "PeriodEnd") VALUES (OLD."Color", OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."Icon", OLD."Id", OLD."MmrThreshold", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Ranks" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Ranks_History_Delete" AFTER DELETE ON "Ranks" FOR EACH ROW BEGIN
                    INSERT INTO "RanksHistory" ("Color", "CreatedAt", "CreatedByID", "Description", "Icon", "Id", "MmrThreshold", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "PeriodEnd") VALUES (OLD."Color", OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."Icon", OLD."Id", OLD."MmrThreshold", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Regions_History_Update" AFTER UPDATE ON "Regions" FOR EACH ROW BEGIN
                    INSERT INTO "RegionsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RegionID", "RegionName", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RegionID", OLD."RegionName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Regions" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Regions_History_Delete" AFTER DELETE ON "Regions" FOR EACH ROW BEGIN
                    INSERT INTO "RegionsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RegionID", "RegionName", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RegionID", OLD."RegionName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GameVersions_History_Update";
                DROP TRIGGER IF EXISTS "GameVersions_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Leagues_History_Update";
                DROP TRIGGER IF EXISTS "Leagues_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Ranks_History_Update";
                DROP TRIGGER IF EXISTS "Ranks_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Regions_History_Update";
                DROP TRIGGER IF EXISTS "Regions_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Other_Regions_NameNotBlank",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Matchmaking_Ranks_ThresholdNotNegative",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Leagues_NameAndSlugNotBlank",
                schema: "Event",
                table: "Leagues");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Other_GameVersions_NameAndSlugNotBlank",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.Sql("""
                CREATE TRIGGER "GameVersions_History_Update" AFTER UPDATE ON "GameVersions" FOR EACH ROW BEGIN
                    INSERT INTO "GameVersionsHistory" ("CreatedAt", "CreatedByID", "GameVersionName", "GameVesionID", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionName", OLD."GameVesionID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GameVersions" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GameVersions_History_Delete" AFTER DELETE ON "GameVersions" FOR EACH ROW BEGIN
                    INSERT INTO "GameVersionsHistory" ("CreatedAt", "CreatedByID", "GameVersionName", "GameVesionID", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."GameVersionName", OLD."GameVesionID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Leagues_History_Update" AFTER UPDATE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Leagues" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Leagues_History_Delete" AFTER DELETE ON "Leagues" FOR EACH ROW BEGIN
                    INSERT INTO "LeaguesHistory" ("CreatedAt", "CreatedByID", "LeagueID", "LeagueName", "ModifiedAt", "ModifiedByID", "PeriodStart", "Slug", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."LeagueID", OLD."LeagueName", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Slug", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Ranks_History_Update" AFTER UPDATE ON "Ranks" FOR EACH ROW BEGIN
                    INSERT INTO "RanksHistory" ("Color", "CreatedAt", "CreatedByID", "Description", "Icon", "Id", "MmrThreshold", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "PeriodEnd") VALUES (OLD."Color", OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."Icon", OLD."Id", OLD."MmrThreshold", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Ranks" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Ranks_History_Delete" AFTER DELETE ON "Ranks" FOR EACH ROW BEGIN
                    INSERT INTO "RanksHistory" ("Color", "CreatedAt", "CreatedByID", "Description", "Icon", "Id", "MmrThreshold", "ModifiedAt", "ModifiedByID", "Name", "PeriodStart", "PeriodEnd") VALUES (OLD."Color", OLD."CreatedAt", OLD."CreatedByID", OLD."Description", OLD."Icon", OLD."Id", OLD."MmrThreshold", OLD."ModifiedAt", OLD."ModifiedByID", OLD."Name", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "Regions_History_Update" AFTER UPDATE ON "Regions" FOR EACH ROW BEGIN
                    INSERT INTO "RegionsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RegionID", "RegionName", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RegionID", OLD."RegionName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Regions" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Regions_History_Delete" AFTER DELETE ON "Regions" FOR EACH ROW BEGIN
                    INSERT INTO "RegionsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RegionID", "RegionName", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RegionID", OLD."RegionName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
