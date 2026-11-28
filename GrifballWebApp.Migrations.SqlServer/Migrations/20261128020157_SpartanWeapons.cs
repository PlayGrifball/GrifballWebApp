using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SpartanWeapons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WeaponCoatingsJson",
                schema: "Xbox",
                table: "XboxUserArmor",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Weapon",
                schema: "Xbox",
                table: "UserSpartanRenders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeaponCoatingsJson",
                schema: "Xbox",
                table: "XboxUserArmor");

            migrationBuilder.DropColumn(
                name: "Weapon",
                schema: "Xbox",
                table: "UserSpartanRenders");
        }
    }
}
