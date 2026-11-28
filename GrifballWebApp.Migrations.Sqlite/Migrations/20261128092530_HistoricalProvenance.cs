using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricalProvenance",
                schema: "Event",
                columns: table => new
                {
                    HistoricalProvenanceID = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Entity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    EntityKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    RunID = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HistoricalImportJobID = table.Column<int>(type: "INTEGER", nullable: true),
                    HistoricalSourceSnapshotID = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceID = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Sheet = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    Cells = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    Value = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true, collation: "NOCASE"),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalProvenance", x => x.HistoricalProvenanceID);
                    table.CheckConstraint("CK_Event_HistoricalProvenance_KnownKind", "Kind IN (1, 2, 3)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalProvenance_Entity_EntityKey",
                schema: "Event",
                table: "HistoricalProvenance",
                columns: new[] { "Entity", "EntityKey" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalProvenance_SourceID_Sheet",
                schema: "Event",
                table: "HistoricalProvenance",
                columns: new[] { "SourceID", "Sheet" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalProvenance",
                schema: "Event");
        }
    }
}
