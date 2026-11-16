using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                table: "DiscordHistory",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VoiceOptIn",
                schema: "User",
                table: "Discord",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"User\".\"Discord\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoiceOptIn",
                schema: "User",
                table: "DiscordHistory");

            migrationBuilder.DropColumn(
                name: "VoiceOptIn",
                schema: "User",
                table: "Discord");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"User\".\"Discord\"');");
        }
    }
}
