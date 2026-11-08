using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class DiscordUserAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A blank username, which the old public setter allowed, goes by its ID (as DiscordUser.FromDiscord names one) before
            // the constraint refuses it
            migrationBuilder.Sql("UPDATE [User].[Discord] SET [DiscordUsername] = CAST([DiscordUserID] AS nvarchar(20)) WHERE TRIM([DiscordUsername]) = '';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_Discord_UsernameNotBlank",
                schema: "User",
                table: "Discord",
                sql: "TRIM(DiscordUsername) <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_Discord_UsernameNotBlank",
                schema: "User",
                table: "Discord");
        }
    }
}
