using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                table: "MatchesHistory",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "MatchesHistory",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UgcGameVariantAssetID",
                schema: "Infinite",
                table: "Matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "Matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Infinite\".\"Matches\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UgcGameVariantAssetID",
                schema: "Infinite",
                table: "MatchesHistory");

            migrationBuilder.DropColumn(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "MatchesHistory");

            migrationBuilder.DropColumn(
                name: "UgcGameVariantAssetID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "UgcGameVariantVersionID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Infinite\".\"Matches\"');");
        }
    }
}
