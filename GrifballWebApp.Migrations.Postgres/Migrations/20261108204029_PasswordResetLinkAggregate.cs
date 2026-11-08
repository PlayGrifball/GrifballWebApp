using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
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
                table: "PasswordResetLinksHistory",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UsedAt",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"PasswordResetLinks\"');");

            // Fill UsedAt before IsUsed goes, and settle the rows the constraint would refuse: a used link is dated by its last
            // change (when it was marked used), if that was before it expired; a used link that can't be dated so is deleted, as
            // the cleanup job would have (it was spent).
            migrationBuilder.Sql("UPDATE \"Auth\".\"PasswordResetLinks\" SET \"UsedAt\" = \"ModifiedAt\" WHERE \"IsUsed\" = TRUE AND \"ModifiedAt\" < \"ExpiresAt\";");
            migrationBuilder.Sql("DELETE FROM \"Auth\".\"PasswordResetLinks\" WHERE \"IsUsed\" = TRUE AND \"UsedAt\" IS NULL;");

            migrationBuilder.DropColumn(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinksHistory");

            migrationBuilder.DropColumn(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"PasswordResetLinks\"');");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_PasswordResetLinks_UsedBeforeExpiry",
                schema: "Auth",
                table: "PasswordResetLinks",
                sql: "\"UsedAt\" IS NULL OR \"UsedAt\" < \"ExpiresAt\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinksHistory",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"PasswordResetLinks\"');");

            // Fill IsUsed before UsedAt goes: a link with a use is used
            migrationBuilder.Sql("UPDATE \"Auth\".\"PasswordResetLinks\" SET \"IsUsed\" = TRUE WHERE \"UsedAt\" IS NOT NULL;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Auth_PasswordResetLinks_UsedBeforeExpiry",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.DropColumn(
                name: "UsedAt",
                schema: "Auth",
                table: "PasswordResetLinksHistory");

            migrationBuilder.DropColumn(
                name: "UsedAt",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"PasswordResetLinks\"');");
        }
    }
}
