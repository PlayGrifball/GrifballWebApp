using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
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
                UPDATE [MatchReschedules] SET [Status] = 2
                WHERE [Status] = 0 AND [MatchRescheduleID] <> COALESCE(
                    (SELECT p.[MatchRescheduleID] FROM [MatchReschedules] p JOIN [Event].[SeasonMatches] m ON m.[ActiveRescheduleRequestId] = p.[MatchRescheduleID]
                        WHERE m.[SeasonMatchID] = p.[SeasonMatchID] AND p.[SeasonMatchID] = [MatchReschedules].[SeasonMatchID] AND p.[Status] = 0),
                    (SELECT MAX(n.[MatchRescheduleID]) FROM [MatchReschedules] n WHERE n.[SeasonMatchID] = [MatchReschedules].[SeasonMatchID] AND n.[Status] = 0));
                UPDATE [MatchReschedules] SET [ProcessedAt] = COALESCE([ProcessedAt], [ModifiedAt]),
                    [ApprovedByUserID] = COALESCE([ApprovedByUserID], [ModifiedByID], [RequestedByUserID])
                WHERE [Status] <> 0;
                UPDATE [MatchReschedules] SET [ProcessedAt] = NULL, [ApprovedByUserID] = NULL WHERE [Status] = 0;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonMatches_MatchReschedules_ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches");

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
                table: "SeasonMatches");

            migrationBuilder.CreateIndex(
                name: "IX_MatchReschedules_SeasonMatchID",
                table: "MatchReschedules",
                column: "SeasonMatchID",
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MatchReschedules_DecidedHasDecider",
                table: "MatchReschedules",
                sql: "(Status = 0 AND ApprovedByUserID IS NULL AND ProcessedAt IS NULL) OR (Status <> 0 AND ApprovedByUserID IS NOT NULL AND ProcessedAt IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchReschedules_SeasonMatchID",
                table: "MatchReschedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MatchReschedules_DecidedHasDecider",
                table: "MatchReschedules");

            migrationBuilder.AddColumn<int>(
                name: "ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches",
                type: "int",
                nullable: true);

            // The pointer again, from the pending request: a match's active one
            migrationBuilder.Sql("""
                UPDATE [Event].[SeasonMatches] SET [ActiveRescheduleRequestId] =
                    (SELECT r.[MatchRescheduleID] FROM [MatchReschedules] r WHERE r.[SeasonMatchID] = [Event].[SeasonMatches].[SeasonMatchID] AND r.[Status] = 0);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatches_ActiveRescheduleRequestId",
                schema: "Event",
                table: "SeasonMatches",
                column: "ActiveRescheduleRequestId",
                unique: true,
                filter: "[ActiveRescheduleRequestId] IS NOT NULL");

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
