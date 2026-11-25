using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ProfilePictureChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProfilePicture",
                schema: "Auth",
                table: "UsersHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfilePicture",
                schema: "Auth",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"Users\"');");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users",
                sql: "\"ProfilePicture\" IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfilePicture",
                schema: "Auth",
                table: "UsersHistory");

            migrationBuilder.DropColumn(
                name: "ProfilePicture",
                schema: "Auth",
                table: "Users");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"Users\"');");
        }
    }
}
