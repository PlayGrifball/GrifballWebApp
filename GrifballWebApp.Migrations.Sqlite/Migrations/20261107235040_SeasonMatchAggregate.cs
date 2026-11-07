using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SeasonMatchAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settled before the checks: rows from before SeasonMatch owned its results, which the checks would refuse.
            // A BYE side that holds a team: the team was sent on there, so the BYE is stale and goes. Then any pair of
            // results no rule makes (CK_Event_SeasonMatches_LegalResults) can't be trusted: the match is reopened,
            // undecided, its games and score kept, for a commissioner to decide again.
            migrationBuilder.Sql("""
                UPDATE "SeasonMatches" SET "HomeTeamResult" = NULL WHERE "HomeTeamResult" = 3 AND "HomeTeamID" IS NOT NULL;
                UPDATE "SeasonMatches" SET "AwayTeamResult" = NULL WHERE "AwayTeamResult" = 3 AND "AwayTeamID" IS NOT NULL;
                UPDATE "SeasonMatches" SET "HomeTeamResult" = NULL, "AwayTeamResult" = NULL
                    WHERE NOT ((COALESCE("HomeTeamResult", -1) = -1 AND COALESCE("AwayTeamResult", -1) = -1) OR (COALESCE("HomeTeamResult", -1) = 0 AND COALESCE("AwayTeamResult", -1) IN (1, 2, 3)) OR (COALESCE("HomeTeamResult", -1) = 1 AND COALESCE("AwayTeamResult", -1) = 0) OR (COALESCE("HomeTeamResult", -1) = 2 AND COALESCE("AwayTeamResult", -1) IN (0, 2)) OR (COALESCE("HomeTeamResult", -1) = 3 AND COALESCE("AwayTeamResult", -1) IN (-1, 0, 3)) OR (COALESCE("HomeTeamResult", -1) = -1 AND COALESCE("AwayTeamResult", -1) = 3));
                """);

            // A decided match keeps its time, so a pending reschedule request of one has nothing left to approve (#297): it is
            // closed as the app now closes it (MatchReschedule.CloseBecauseDecided), rejected by its requester at the match's
            // last change, after the results above are settled (a reopened match keeps its request).
            migrationBuilder.Sql("""
                UPDATE "MatchReschedules" SET "Status" = 2, "ApprovedByUserID" = "RequestedByUserID",
                    "ProcessedAt" = (SELECT m."ModifiedAt" FROM "SeasonMatches" m WHERE m."SeasonMatchID" = "MatchReschedules"."SeasonMatchID"),
                    "CommissionerNotes" = 'Closed automatically: the match was decided before this request was.'
                WHERE "Status" = 0 AND "SeasonMatchID" IN
                    (SELECT "SeasonMatchID" FROM "SeasonMatches" WHERE "HomeTeamResult" IS NOT NULL OR "AwayTeamResult" IS NOT NULL);
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeasonMatches_History_Update";
                DROP TRIGGER IF EXISTS "SeasonMatches_History_Delete";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonMatches_ByeHasNoTeam",
                schema: "Event",
                table: "SeasonMatches",
                sql: "(HomeTeamResult IS NULL OR HomeTeamResult <> 3 OR HomeTeamID IS NULL) AND (AwayTeamResult IS NULL OR AwayTeamResult <> 3 OR AwayTeamID IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonMatches_LegalResults",
                schema: "Event",
                table: "SeasonMatches",
                sql: "(COALESCE(HomeTeamResult, -1) = -1 AND COALESCE(AwayTeamResult, -1) = -1) OR (COALESCE(HomeTeamResult, -1) = 0 AND COALESCE(AwayTeamResult, -1) IN (1, 2, 3)) OR (COALESCE(HomeTeamResult, -1) = 1 AND COALESCE(AwayTeamResult, -1) = 0) OR (COALESCE(HomeTeamResult, -1) = 2 AND COALESCE(AwayTeamResult, -1) IN (0, 2)) OR (COALESCE(HomeTeamResult, -1) = 3 AND COALESCE(AwayTeamResult, -1) IN (-1, 0, 3)) OR (COALESCE(HomeTeamResult, -1) = -1 AND COALESCE(AwayTeamResult, -1) = 3)");

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeasonMatches_History_Update" AFTER UPDATE ON "SeasonMatches" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonMatchesHistory" ("AwayTeamID", "AwayTeamResult", "AwayTeamScore", "BestOf", "CreatedAt", "CreatedByID", "HomeTeamID", "HomeTeamResult", "HomeTeamScore", "ModifiedAt", "ModifiedByID", "PeriodStart", "ScheduledTime", "SeasonID", "SeasonMatchID", "PeriodEnd") VALUES (OLD."AwayTeamID", OLD."AwayTeamResult", OLD."AwayTeamScore", OLD."BestOf", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamID", OLD."HomeTeamResult", OLD."HomeTeamScore", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ScheduledTime", OLD."SeasonID", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeasonMatches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeasonMatches_History_Delete" AFTER DELETE ON "SeasonMatches" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonMatchesHistory" ("AwayTeamID", "AwayTeamResult", "AwayTeamScore", "BestOf", "CreatedAt", "CreatedByID", "HomeTeamID", "HomeTeamResult", "HomeTeamScore", "ModifiedAt", "ModifiedByID", "PeriodStart", "ScheduledTime", "SeasonID", "SeasonMatchID", "PeriodEnd") VALUES (OLD."AwayTeamID", OLD."AwayTeamResult", OLD."AwayTeamScore", OLD."BestOf", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamID", OLD."HomeTeamResult", OLD."HomeTeamScore", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ScheduledTime", OLD."SeasonID", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "SeasonMatches_History_Update";
                DROP TRIGGER IF EXISTS "SeasonMatches_History_Delete";
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonMatches_ByeHasNoTeam",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonMatches_LegalResults",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.Sql("""
                CREATE TRIGGER "SeasonMatches_History_Update" AFTER UPDATE ON "SeasonMatches" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonMatchesHistory" ("AwayTeamID", "AwayTeamResult", "AwayTeamScore", "BestOf", "CreatedAt", "CreatedByID", "HomeTeamID", "HomeTeamResult", "HomeTeamScore", "ModifiedAt", "ModifiedByID", "PeriodStart", "ScheduledTime", "SeasonID", "SeasonMatchID", "PeriodEnd") VALUES (OLD."AwayTeamID", OLD."AwayTeamResult", OLD."AwayTeamScore", OLD."BestOf", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamID", OLD."HomeTeamResult", OLD."HomeTeamScore", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ScheduledTime", OLD."SeasonID", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "SeasonMatches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "SeasonMatches_History_Delete" AFTER DELETE ON "SeasonMatches" FOR EACH ROW BEGIN
                    INSERT INTO "SeasonMatchesHistory" ("AwayTeamID", "AwayTeamResult", "AwayTeamScore", "BestOf", "CreatedAt", "CreatedByID", "HomeTeamID", "HomeTeamResult", "HomeTeamScore", "ModifiedAt", "ModifiedByID", "PeriodStart", "ScheduledTime", "SeasonID", "SeasonMatchID", "PeriodEnd") VALUES (OLD."AwayTeamID", OLD."AwayTeamResult", OLD."AwayTeamScore", OLD."BestOf", OLD."CreatedAt", OLD."CreatedByID", OLD."HomeTeamID", OLD."HomeTeamResult", OLD."HomeTeamScore", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."ScheduledTime", OLD."SeasonID", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
