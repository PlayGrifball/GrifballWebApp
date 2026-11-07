using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedGameOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Origin",
                schema: "Recorded",
                table: "GamesHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                schema: "Recorded",
                table: "Games",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Recorded\".\"Games\"');");

            // Games entered by hand before Origin existed (manual match entry, test and preview environments): told by their key then
            migrationBuilder.Sql("UPDATE \"Recorded\".\"Games\" SET \"Origin\" = 2 WHERE \"SourceKey\" LIKE 'manual/%';");

            // Games entered by hand before Origin existed (manual match entry, test and preview environments): told by their key then
            migrationBuilder.Sql("UPDATE \"Recorded\".\"Games\" SET \"Origin\" = 2 WHERE \"SourceKey\" LIKE 'manual/%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "Recorded",
                table: "GamesHistory");

            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Recorded\".\"Games\"');");
        }
    }
}
