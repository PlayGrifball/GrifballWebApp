using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SeasonSignupAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeasonSignups_History_Update";
                DROP TRIGGER IF EXISTS "SeasonSignups_History_Delete";
                """);

            migrationBuilder.DropColumn(
                name: "Approved",
                schema: "Event",
                table: "SeasonSignupsHistory");

            migrationBuilder.DropColumn(
                name: "Approved",
                schema: "Event",
                table: "SeasonSignups");

            // Settle the rows the constraints would refuse first: a team name trimmed (a blank one goes), and no help
            // drafting for a player who won't captain
            migrationBuilder.Sql("""
                UPDATE "SeasonSignups" SET "TeamName" = NULLIF(LTRIM(RTRIM("TeamName")), '')
                WHERE "TeamName" = '' OR "TeamName" <> LTRIM(RTRIM("TeamName"));
                UPDATE "SeasonSignups" SET "RequiresAssistanceDrafting" = 0 WHERE "WillCaptain" = 0 AND "RequiresAssistanceDrafting" = 1;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonSignups_AssistanceOnlyForCaptains",
                schema: "Event",
                table: "SeasonSignups",
                sql: "CAST(RequiresAssistanceDrafting AS INT) = 0 OR CAST(WillCaptain AS INT) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonSignups_TeamNameTrimmed",
                schema: "Event",
                table: "SeasonSignups",
                sql: "TeamName IS NULL OR (TeamName <> '' AND TeamName = LTRIM(RTRIM(TeamName)))");

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeasonSignups_History_Update" AFTER UPDATE ON "SeasonSignups" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonSignupsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RequiresAssistanceDrafting", "SeasonID", "SeasonSignupID", "TeamName", "Timestamp", "UserID", "WillCaptain", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RequiresAssistanceDrafting", OLD."SeasonID", OLD."SeasonSignupID", OLD."TeamName", OLD."Timestamp", OLD."UserID", OLD."WillCaptain", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeasonSignups" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeasonSignups_History_Delete" AFTER DELETE ON "SeasonSignups" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonSignupsHistory" ("CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RequiresAssistanceDrafting", "SeasonID", "SeasonSignupID", "TeamName", "Timestamp", "UserID", "WillCaptain", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RequiresAssistanceDrafting", OLD."SeasonID", OLD."SeasonSignupID", OLD."TeamName", OLD."Timestamp", OLD."UserID", OLD."WillCaptain", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeasonSignups_History_Update";
                DROP TRIGGER IF EXISTS "SeasonSignups_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonSignups_AssistanceOnlyForCaptains",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonSignups_TeamNameTrimmed",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                schema: "Event",
                table: "SeasonSignupsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Approved",
                schema: "Event",
                table: "SeasonSignups",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeasonSignups_History_Update" AFTER UPDATE ON "SeasonSignups" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonSignupsHistory" ("Approved", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RequiresAssistanceDrafting", "SeasonID", "SeasonSignupID", "TeamName", "Timestamp", "UserID", "WillCaptain", "PeriodEnd") VALUES (OLD."Approved", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RequiresAssistanceDrafting", OLD."SeasonID", OLD."SeasonSignupID", OLD."TeamName", OLD."Timestamp", OLD."UserID", OLD."WillCaptain", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeasonSignups" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeasonSignups_History_Delete" AFTER DELETE ON "SeasonSignups" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonSignupsHistory" ("Approved", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RequiresAssistanceDrafting", "SeasonID", "SeasonSignupID", "TeamName", "Timestamp", "UserID", "WillCaptain", "PeriodEnd") VALUES (OLD."Approved", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RequiresAssistanceDrafting", OLD."SeasonID", OLD."SeasonSignupID", OLD."TeamName", OLD."Timestamp", OLD."UserID", OLD."WillCaptain", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
