using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class GamertagClaimDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GamertagClaims_History_Update";
                DROP TRIGGER IF EXISTS "GamertagClaims_History_Delete";
                """);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAt",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecidedByUserID",
                schema: "Event",
                table: "GamertagClaimsHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAt",
                schema: "Event",
                table: "GamertagClaims",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecidedByUserID",
                schema: "Event",
                table: "GamertagClaims",
                type: "INTEGER",
                nullable: true);

            // Settle what the new constraints would refuse, then fill the decision from the audit columns: a claim was decided by
            // the first write that gave it its status, so its time and user are those of its earliest version that isn't
            // pending, in its history or the row itself (#300). Only a rejected claim keeps a rejection reason.
            migrationBuilder.Sql("""UPDATE "GamertagClaims" SET "RejectionReason" = NULL WHERE "Status" <> 3 AND "RejectionReason" IS NOT NULL;""");
            migrationBuilder.Sql("""
                UPDATE "GamertagClaims" AS c SET "DecidedAt" = (
                    SELECT MIN(v."ModifiedAt") FROM (
                        SELECT h."GamertagClaimID", h."Status", h."ModifiedAt" FROM "GamertagClaimsHistory" h
                        UNION ALL SELECT r."GamertagClaimID", r."Status", r."ModifiedAt" FROM "GamertagClaims" r) v
                    WHERE v."GamertagClaimID" = c."GamertagClaimID" AND v."Status" <> 1)
                WHERE c."Status" <> 1;
                """);
            migrationBuilder.Sql("""
                UPDATE "GamertagClaims" AS c SET "DecidedByUserID" = (
                    SELECT MAX(v."ModifiedByID") FROM (
                        SELECT h."GamertagClaimID", h."Status", h."ModifiedAt", h."ModifiedByID" FROM "GamertagClaimsHistory" h
                        UNION ALL SELECT r."GamertagClaimID", r."Status", r."ModifiedAt", r."ModifiedByID" FROM "GamertagClaims" r) v
                    WHERE v."GamertagClaimID" = c."GamertagClaimID" AND v."Status" <> 1 AND v."ModifiedAt" = c."DecidedAt")
                WHERE c."Status" <> 1;
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

            migrationBuilder.Sql("""
                CREATE TRIGGER "GamertagClaims_History_Update" AFTER UPDATE ON "GamertagClaims" FOR EACH ROW BEGIN
                    INSERT INTO "GamertagClaimsHistory" ("ClaimantUserID", "CreatedAt", "CreatedByID", "CurrentGamertag", "DecidedAt", "DecidedByUserID", "FirstSeen", "Gamertag", "GamertagClaimID", "Kind", "LastSeen", "LookedUpAt", "ModifiedAt", "ModifiedByID", "NormalizedGamertag", "PeriodStart", "PlaceholderUserID", "RejectionReason", "SeasonID", "Status", "TeamID", "XboxUserID", "PeriodEnd") VALUES (OLD."ClaimantUserID", OLD."CreatedAt", OLD."CreatedByID", OLD."CurrentGamertag", OLD."DecidedAt", OLD."DecidedByUserID", OLD."FirstSeen", OLD."Gamertag", OLD."GamertagClaimID", OLD."Kind", OLD."LastSeen", OLD."LookedUpAt", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedGamertag", OLD."PeriodStart", OLD."PlaceholderUserID", OLD."RejectionReason", OLD."SeasonID", OLD."Status", OLD."TeamID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GamertagClaims" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GamertagClaims_History_Delete" AFTER DELETE ON "GamertagClaims" FOR EACH ROW BEGIN
                    INSERT INTO "GamertagClaimsHistory" ("ClaimantUserID", "CreatedAt", "CreatedByID", "CurrentGamertag", "DecidedAt", "DecidedByUserID", "FirstSeen", "Gamertag", "GamertagClaimID", "Kind", "LastSeen", "LookedUpAt", "ModifiedAt", "ModifiedByID", "NormalizedGamertag", "PeriodStart", "PlaceholderUserID", "RejectionReason", "SeasonID", "Status", "TeamID", "XboxUserID", "PeriodEnd") VALUES (OLD."ClaimantUserID", OLD."CreatedAt", OLD."CreatedByID", OLD."CurrentGamertag", OLD."DecidedAt", OLD."DecidedByUserID", OLD."FirstSeen", OLD."Gamertag", OLD."GamertagClaimID", OLD."Kind", OLD."LastSeen", OLD."LookedUpAt", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedGamertag", OLD."PeriodStart", OLD."PlaceholderUserID", OLD."RejectionReason", OLD."SeasonID", OLD."Status", OLD."TeamID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "GamertagClaims_History_Update";
                DROP TRIGGER IF EXISTS "GamertagClaims_History_Delete";
                """);

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
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "DecidedByUserID",
                schema: "Event",
                table: "GamertagClaimsHistory");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.DropColumn(
                name: "DecidedByUserID",
                schema: "Event",
                table: "GamertagClaims");

            migrationBuilder.Sql("""
                CREATE TRIGGER "GamertagClaims_History_Update" AFTER UPDATE ON "GamertagClaims" FOR EACH ROW BEGIN
                    INSERT INTO "GamertagClaimsHistory" ("ClaimantUserID", "CreatedAt", "CreatedByID", "CurrentGamertag", "FirstSeen", "Gamertag", "GamertagClaimID", "Kind", "LastSeen", "LookedUpAt", "ModifiedAt", "ModifiedByID", "NormalizedGamertag", "PeriodStart", "PlaceholderUserID", "RejectionReason", "SeasonID", "Status", "TeamID", "XboxUserID", "PeriodEnd") VALUES (OLD."ClaimantUserID", OLD."CreatedAt", OLD."CreatedByID", OLD."CurrentGamertag", OLD."FirstSeen", OLD."Gamertag", OLD."GamertagClaimID", OLD."Kind", OLD."LastSeen", OLD."LookedUpAt", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedGamertag", OLD."PeriodStart", OLD."PlaceholderUserID", OLD."RejectionReason", OLD."SeasonID", OLD."Status", OLD."TeamID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "GamertagClaims" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "GamertagClaims_History_Delete" AFTER DELETE ON "GamertagClaims" FOR EACH ROW BEGIN
                    INSERT INTO "GamertagClaimsHistory" ("ClaimantUserID", "CreatedAt", "CreatedByID", "CurrentGamertag", "FirstSeen", "Gamertag", "GamertagClaimID", "Kind", "LastSeen", "LookedUpAt", "ModifiedAt", "ModifiedByID", "NormalizedGamertag", "PeriodStart", "PlaceholderUserID", "RejectionReason", "SeasonID", "Status", "TeamID", "XboxUserID", "PeriodEnd") VALUES (OLD."ClaimantUserID", OLD."CreatedAt", OLD."CreatedByID", OLD."CurrentGamertag", OLD."FirstSeen", OLD."Gamertag", OLD."GamertagClaimID", OLD."Kind", OLD."LastSeen", OLD."LookedUpAt", OLD."ModifiedAt", OLD."ModifiedByID", OLD."NormalizedGamertag", OLD."PeriodStart", OLD."PlaceholderUserID", OLD."RejectionReason", OLD."SeasonID", OLD."Status", OLD."TeamID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
