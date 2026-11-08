using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class PasswordResetLinkAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UsedAt",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "datetime2",
                nullable: true);

            // Fill UsedAt before IsUsed goes, and settle the rows the constraint would refuse: a used link is dated by its last
            // change (when it was marked used), if that was before it expired; a used link that can't be dated so is deleted, as
            // the cleanup job would have (it was spent).
            migrationBuilder.Sql("UPDATE [Auth].[PasswordResetLinks] SET [UsedAt] = [ModifiedAt] WHERE [IsUsed] = 1 AND [ModifiedAt] < [ExpiresAt];");
            migrationBuilder.Sql("DELETE FROM [Auth].[PasswordResetLinks] WHERE [IsUsed] = 1 AND [UsedAt] IS NULL;");

            migrationBuilder.DropColumn(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_PasswordResetLinks_UsedBeforeExpiry",
                schema: "Auth",
                table: "PasswordResetLinks",
                sql: "UsedAt IS NULL OR UsedAt < ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Fill IsUsed before UsedAt goes: a link with a use is used
            migrationBuilder.Sql("UPDATE [Auth].[PasswordResetLinks] SET [IsUsed] = 1 WHERE [UsedAt] IS NOT NULL;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_PasswordResetLinks_UsedBeforeExpiry",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.DropColumn(
                name: "UsedAt",
                schema: "Auth",
                table: "PasswordResetLinks");
        }
    }
}
