using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class conc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Auth",
                table: "Roles",
                keyColumn: "Id",
                keyValue: "13910e05-fe11-43fa-9299-7e8fec8732fd");

            migrationBuilder.DeleteData(
                schema: "Auth",
                table: "Roles",
                keyColumn: "Id",
                keyValue: "2e7bc025-56d4-4d5c-a1f5-45621f140f90");

            migrationBuilder.DeleteData(
                schema: "Auth",
                table: "Roles",
                keyColumn: "Id",
                keyValue: "6df39de0-8843-46cc-b618-ee237cc434b0");

            migrationBuilder.InsertData(
                schema: "Auth",
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "a4e02348-2c02-4965-956a-441a86ff5328", "37ff5fdf-5cb8-403c-bb9e-81db05a5fcdd", "Commissioner", "COMMISSIONER" },
                    { "cbe1b7e5-b6c8-4dec-aa81-10ee49a6ef4c", "0be992c7-f824-40c7-8975-964068c7fbfe", "Player", "PLAYER" },
                    { "fd0678fb-6005-4cac-8609-a5cee67889f5", "cda78946-53d4-4154-b723-2b33b95341cc", "Sysadmin", "SYSADMIN" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "Auth",
                table: "Roles",
                keyColumn: "Id",
                keyValue: "a4e02348-2c02-4965-956a-441a86ff5328");

            migrationBuilder.DeleteData(
                schema: "Auth",
                table: "Roles",
                keyColumn: "Id",
                keyValue: "cbe1b7e5-b6c8-4dec-aa81-10ee49a6ef4c");

            migrationBuilder.DeleteData(
                schema: "Auth",
                table: "Roles",
                keyColumn: "Id",
                keyValue: "fd0678fb-6005-4cac-8609-a5cee67889f5");

            migrationBuilder.InsertData(
                schema: "Auth",
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "13910e05-fe11-43fa-9299-7e8fec8732fd", null, "Commissioner", null },
                    { "2e7bc025-56d4-4d5c-a1f5-45621f140f90", null, "Player", null },
                    { "6df39de0-8843-46cc-b618-ee237cc434b0", null, "Sysadmin", null }
                });
        }
    }
}
