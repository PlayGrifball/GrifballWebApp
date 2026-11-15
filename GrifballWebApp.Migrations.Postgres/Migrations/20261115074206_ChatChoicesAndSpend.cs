using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ChatChoicesAndSpend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatChoices",
                schema: "Auth",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatChoices", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "ChatChoicesHistory",
                schema: "Auth",
                columns: table => new
                {
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ChatMonths",
                schema: "Auth",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    CostMicros = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    Replies = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMonths", x => new { x.UserID, x.Month });
                    table.CheckConstraint("CK_Auth_ChatMonths_NotNegative", "\"CostMicros\" >= 0 AND \"InputTokens\" >= 0 AND \"OutputTokens\" >= 0 AND \"Replies\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatChoicesHistory_UserID_PeriodEnd",
                schema: "Auth",
                table: "ChatChoicesHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"ChatChoices\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"ChatChoices\"');");

            migrationBuilder.DropTable(
                name: "ChatChoices",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "ChatChoicesHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "ChatMonths",
                schema: "Auth");
        }
    }
}
