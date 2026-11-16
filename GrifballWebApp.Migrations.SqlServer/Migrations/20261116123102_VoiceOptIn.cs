using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class VoiceOptIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "VoiceOptIn",
                schema: "User",
                table: "Discord",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoiceOptIn",
                schema: "User",
                table: "Discord");
        }
    }
}
