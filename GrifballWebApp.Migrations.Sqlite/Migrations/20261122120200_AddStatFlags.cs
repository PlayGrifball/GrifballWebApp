using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
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
                    StatFlagID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Rule = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchID = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecordedGameID = table.Column<int>(type: "INTEGER", nullable: true),
                    PlayerUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    Discriminator = table.Column<long>(type: "INTEGER", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, collation: "NOCASE"),
                    FoundAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DismissedByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatFlags", x => x.StatFlagID);
                    table.CheckConstraint("CK_Event_StatFlags_DismissedByNeedsDismissedAt", "DismissedByUserID IS NULL OR DismissedAt IS NOT NULL");
                    table.CheckConstraint("CK_Event_StatFlags_OneGame", "(MatchID IS NOT NULL AND RecordedGameID IS NULL) OR (MatchID IS NULL AND RecordedGameID IS NOT NULL)");
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
