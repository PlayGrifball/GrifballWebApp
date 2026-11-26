using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CoreId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    CorePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    ThemePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    CoatingPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    HelmetPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    VisorPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    GlovePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    HelmetAttachmentPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    ChestAttachmentPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    KneePadPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    LeftShoulderPadPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    RightShoulderPadPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    HipAttachmentPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    WristAttachmentPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    ArmorFxPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    MythicFxPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    ActionPosePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    StancePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    BodyType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    LeftArm = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    RightArm = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    LeftLeg = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    RightLeg = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive")
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
