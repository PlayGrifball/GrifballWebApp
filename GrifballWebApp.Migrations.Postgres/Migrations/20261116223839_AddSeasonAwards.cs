using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasonAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeasonAwards",
                schema: "Event",
                columns: table => new
                {
                    SeasonAwardID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeasonID = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    WinnerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    Value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()"),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonAwards", x => x.SeasonAwardID);
                    table.CheckConstraint("CK_Event_SeasonAwards_PlayerOrTeam", "\"UserID\" IS NOT NULL OR \"TeamID\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_SeasonAwards_Seasons_SeasonID",
                        column: x => x.SeasonID,
                        principalSchema: "Event",
                        principalTable: "Seasons",
                        principalColumn: "SeasonID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeasonAwards_Teams_TeamID",
                        column: x => x.TeamID,
                        principalSchema: "Event",
                        principalTable: "Teams",
                        principalColumn: "TeamID");
                    table.ForeignKey(
                        name: "FK_SeasonAwards_Users_UserID",
                        column: x => x.UserID,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeasonAwardsHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: true),
                    SeasonAwardID = table.Column<int>(type: "integer", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    Value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    WinnerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwards_SeasonID_Name_WinnerName",
                schema: "Event",
                table: "SeasonAwards",
                columns: new[] { "SeasonID", "Name", "WinnerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwards_TeamID",
                schema: "Event",
                table: "SeasonAwards",
                column: "TeamID");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwards_UserID",
                schema: "Event",
                table: "SeasonAwards",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwardsHistory_SeasonAwardID_PeriodEnd",
                schema: "Event",
                table: "SeasonAwardsHistory",
                columns: new[] { "SeasonAwardID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwardsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "SeasonAwardsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwardsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "SeasonAwardsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAwardsHistory_UserID_PeriodEnd",
                schema: "Event",
                table: "SeasonAwardsHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"SeasonAwards\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"SeasonAwards\"');");

            migrationBuilder.DropTable(
                name: "SeasonAwards",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonAwardsHistory",
                schema: "Event");
        }
    }
}
