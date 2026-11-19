using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalLeaguesGglEugbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "Event",
                table: "Leagues",
                columns: new[] { "LeagueID", "CreatedAt", "CreatedByID", "LeagueName", "ModifiedAt", "ModifiedByID", "Slug" },
                values: new object[,]
                {
                    { 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "GGL", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "ggl" },
                    { 7, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "EUGBL", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "eugbl" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Event",
                table: "Leagues",
                keyColumn: "LeagueID",
                keyValue: 6);

            migrationBuilder.DeleteData(
                schema: "Event",
                table: "Leagues",
                keyColumn: "LeagueID",
                keyValue: 7);
        }
    }
}
