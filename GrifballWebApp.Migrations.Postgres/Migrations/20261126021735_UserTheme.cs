using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class UserTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Theme",
                schema: "Auth",
                table: "UsersHistory",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Theme",
                schema: "Auth",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"Users\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Theme",
                schema: "Auth",
                table: "UsersHistory");

            migrationBuilder.DropColumn(
                name: "Theme",
                schema: "Auth",
                table: "Users");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"Users\"');");
        }
    }
}
