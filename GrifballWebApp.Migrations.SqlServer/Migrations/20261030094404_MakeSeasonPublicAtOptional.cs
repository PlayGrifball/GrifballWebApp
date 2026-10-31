using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class MakeSeasonPublicAtOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "PublicAt",
                schema: "Event",
                table: "Seasons",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            // Null is now "not public yet", what a new season is. A season saved before this never had PublicAt set, so it
            // is the beginning of time, and those seasons have been announced: they stay public, from when they were made
            // where that is known, so the editor shows a real date.
            migrationBuilder.Sql("""
                UPDATE [Event].[Seasons] SET [PublicAt] = [CreatedAt]
                WHERE [PublicAt] < '1900-01-01' AND [CreatedAt] >= '1900-01-01';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seasons that are not public stay so, at the end of time. Their old versions in the history table can be null
            // too, and SQL Server makes the history column NOT NULL with the table's: it can only be written to with
            // versioning off. The current rows go first, as updating them adds their null versions to the history.
            migrationBuilder.Sql("""
                UPDATE [Event].[Seasons] SET [PublicAt] = '9999-12-31T23:59:59' WHERE [PublicAt] IS NULL;
                """);
            // Each its own batch: SQL Server checks a batch that writes to the history table before versioning is off.
            migrationBuilder.Sql("""
                ALTER TABLE [Event].[Seasons] SET (SYSTEM_VERSIONING = OFF);
                """);
            migrationBuilder.Sql("""
                UPDATE [Event].[SeasonsHistory] SET [PublicAt] = '9999-12-31T23:59:59' WHERE [PublicAt] IS NULL;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE [Event].[Seasons] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [Event].[SeasonsHistory]));
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PublicAt",
                schema: "Event",
                table: "Seasons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
