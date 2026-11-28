using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Weapon",
                schema: "Xbox",
                table: "UserSpartanRenders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                collation: "case_insensitive");
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
