using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddXboxUserArmorAndSpartans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "ProfilePicture",
                schema: "Auth",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

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

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users",
                sql: "\"ProfilePicture\" IN (0, 1, 2)");

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

            migrationBuilder.CreateIndex(
                name: "IX_XboxUserArmor_CheckedAt",
                schema: "Xbox",
                table: "XboxUserArmor",
                column: "CheckedAt");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Xbox\".\"UserSpartans\"');");

            migrationBuilder.Sql("UPDATE \"Auth\".\"Users\" SET \"ProfilePicture\" = 2 WHERE \"ProfilePicture\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Auth\".\"Users\" SET \"ProfilePicture\" = 0 WHERE \"ProfilePicture\" = 2;");

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
                name: "XboxUserArmor",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "UserSpartanRenders",
                schema: "Xbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "ProfilePicture",
                schema: "Auth",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users",
                sql: "\"ProfilePicture\" IN (0, 1)");
        }
    }
}
