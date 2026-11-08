using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class UserMergeAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the rows the constraints would refuse (the old model's public setters took any values; the app never
            // made these): a record of a user merged into itself records no merge (the merge always refused one), and is
            // removed; a standing merge keeps no undoer or undo note (it stands: UndoneAt says so); a count below zero is 0.
            migrationBuilder.Sql("DELETE FROM \"UserMerges\" WHERE \"SourceUserID\" = \"TargetUserID\";");
            migrationBuilder.Sql("UPDATE \"UserMerges\" SET \"UndoneByUserID\" = NULL, \"UndoNote\" = NULL WHERE \"UndoneAt\" IS NULL AND (\"UndoneByUserID\" IS NOT NULL OR \"UndoNote\" IS NOT NULL);");
            migrationBuilder.Sql("UPDATE \"UserMerges\" SET \"RowsMoved\" = 0 WHERE \"RowsMoved\" < 0;");
            migrationBuilder.Sql("UPDATE \"UserMerges\" SET \"RowsRemoved\" = 0 WHERE \"RowsRemoved\" < 0;");
            migrationBuilder.Sql("UPDATE \"UserMerges\" SET \"RowsAdded\" = 0 WHERE \"RowsAdded\" < 0;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_UserMerges_DifferentUsers",
                schema: "Auth",
                table: "UserMerges",
                sql: "\"SourceUserID\" <> \"TargetUserID\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_UserMerges_RowCountsNotNegative",
                schema: "Auth",
                table: "UserMerges",
                sql: "\"RowsMoved\" >= 0 AND \"RowsRemoved\" >= 0 AND \"RowsAdded\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_UserMerges_UndoOnlyWhenUndone",
                schema: "Auth",
                table: "UserMerges",
                sql: "\"UndoneAt\" IS NOT NULL OR (\"UndoneByUserID\" IS NULL AND \"UndoNote\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled rows stay settled: they were in no state a merge record can have
            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_UserMerges_DifferentUsers",
                schema: "Auth",
                table: "UserMerges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_UserMerges_RowCountsNotNegative",
                schema: "Auth",
                table: "UserMerges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_UserMerges_UndoOnlyWhenUndone",
                schema: "Auth",
                table: "UserMerges");
        }
    }
}
