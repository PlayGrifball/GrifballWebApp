using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
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
                    XboxUserID = table.Column<long>(type: "bigint", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CoreId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CorePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ThemePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CoatingPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HelmetPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    VisorPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    GlovePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HelmetAttachmentPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ChestAttachmentPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    KneePadPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LeftShoulderPadPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RightShoulderPadPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HipAttachmentPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WristAttachmentPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ArmorFxPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MythicFxPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ActionPosePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    StancePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BodyType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LeftArm = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RightArm = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LeftLeg = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RightLeg = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
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
