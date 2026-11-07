using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class GamertagClaimDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAt",
                schema: "Event",
                table: "GamertagClaims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecidedByUserID",
                schema: "Event",
                table: "GamertagClaims",
                type: "int",
                nullable: true);

            // Settle what the new constraints would refuse, then fill the decision from the audit columns: a claim was decided by
            // the first write that gave it its status, so its time and user are those of its earliest version that isn't
            // pending, in its history or the row itself (#300). Only a rejected claim keeps a rejection reason.
            migrationBuilder.Sql("""UPDATE [Event].[GamertagClaims] SET [RejectionReason] = NULL WHERE [Status] <> 3 AND [RejectionReason] IS NOT NULL;""");
            migrationBuilder.Sql("""
                UPDATE c SET [DecidedAt] = (
                    SELECT MIN(v.[ModifiedAt]) FROM (
                        SELECT h.[GamertagClaimID], h.[Status], h.[ModifiedAt] FROM [Event].[GamertagClaimsHistory] h
                        UNION ALL SELECT r.[GamertagClaimID], r.[Status], r.[ModifiedAt] FROM [Event].[GamertagClaims] r) v
                    WHERE v.[GamertagClaimID] = c.[GamertagClaimID] AND v.[Status] <> 1)
                FROM [Event].[GamertagClaims] c
                WHERE c.[Status] <> 1;
                """);
            migrationBuilder.Sql("""
                UPDATE c SET [DecidedByUserID] = (
                    SELECT MAX(v.[ModifiedByID]) FROM (
                        SELECT h.[GamertagClaimID], h.[Status], h.[ModifiedAt], h.[ModifiedByID] FROM [Event].[GamertagClaimsHistory] h
                        UNION ALL SELECT r.[GamertagClaimID], r.[Status], r.[ModifiedAt], r.[ModifiedByID] FROM [Event].[GamertagClaims] r) v
                    WHERE v.[GamertagClaimID] = c.[GamertagClaimID] AND v.[Status] <> 1 AND v.[ModifiedAt] = c.[DecidedAt])
                FROM [Event].[GamertagClaims] c
                WHERE c.[Status] <> 1;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_GamertagClaims_DecidedOnceDecided",
                schema: "Event",
                table: "GamertagClaims",
                sql: "(Status = 1 AND DecidedAt IS NULL AND DecidedByUserID IS NULL) OR (Status <> 1 AND DecidedAt IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_GamertagClaims_RejectionReasonOnlyWhenRejected",
                schema: "Event",
                table: "GamertagClaims",
                sql: "Status = 3 OR RejectionReason IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_GamertagClaims_DecidedOnceDecided",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_GamertagClaims_RejectionReasonOnlyWhenRejected",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropColumn(
                name: "DecidedByUserID",
                schema: "Event",
                table: "GamertagClaims");
        }
    }
}
