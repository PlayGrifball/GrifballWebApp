using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class VoiceConsents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoiceConsents",
                schema: "User",
                columns: table => new
                {
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: false),
                    GivenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceConsents", x => x.DiscordUserID);
                });

            migrationBuilder.CreateTable(
                name: "VoiceConsentsHistory",
                schema: "User",
                columns: table => new
                {
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: true),
                    GivenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceConsentsHistory_DiscordUserID_PeriodEnd",
                schema: "User",
                table: "VoiceConsentsHistory",
                columns: new[] { "DiscordUserID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"User\".\"VoiceConsents\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"User\".\"VoiceConsents\"');");

            migrationBuilder.DropTable(
                name: "VoiceConsents",
                schema: "User");

            migrationBuilder.DropTable(
                name: "VoiceConsentsHistory",
                schema: "User");
        }
    }
}
