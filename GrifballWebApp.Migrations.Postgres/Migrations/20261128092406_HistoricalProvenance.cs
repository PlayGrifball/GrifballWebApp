using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Entity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    EntityKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RunID = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HistoricalImportJobID = table.Column<int>(type: "integer", nullable: true),
                    HistoricalSourceSnapshotID = table.Column<int>(type: "integer", nullable: true),
                    SourceID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    Sheet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    Cells = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "case_insensitive"),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "case_insensitive")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalProvenance", x => x.HistoricalProvenanceID);
                    table.CheckConstraint("CK_Event_HistoricalProvenance_KnownKind", "\"Kind\" IN (1, 2, 3)");
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
