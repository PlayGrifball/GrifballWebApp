using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle what the new constraints would refuse (#320): a job ends with a finish time and without its archive (an
            // unknown status counts as failed), only an upload's import keeps one; a review is decided with a time (an unknown
            // status waits for review), and only an approval names the import it queued; the scheduler has one row, ID 1.
            // A missing time is taken from ModifiedAt, the row's last write through SaveChanges: a review's decision, but a
            // job's enqueue, since the worker's moves bypass the audit (the app always wrote FinishedAt, so only a job edited
            // by hand lacks one)
            migrationBuilder.Sql("""UPDATE "HistoricalImportJobs" SET "Status" = 4, "FinishedAt" = COALESCE("FinishedAt", "ModifiedAt") WHERE "Status" NOT IN (1, 2, 3, 4);""");
            migrationBuilder.Sql("""UPDATE "HistoricalImportJobs" SET "FinishedAt" = "ModifiedAt" WHERE "Status" IN (3, 4) AND "FinishedAt" IS NULL;""");
            migrationBuilder.Sql("""UPDATE "HistoricalImportJobs" SET "FinishedAt" = NULL WHERE "Status" IN (1, 2) AND "FinishedAt" IS NOT NULL;""");
            migrationBuilder.Sql("""UPDATE "HistoricalImportJobs" SET "Payload" = NULL WHERE ("Status" NOT IN (1, 2) OR "Kind" <> 1) AND "Payload" IS NOT NULL;""");
            migrationBuilder.Sql("""UPDATE "HistoricalSeasonReviews" SET "Status" = 2 WHERE "Status" NOT IN (1, 2, 3, 4);""");
            migrationBuilder.Sql("""UPDATE "HistoricalSeasonReviews" SET "DecidedAt" = "ModifiedAt" WHERE "Status" IN (3, 4) AND "DecidedAt" IS NULL;""");
            migrationBuilder.Sql("""UPDATE "HistoricalSeasonReviews" SET "DecidedAt" = NULL WHERE "Status" IN (1, 2) AND "DecidedAt" IS NOT NULL;""");
            migrationBuilder.Sql("""UPDATE "HistoricalSeasonReviews" SET "ApprovalJobID" = NULL WHERE "Status" <> 3 AND "ApprovalJobID" IS NOT NULL;""");
            migrationBuilder.Sql("""DELETE FROM "HistoricalImportSettings" WHERE "HistoricalImportSettingsID" <> 1;""");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_HistoricalSeasonReviews_ApprovalJobOnlyWhenApproved",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                sql: "ApprovalJobID IS NULL OR Status = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_HistoricalSeasonReviews_DecidedOnceDecided",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                sql: "(Status IN (3, 4) AND DecidedAt IS NOT NULL) OR (Status IN (1, 2) AND DecidedAt IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_HistoricalImportSettings_Singleton",
                schema: "Event",
                table: "HistoricalImportSettings",
                sql: "HistoricalImportSettingsID = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_HistoricalImportJobs_FinishedOnceEnded",
                schema: "Event",
                table: "HistoricalImportJobs",
                sql: "(Status IN (3, 4) AND FinishedAt IS NOT NULL) OR (Status IN (1, 2) AND FinishedAt IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_HistoricalImportJobs_PayloadOnlyForImports",
                schema: "Event",
                table: "HistoricalImportJobs",
                sql: "Payload IS NULL OR Kind = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_HistoricalImportJobs_PayloadUntilEnded",
                schema: "Event",
                table: "HistoricalImportJobs",
                sql: "Payload IS NULL OR Status IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_HistoricalSeasonReviews_ApprovalJobOnlyWhenApproved",
                schema: "Event",
                table: "HistoricalSeasonReviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_HistoricalSeasonReviews_DecidedOnceDecided",
                schema: "Event",
                table: "HistoricalSeasonReviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_HistoricalImportSettings_Singleton",
                schema: "Event",
                table: "HistoricalImportSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_HistoricalImportJobs_FinishedOnceEnded",
                schema: "Event",
                table: "HistoricalImportJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_HistoricalImportJobs_PayloadOnlyForImports",
                schema: "Event",
                table: "HistoricalImportJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_HistoricalImportJobs_PayloadUntilEnded",
                schema: "Event",
                table: "HistoricalImportJobs");
        }
    }
}
