using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
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
                type: "int",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserSpartanRenders",
                schema: "Xbox",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RenderedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LoadoutKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
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
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ChoiceJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true)
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
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserSpartansHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

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

            migrationBuilder.CreateTable(
                name: "UserSpartanFiles",
                schema: "Xbox",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
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
                sql: "ProfilePicture IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_UserSpartanRenders_State_RequestedAt",
                schema: "Xbox",
                table: "UserSpartanRenders",
                columns: new[] { "State", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_XboxUserArmor_CheckedAt",
                schema: "Xbox",
                table: "XboxUserArmor",
                column: "CheckedAt");

            migrationBuilder.Sql("UPDATE [Auth].[Users] SET [ProfilePicture] = 2 WHERE [ProfilePicture] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSpartanFiles",
                schema: "Xbox");

            migrationBuilder.DropTable(
                name: "UserSpartans",
                schema: "Xbox")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "UserSpartansHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", "Xbox")
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

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
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_Users_KnownProfilePicture",
                schema: "Auth",
                table: "Users",
                sql: "ProfilePicture IN (0, 1)");
        }
    }
}
