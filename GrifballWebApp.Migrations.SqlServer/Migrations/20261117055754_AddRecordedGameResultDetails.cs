using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedGameResultDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Host",
                schema: "Recorded",
                table: "Games",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForfeit",
                schema: "Recorded",
                table: "Games",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Vod",
                schema: "Recorded",
                table: "Games",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Host",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsForfeit",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Vod",
                schema: "Recorded",
                table: "Games");
        }
    }
}
