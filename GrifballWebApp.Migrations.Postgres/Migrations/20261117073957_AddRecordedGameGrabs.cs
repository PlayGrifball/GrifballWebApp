using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordedGameGrabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Grabs",
                schema: "Recorded",
                table: "GameParticipantsHistory",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Grabs",
                schema: "Recorded",
                table: "GameParticipants",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Recorded\".\"GameParticipants\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Grabs",
                schema: "Recorded",
                table: "GameParticipantsHistory");

            migrationBuilder.DropColumn(
                name: "Grabs",
                schema: "Recorded",
                table: "GameParticipants");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Recorded\".\"GameParticipants\"');");
        }
    }
}
