using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "Teams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "Teams",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "Teams",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Event",
                table: "Seasons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Event",
                table: "Seasons",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Event",
                table: "Seasons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "Infinite",
                table: "Matches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedByID",
                schema: "Infinite",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "Matches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "Matches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CreatedByID",
                schema: "Event",
                table: "Teams",
                column: "CreatedByID");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ModifiedByID",
                schema: "Event",
                table: "Teams",
                column: "ModifiedByID");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_CreatedByID",
                schema: "Event",
                table: "Seasons",
                column: "CreatedByID");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_ModifiedByID",
                schema: "Event",
                table: "Seasons",
                column: "ModifiedByID");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CreatedByID",
                schema: "Infinite",
                table: "Matches",
                column: "CreatedByID");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ModifiedByID",
                schema: "Infinite",
                table: "Matches",
                column: "ModifiedByID");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Users_CreatedByID",
                schema: "Infinite",
                table: "Matches",
                column: "CreatedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Users_ModifiedByID",
                schema: "Infinite",
                table: "Matches",
                column: "ModifiedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Users_CreatedByID",
                schema: "Event",
                table: "Seasons",
                column: "CreatedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Users_ModifiedByID",
                schema: "Event",
                table: "Seasons",
                column: "ModifiedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Users_CreatedByID",
                schema: "Event",
                table: "Teams",
                column: "CreatedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Users_ModifiedByID",
                schema: "Event",
                table: "Teams",
                column: "ModifiedByID",
                principalSchema: "Auth",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Users_CreatedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Users_ModifiedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Users_CreatedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Users_ModifiedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Users_CreatedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Users_ModifiedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_CreatedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_ModifiedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_CreatedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Seasons_ModifiedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_Matches_CreatedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropIndex(
                name: "IX_Matches_ModifiedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "CreatedByID",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ModifiedByID",
                schema: "Infinite",
                table: "Matches");
        }
    }
}
