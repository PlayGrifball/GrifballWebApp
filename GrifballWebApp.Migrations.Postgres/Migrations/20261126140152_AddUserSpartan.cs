using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSpartan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSpartanRenders",
                schema: "Xbox",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RenderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LoadoutKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "case_insensitive")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpartanRenders", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSpartanRenders_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSpartans",
                schema: "Xbox",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ChoiceJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false, collation: "case_insensitive"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, computedColumnSql: "'infinity'::timestamp with time zone", stored: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "transaction_timestamp()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpartans", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSpartans_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "Auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSpartansHistory",
                schema: "Xbox",
                columns: table => new
                {
                    ChoiceJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true, collation: "case_insensitive"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserSpartanFiles",
                schema: "Xbox",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, collation: "case_insensitive"),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, collation: "case_insensitive"),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpartanFiles", x => new { x.UserId, x.Name });
                    table.ForeignKey(
                        name: "FK_UserSpartanFiles_UserSpartanRenders_UserId",
                        column: x => x.UserId,
                        principalSchema: "Xbox",
                        principalTable: "UserSpartanRenders",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSpartanRenders_State_RequestedAt",
                schema: "Xbox",
                table: "UserSpartanRenders",
                columns: new[] { "State", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSpartansHistory_UserId_PeriodEnd",
                schema: "Xbox",
                table: "UserSpartansHistory",
                columns: new[] { "UserId", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Xbox\".\"UserSpartans\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Xbox\".\"UserSpartans\"');");

            migrationBuilder.DropTable(
                name: "UserSpartanFiles",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "UserSpartans",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "UserSpartansHistory",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "UserSpartanRenders",
                schema: "Xbox");
        }
    }
}
