using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Discord");

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "Discord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    FromDiscordUserId = table.Column<long>(type: "bigint", nullable: false),
                    ToDiscordUserId = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Discord_FromDiscordUserId",
                        column: x => x.FromDiscordUserId,
                        principalSchema: "User",
                        principalTable: "Discord",
                        principalColumn: "DiscordUserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Discord_ToDiscordUserId",
                        column: x => x.ToDiscordUserId,
                        principalSchema: "User",
                        principalTable: "Discord",
                        principalColumn: "DiscordUserID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MessagesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Discord")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_FromDiscordUserId",
                schema: "Discord",
                table: "Messages",
                column: "FromDiscordUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ToDiscordUserId",
                schema: "Discord",
                table: "Messages",
                column: "ToDiscordUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages",
                schema: "Discord")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "MessagesHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Discord")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
