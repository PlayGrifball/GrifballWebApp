using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class TeamAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the teams the constraint would refuse first: half a record of the spreadsheet's (the import always
            // wrote both halves, so none is expected). The blank half becomes 0, as the standings already counted it
            // (TeamRecord.Of): every team keeps the record it showed.
            migrationBuilder.Sql("UPDATE \"Teams\" SET \"RecordedWins\" = COALESCE(\"RecordedWins\", 0), \"RecordedLosses\" = COALESCE(\"RecordedLosses\", 0) WHERE (\"RecordedWins\" IS NULL AND \"RecordedLosses\" IS NOT NULL) OR (\"RecordedWins\" IS NOT NULL AND \"RecordedLosses\" IS NULL);");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Teams_History_Update";
                DROP TRIGGER IF EXISTS "Teams_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Teams_RecordBothOrNeither",
                schema: "Event",
                table: "Teams",
                sql: "(RecordedWins IS NULL AND RecordedLosses IS NULL) OR (RecordedWins IS NOT NULL AND RecordedLosses IS NOT NULL)");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Teams_History_Update" AFTER UPDATE ON "Teams" FOR EACH ROW BEGIN
                    INSERT INTO "TeamsHistory" ("CaptainID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedLosses", "RecordedWins", "SeasonID", "TeamID", "TeamName", "PeriodEnd") VALUES (OLD."CaptainID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedLosses", OLD."RecordedWins", OLD."SeasonID", OLD."TeamID", OLD."TeamName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Teams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Teams_History_Delete" AFTER DELETE ON "Teams" FOR EACH ROW BEGIN
                    INSERT INTO "TeamsHistory" ("CaptainID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedLosses", "RecordedWins", "SeasonID", "TeamID", "TeamName", "PeriodEnd") VALUES (OLD."CaptainID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedLosses", OLD."RecordedWins", OLD."SeasonID", OLD."TeamID", OLD."TeamName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A half record the constraint settled keeps its 0: it is the record the team showed
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Teams_History_Update";
                DROP TRIGGER IF EXISTS "Teams_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Teams_RecordBothOrNeither",
                schema: "Event",
                table: "Teams");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Teams_History_Update" AFTER UPDATE ON "Teams" FOR EACH ROW BEGIN
                    INSERT INTO "TeamsHistory" ("CaptainID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedLosses", "RecordedWins", "SeasonID", "TeamID", "TeamName", "PeriodEnd") VALUES (OLD."CaptainID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedLosses", OLD."RecordedWins", OLD."SeasonID", OLD."TeamID", OLD."TeamName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Teams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Teams_History_Delete" AFTER DELETE ON "Teams" FOR EACH ROW BEGIN
                    INSERT INTO "TeamsHistory" ("CaptainID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedLosses", "RecordedWins", "SeasonID", "TeamID", "TeamName", "PeriodEnd") VALUES (OLD."CaptainID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedLosses", OLD."RecordedWins", OLD."SeasonID", OLD."TeamID", OLD."TeamName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
