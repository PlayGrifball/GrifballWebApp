using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddStatFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatFlags",
                schema: "Event",
                columns: table => new
                {
                    StatFlagID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Rule = table.Column<int>(type: "integer", nullable: false),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedGameID = table.Column<int>(type: "integer", nullable: true),
                    PlayerUserID = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, collation: "case_insensitive"),
                    FoundAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DismissedByUserID = table.Column<int>(type: "integer", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatFlags", x => x.StatFlagID);
                    table.CheckConstraint("CK_Event_StatFlags_DismissedByNeedsDismissedAt", "\"DismissedByUserID\" IS NULL OR \"DismissedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_Event_StatFlags_OneGame", "(\"MatchID\" IS NOT NULL AND \"RecordedGameID\" IS NULL) OR (\"MatchID\" IS NULL AND \"RecordedGameID\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StatFlags_Games_RecordedGameID",
                        column: x => x.RecordedGameID,
                        principalSchema: "Recorded",
                        principalTable: "Games",
                        principalColumn: "RecordedGameID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StatFlags_Matches_MatchID",
                        column: x => x.MatchID,
                        principalSchema: "Infinite",
                        principalTable: "Matches",
                        principalColumn: "MatchID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatFlags_DismissedAt",
                schema: "Event",
                table: "StatFlags",
                column: "DismissedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StatFlags_MatchID",
                schema: "Event",
                table: "StatFlags",
                column: "MatchID");

            migrationBuilder.CreateIndex(
                name: "IX_StatFlags_RecordedGameID",
                schema: "Event",
                table: "StatFlags",
                column: "RecordedGameID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatFlags",
                schema: "Event");
        }
    }
}
