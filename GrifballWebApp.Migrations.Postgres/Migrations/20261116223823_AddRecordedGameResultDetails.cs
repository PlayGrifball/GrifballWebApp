using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedGameResultDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Host",
                schema: "Recorded",
                table: "GamesHistory",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<bool>(
                name: "IsForfeit",
                schema: "Recorded",
                table: "GamesHistory",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vod",
                schema: "Recorded",
                table: "GamesHistory",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Host",
                schema: "Recorded",
                table: "Games",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<bool>(
                name: "IsForfeit",
                schema: "Recorded",
                table: "Games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Vod",
                schema: "Recorded",
                table: "Games",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Recorded\".\"Games\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Host",
                schema: "Recorded",
                table: "GamesHistory");

            migrationBuilder.DropColumn(
                name: "IsForfeit",
                schema: "Recorded",
                table: "GamesHistory");

            migrationBuilder.DropColumn(
                name: "Vod",
                schema: "Recorded",
                table: "GamesHistory");

            migrationBuilder.DropColumn(
                name: "Host",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "IsForfeit",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Vod",
                schema: "Recorded",
                table: "Games");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Recorded\".\"Games\"');");
        }
    }
}
