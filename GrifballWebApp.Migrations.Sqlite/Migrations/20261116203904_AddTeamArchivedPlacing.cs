using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamArchivedPlacing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Teams_History_Update";
                DROP TRIGGER IF EXISTS "Teams_History_Delete";
                """);

            migrationBuilder.AddColumn<int>(
                name: "ArchivedFinish",
                schema: "Event",
                table: "TeamsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchivedRank",
                schema: "Event",
                table: "TeamsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchivedFinish",
                schema: "Event",
                table: "Teams",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchivedRank",
                schema: "Event",
                table: "Teams",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_Teams_ArchivedPlacingFromFirst",
                schema: "Event",
                table: "Teams",
                sql: "(ArchivedRank IS NULL OR ArchivedRank >= 1) AND (ArchivedFinish IS NULL OR ArchivedFinish >= 1)");

            migrationBuilder.Sql("""
                CREATE TRIGGER "Teams_History_Update" AFTER UPDATE ON "Teams" FOR EACH ROW BEGIN
                    INSERT INTO "TeamsHistory" ("ArchivedFinish", "ArchivedRank", "CaptainID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedLosses", "RecordedWins", "SeasonID", "TeamID", "TeamName", "PeriodEnd") VALUES (OLD."ArchivedFinish", OLD."ArchivedRank", OLD."CaptainID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedLosses", OLD."RecordedWins", OLD."SeasonID", OLD."TeamID", OLD."TeamName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "Teams" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "Teams_History_Delete" AFTER DELETE ON "Teams" FOR EACH ROW BEGIN
                    INSERT INTO "TeamsHistory" ("ArchivedFinish", "ArchivedRank", "CaptainID", "CreatedAt", "CreatedByID", "ModifiedAt", "ModifiedByID", "PeriodStart", "RecordedLosses", "RecordedWins", "SeasonID", "TeamID", "TeamName", "PeriodEnd") VALUES (OLD."ArchivedFinish", OLD."ArchivedRank", OLD."CaptainID", OLD."CreatedAt", OLD."CreatedByID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RecordedLosses", OLD."RecordedWins", OLD."SeasonID", OLD."TeamID", OLD."TeamName", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Teams_History_Update";
                DROP TRIGGER IF EXISTS "Teams_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_Teams_ArchivedPlacingFromFirst",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ArchivedFinish",
                schema: "Event",
                table: "TeamsHistory");

            migrationBuilder.DropColumn(
                name: "ArchivedRank",
                schema: "Event",
                table: "TeamsHistory");

            migrationBuilder.DropColumn(
                name: "ArchivedFinish",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ArchivedRank",
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
