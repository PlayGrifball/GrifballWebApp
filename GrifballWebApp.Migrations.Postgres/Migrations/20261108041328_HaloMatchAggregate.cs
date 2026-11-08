using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class HaloMatchAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the rows the constraints would refuse (the old model's public setters took any state): half a game variant
            // (an asset without its version, or a version without its asset) is no variant, which the backfill fetches again; a
            // layout the model doesn't know is undecided, which the decoder decides again; an outcome Halo doesn't give (the
            // pull maps only 1 to 4) is DidNotFinish, the one that claims nothing. Each row is otherwise as it was.
            migrationBuilder.Sql("UPDATE \"Infinite\".\"Matches\" SET \"UgcGameVariantAssetID\" = NULL, \"UgcGameVariantVersionID\" = NULL WHERE (\"UgcGameVariantAssetID\" IS NULL AND \"UgcGameVariantVersionID\" IS NOT NULL) OR (\"UgcGameVariantAssetID\" IS NOT NULL AND \"UgcGameVariantVersionID\" IS NULL);");
            migrationBuilder.Sql("UPDATE \"Infinite\".\"Matches\" SET \"ScoreLayout\" = NULL WHERE \"ScoreLayout\" NOT IN (1, 2, 3, 4, 5, 6);");
            migrationBuilder.Sql("UPDATE \"Infinite\".\"MatchTeams\" SET \"Outcome\" = 4 WHERE \"Outcome\" NOT IN (1, 2, 3, 4);");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Infinite_MatchTeams_KnownOutcome",
                schema: "Infinite",
                table: "MatchTeams",
                sql: "\"Outcome\" IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Infinite_Matches_GameVariantWhole",
                schema: "Infinite",
                table: "Matches",
                sql: "(\"UgcGameVariantAssetID\" IS NULL AND \"UgcGameVariantVersionID\" IS NULL) OR (\"UgcGameVariantAssetID\" IS NOT NULL AND \"UgcGameVariantVersionID\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Infinite_Matches_KnownScoreLayout",
                schema: "Infinite",
                table: "Matches",
                sql: "\"ScoreLayout\" IS NULL OR \"ScoreLayout\" IN (1, 2, 3, 4, 5, 6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled rows stay settled: they were in no state a match can have
            migrationBuilder.DropCheckConstraint(
                name: "CK_Infinite_MatchTeams_KnownOutcome",
                schema: "Infinite",
                table: "MatchTeams");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Infinite_Matches_GameVariantWhole",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Infinite_Matches_KnownScoreLayout",
                schema: "Infinite",
                table: "Matches");
        }
    }
}
