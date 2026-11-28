using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
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
                type: "TEXT",
                nullable: true,
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "Weapon",
                schema: "Xbox",
                table: "UserSpartanRenders",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                collation: "NOCASE");
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
