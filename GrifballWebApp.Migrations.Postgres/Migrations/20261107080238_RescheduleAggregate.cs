using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RescheduleAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A match's active request was kept twice: as its pointer and as a pending request. The pending request is the one
            // kept now (a unique index: one per match), and a decided request has its decider and time (a check). Nothing the app
            // did breaks either, but the index and the check would refuse rows that do, so they are settled first (#297):
            // - a second pending request of a match is rejected: the match's pointer's is kept, or its newest when it has none;
            // - a decision missing its decider or time takes them from the row's last change (its decider made it), the
            //   requester when that isn't known;
            // - a pending request has neither.
            migrationBuilder.Sql("""
                UPDATE "MatchReschedules" SET "Status" = 2
                WHERE "Status" = 0 AND "MatchRescheduleID" <> COALESCE(
                    (SELECT p."MatchRescheduleID" FROM "MatchReschedules" p JOIN "Event"."SeasonMatches" m ON m."ActiveRescheduleRequestId" = p."MatchRescheduleID"
                        WHERE m."SeasonMatchID" = p."SeasonMatchID" AND p."SeasonMatchID" = "MatchReschedules"."SeasonMatchID" AND p."Status" = 0),
                    (SELECT MAX(n."MatchRescheduleID") FROM "MatchReschedules" n WHERE n."SeasonMatchID" = "MatchReschedules"."SeasonMatchID" AND n."Status" = 0));
                UPDATE "MatchReschedules" SET "ProcessedAt" = COALESCE("ProcessedAt", "ModifiedAt"),
                    "ApprovedByUserID" = COALESCE("ApprovedByUserID", "ModifiedByID", "RequestedByUserID")
                WHERE "Status" <> 0;
                UPDATE "MatchReschedules" SET "ProcessedAt" = NULL, "ApprovedByUserID" = NULL WHERE "Status" = 0;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonMatches_MatchReschedules_ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropIndex(
                name: "IX_SeasonMatchesHistory_ActiveRescheduleRequestId_PeriodEnd",
                schema: "Event",
                table: "SeasonMatchesHistory");

            migrationBuilder.DropIndex(
                name: "IX_SeasonMatches_ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropIndex(
                name: "IX_MatchReschedules_SeasonMatchID",
                table: "MatchReschedules");

            migrationBuilder.DropColumn(
                name: "ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatchesHistory");

            migrationBuilder.DropColumn(
                name: "ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"SeasonMatches\"');");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                table: "MatchReschedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                table: "MatchReschedules",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                table: "MatchReschedules",
                type: "timestamp with time zone",
                nullable: false,
                computedColumnSql: "'infinity'::timestamp with time zone",
                stored: true);

            migrationBuilder.CreateTable(
                name: "MatchReschedulesHistory",
                columns: table => new
                {
                    ApprovedByUserID = table.Column<int>(type: "integer", nullable: true),
                    CommissionerNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "case_insensitive"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DiscordThreadID = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    MatchRescheduleID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    NewScheduledTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OriginalScheduledTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "case_insensitive"),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedByUserID = table.Column<int>(type: "integer", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_SeasonMatchID",
                table: "MatchReschedules",
                column: "SeasonMatchID",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchReschedules_DecidedHasDecider",
                table: "MatchReschedules",
                sql: "(\"Status\" = 0 AND \"ApprovedByUserID\" IS NULL AND \"ProcessedAt\" IS NULL) OR (\"Status\" <> 0 AND \"ApprovedByUserID\" IS NOT NULL AND \"ProcessedAt\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedulesHistory_ApprovedByUserID_PeriodEnd",
                table: "MatchReschedulesHistory",
                columns: new[] { "ApprovedByUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedulesHistory_MatchRescheduleID_PeriodEnd",
                table: "MatchReschedulesHistory",
                columns: new[] { "MatchRescheduleID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedulesHistory_RequestedByUserID_PeriodEnd",
                table: "MatchReschedulesHistory",
                columns: new[] { "RequestedByUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedulesHistory_SeasonMatchID_PeriodEnd",
                table: "MatchReschedulesHistory",
                columns: new[] { "SeasonMatchID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"MatchReschedules\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"MatchReschedules\"');");

            migrationBuilder.DropTable(
                name: "MatchReschedulesHistory");

            migrationBuilder.DropIndex(
                name: "IX_MatchReschedules_SeasonMatchID",
                table: "MatchReschedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchReschedules_DecidedHasDecider",
                table: "MatchReschedules");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "MatchReschedules");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "MatchReschedules");

            migrationBuilder.AddColumn<int>(
                name: "ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatchesHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches",
                type: "integer",
                nullable: true);

            // The pointer again, from the pending request: a match's active one
            migrationBuilder.Sql("""
                UPDATE "Event"."SeasonMatches" SET "ActiveRescheduleRequestId" =
                    (SELECT r."MatchRescheduleID" FROM "MatchReschedules" r WHERE r."SeasonMatchID" = "Event"."SeasonMatches"."SeasonMatchID" AND r."Status" = 0);
                """);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"SeasonMatches\"');");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatchesHistory_ActiveRescheduleRequestId_PeriodEnd",
                schema: "Event",
                table: "SeasonMatchesHistory",
                columns: new[] { "ActiveRescheduleRequestId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatches_ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches",
                column: "ActiveRescheduleRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_SeasonMatchID",
                table: "MatchReschedules",
                column: "SeasonMatchID");

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonMatches_MatchReschedules_ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches",
                column: "ActiveRescheduleRequestId",
                principalTable: "MatchReschedules",
                principalColumn: "MatchRescheduleID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
