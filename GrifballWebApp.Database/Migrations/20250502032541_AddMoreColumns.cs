using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Discord_XboxUserID",
                schema: "User",
                table: "Discord");

            migrationBuilder.AlterColumn<string>(
                name: "Gamertag",
                schema: "Xbox",
                table: "XboxUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LossStreak",
                schema: "User",
                table: "Discord",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Losses",
                schema: "User",
                table: "Discord",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WinStreak",
                schema: "User",
                table: "Discord",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Wins",
                schema: "User",
                table: "Discord",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Discord_XboxUserID",
                schema: "User",
                table: "Discord",
                column: "XboxUserID",
                unique: true,
                filter: "[XboxUserID] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Discord_XboxUserID",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "LossStreak",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "Losses",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "WinStreak",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "Wins",
                schema: "User",
                table: "Discord");

            migrationBuilder.AlterColumn<string>(
                name: "Gamertag",
                schema: "Xbox",
                table: "XboxUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Discord_XboxUserID",
                schema: "User",
                table: "Discord",
                column: "XboxUserID");
        }
    }
}
