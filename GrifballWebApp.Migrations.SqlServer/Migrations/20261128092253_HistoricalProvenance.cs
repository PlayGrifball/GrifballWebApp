using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
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
                    HistoricalProvenanceID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Entity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    RunID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HistoricalImportJobID = table.Column<int>(type: "int", nullable: true),
                    HistoricalSourceSnapshotID = table.Column<int>(type: "int", nullable: true),
                    SourceID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sheet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cells = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
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
