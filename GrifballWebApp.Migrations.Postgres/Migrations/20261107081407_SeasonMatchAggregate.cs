using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                UPDATE "Event"."SeasonMatches" SET "HomeTeamResult" = NULL WHERE "HomeTeamResult" = 3 AND "HomeTeamID" IS NOT NULL;
                UPDATE "Event"."SeasonMatches" SET "AwayTeamResult" = NULL WHERE "AwayTeamResult" = 3 AND "AwayTeamID" IS NOT NULL;
                UPDATE "Event"."SeasonMatches" SET "HomeTeamResult" = NULL, "AwayTeamResult" = NULL
                    WHERE NOT ((COALESCE("HomeTeamResult", -1) = -1 AND COALESCE("AwayTeamResult", -1) = -1) OR (COALESCE("HomeTeamResult", -1) = 0 AND COALESCE("AwayTeamResult", -1) IN (1, 2, 3)) OR (COALESCE("HomeTeamResult", -1) = 1 AND COALESCE("AwayTeamResult", -1) = 0) OR (COALESCE("HomeTeamResult", -1) = 2 AND COALESCE("AwayTeamResult", -1) IN (0, 2)) OR (COALESCE("HomeTeamResult", -1) = 3 AND COALESCE("AwayTeamResult", -1) IN (-1, 0, 3)) OR (COALESCE("HomeTeamResult", -1) = -1 AND COALESCE("AwayTeamResult", -1) = 3));
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonMatches_ByeHasNoTeam",
                schema: "Event",
                table: "SeasonMatches",
                sql: "(\"HomeTeamResult\" IS NULL OR \"HomeTeamResult\" <> 3 OR \"HomeTeamID\" IS NULL) AND (\"AwayTeamResult\" IS NULL OR \"AwayTeamResult\" <> 3 OR \"AwayTeamID\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_SeasonMatches_LegalResults",
                schema: "Event",
                table: "SeasonMatches",
                sql: "(COALESCE(\"HomeTeamResult\", -1) = -1 AND COALESCE(\"AwayTeamResult\", -1) = -1) OR (COALESCE(\"HomeTeamResult\", -1) = 0 AND COALESCE(\"AwayTeamResult\", -1) IN (1, 2, 3)) OR (COALESCE(\"HomeTeamResult\", -1) = 1 AND COALESCE(\"AwayTeamResult\", -1) = 0) OR (COALESCE(\"HomeTeamResult\", -1) = 2 AND COALESCE(\"AwayTeamResult\", -1) IN (0, 2)) OR (COALESCE(\"HomeTeamResult\", -1) = 3 AND COALESCE(\"AwayTeamResult\", -1) IN (-1, 0, 3)) OR (COALESCE(\"HomeTeamResult\", -1) = -1 AND COALESCE(\"AwayTeamResult\", -1) = 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonMatches_ByeHasNoTeam",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_SeasonMatches_LegalResults",
                schema: "Event",
                table: "SeasonMatches");
        }
    }
}
