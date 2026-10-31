using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            // Null is now "not public yet", what a new season is. A season saved before this never had PublicAt set, so it
            // is the beginning of time, and those seasons have been announced: they stay public, from when they were made
            // where that is known, so the editor shows a real date.
            migrationBuilder.Sql("""
                UPDATE "Event"."Seasons" SET "PublicAt" = "CreatedAt"
                WHERE "PublicAt" < '1900-01-01' AND "CreatedAt" >= '1900-01-01';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seasons that are not public stay so, at the end of time. The history table's columns all take null here, so its
            // old versions can stay as they are.
            migrationBuilder.Sql("""
                UPDATE "Event"."Seasons" SET "PublicAt" = 'infinity' WHERE "PublicAt" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PublicAt",
                schema: "Event",
                table: "Seasons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
