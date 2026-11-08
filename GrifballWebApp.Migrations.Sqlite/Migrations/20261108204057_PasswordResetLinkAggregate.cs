using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UsedAt",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "TEXT",
                nullable: true);

            // Fill UsedAt before IsUsed goes, and settle the rows the constraint would refuse: a used link is dated by its last
            // change (when it was marked used), if that was before it expired; a used link that can't be dated so is deleted, as
            // the cleanup job would have (it was spent).
            migrationBuilder.Sql("UPDATE \"PasswordResetLinks\" SET \"UsedAt\" = \"ModifiedAt\" WHERE \"IsUsed\" = 1 AND \"ModifiedAt\" < \"ExpiresAt\";");
            migrationBuilder.Sql("DELETE FROM \"PasswordResetLinks\" WHERE \"IsUsed\" = 1 AND \"UsedAt\" IS NULL;");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PasswordResetLinks_History_Update";
                DROP TRIGGER IF EXISTS "PasswordResetLinks_History_Delete";
                """);

            migrationBuilder.DropColumn(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinksHistory");

            migrationBuilder.DropColumn(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Auth_PasswordResetLinks_UsedBeforeExpiry",
                schema: "Auth",
                table: "PasswordResetLinks",
                sql: "UsedAt IS NULL OR UsedAt < ExpiresAt");

            migrationBuilder.Sql("""
                CREATE TRIGGER "PasswordResetLinks_History_Update" AFTER UPDATE ON "PasswordResetLinks" FOR EACH ROW BEGIN
                    INSERT INTO "PasswordResetLinksHistory" ("CreatedAt", "CreatedByID", "ExpiresAt", "Id", "ModifiedAt", "ModifiedByID", "PeriodStart", "Token", "UsedAt", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ExpiresAt", OLD."Id", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Token", OLD."UsedAt", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "PasswordResetLinks" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "PasswordResetLinks_History_Delete" AFTER DELETE ON "PasswordResetLinks" FOR EACH ROW BEGIN
                    INSERT INTO "PasswordResetLinksHistory" ("CreatedAt", "CreatedByID", "ExpiresAt", "Id", "ModifiedAt", "ModifiedByID", "PeriodStart", "Token", "UsedAt", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ExpiresAt", OLD."Id", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Token", OLD."UsedAt", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinksHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUsed",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Fill IsUsed before UsedAt goes: a link with a use is used
            migrationBuilder.Sql("UPDATE \"PasswordResetLinks\" SET \"IsUsed\" = 1 WHERE \"UsedAt\" IS NOT NULL;");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PasswordResetLinks_History_Update";
                DROP TRIGGER IF EXISTS "PasswordResetLinks_History_Delete";
                """);

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

            migrationBuilder.Sql("""
                CREATE TRIGGER "PasswordResetLinks_History_Update" AFTER UPDATE ON "PasswordResetLinks" FOR EACH ROW BEGIN
                    INSERT INTO "PasswordResetLinksHistory" ("CreatedAt", "CreatedByID", "ExpiresAt", "Id", "IsUsed", "ModifiedAt", "ModifiedByID", "PeriodStart", "Token", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ExpiresAt", OLD."Id", OLD."IsUsed", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Token", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "PasswordResetLinks" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "PasswordResetLinks_History_Delete" AFTER DELETE ON "PasswordResetLinks" FOR EACH ROW BEGIN
                    INSERT INTO "PasswordResetLinksHistory" ("CreatedAt", "CreatedByID", "ExpiresAt", "Id", "IsUsed", "ModifiedAt", "ModifiedByID", "PeriodStart", "Token", "UserId", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."ExpiresAt", OLD."Id", OLD."IsUsed", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."Token", OLD."UserId", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
