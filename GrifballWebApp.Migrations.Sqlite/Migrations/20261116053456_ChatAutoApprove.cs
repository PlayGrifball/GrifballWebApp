using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ChatAutoApprove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "ChatChoices_History_Update";
                DROP TRIGGER IF EXISTS "ChatChoices_History_Delete";
                """);

            migrationBuilder.AddColumn<bool>(
                name: "AutoApprove",
                schema: "Auth",
                table: "ChatChoicesHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                schema: "Auth",
                table: "ChatChoices",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldCollation: "NOCASE");

            migrationBuilder.AddColumn<bool>(
                name: "AutoApprove",
                schema: "Auth",
                table: "ChatChoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                CREATE TRIGGER "ChatChoices_History_Update" AFTER UPDATE ON "ChatChoices" FOR EACH ROW BEGIN
                    INSERT INTO "ChatChoicesHistory" ("AutoApprove", "Model", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."AutoApprove", OLD."Model", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "ChatChoices" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "ChatChoices_History_Delete" AFTER DELETE ON "ChatChoices" FOR EACH ROW BEGIN
                    INSERT INTO "ChatChoicesHistory" ("AutoApprove", "Model", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."AutoApprove", OLD."Model", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "ChatChoices_History_Update";
                DROP TRIGGER IF EXISTS "ChatChoices_History_Delete";
                """);

            migrationBuilder.DropColumn(
                name: "AutoApprove",
                schema: "Auth",
                table: "ChatChoicesHistory");

            migrationBuilder.DropColumn(
                name: "AutoApprove",
                schema: "Auth",
                table: "ChatChoices");

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                schema: "Auth",
                table: "ChatChoices",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: true,
                oldCollation: "NOCASE");

            migrationBuilder.Sql("""
                CREATE TRIGGER "ChatChoices_History_Update" AFTER UPDATE ON "ChatChoices" FOR EACH ROW BEGIN
                    INSERT INTO "ChatChoicesHistory" ("Model", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."Model", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "ChatChoices" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "ChatChoices_History_Delete" AFTER DELETE ON "ChatChoices" FOR EACH ROW BEGIN
                    INSERT INTO "ChatChoicesHistory" ("Model", "PeriodStart", "UserID", "PeriodEnd") VALUES (OLD."Model", OLD."PeriodStart", OLD."UserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }
    }
}
