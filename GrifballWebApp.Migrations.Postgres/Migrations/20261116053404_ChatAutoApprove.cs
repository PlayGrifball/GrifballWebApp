using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ChatAutoApprove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoApprove",
                schema: "Auth",
                table: "ChatChoicesHistory",
                type: "boolean",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                schema: "Auth",
                table: "ChatChoices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldCollation: "case_insensitive");

            migrationBuilder.AddColumn<bool>(
                name: "AutoApprove",
                schema: "Auth",
                table: "ChatChoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"ChatChoices\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoApprove",
                schema: "Auth",
                table: "ChatChoicesHistory");

            migrationBuilder.DropColumn(
                name: "AutoApprove",
                schema: "Auth",
                table: "ChatChoices");

            migrationBuilder.Sql("CALL public.grif_refresh_versioning('\"Auth\".\"ChatChoices\"');");

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                schema: "Auth",
                table: "ChatChoices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                collation: "case_insensitive",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldCollation: "case_insensitive");
        }
    }
}
