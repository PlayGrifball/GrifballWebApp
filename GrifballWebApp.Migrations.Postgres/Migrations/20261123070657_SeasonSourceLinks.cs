using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class SeasonSourceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceLinks",
                schema: "Event",
                table: "SeasonsHistory",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoricalSourceLinks",
                schema: "Event",
                table: "Seasons",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                defaultValueSql: "NULL");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"Seasons\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HistoricalSourceLinks",
                schema: "Event",
                table: "SeasonsHistory");

            migrationBuilder.DropColumn(
                name: "HistoricalSourceLinks",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Event\".\"Seasons\"');");
        }
    }
}
