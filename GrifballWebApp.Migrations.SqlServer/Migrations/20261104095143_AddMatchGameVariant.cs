using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchGameVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UgcGameVariantAssetID",
                schema: "Infinite",
                table: "Matches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "Matches",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UgcGameVariantAssetID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "Matches");
        }
    }
}
