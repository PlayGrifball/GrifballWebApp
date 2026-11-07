using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
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
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Games entered by hand before Origin existed (manual match entry, test and preview environments): told by their key then
            migrationBuilder.Sql("UPDATE [Recorded].[Games] SET [Origin] = 2 WHERE [SourceKey] LIKE N'manual/%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "Recorded",
                table: "Games");
        }
    }
}
