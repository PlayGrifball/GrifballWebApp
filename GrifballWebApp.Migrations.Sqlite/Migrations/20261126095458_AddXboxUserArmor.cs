using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddXboxUserArmor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "XboxUserArmor",
                schema: "Xbox",
                columns: table => new
                {
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CoreId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    CorePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    ThemePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    CoatingPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    HelmetPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    VisorPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    GlovePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    HelmetAttachmentPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    ChestAttachmentPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    KneePadPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    LeftShoulderPadPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    RightShoulderPadPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    HipAttachmentPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    WristAttachmentPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    ArmorFxPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    MythicFxPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    ActionPosePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    StancePath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    BodyType = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    LeftArm = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    RightArm = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    LeftLeg = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    RightLeg = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XboxUserArmor", x => x.XboxUserID);
                    table.ForeignKey(
                        name: "FK_XboxUserArmor_XboxUsers_XboxUserID",
                        column: x => x.XboxUserID,
                        principalSchema: "Xbox",
                        principalTable: "XboxUsers",
                        principalColumn: "XboxUserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XboxUserArmor_CheckedAt",
                schema: "Xbox",
                table: "XboxUserArmor",
                column: "CheckedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XboxUserArmor",
                schema: "Xbox");
        }
    }
}
