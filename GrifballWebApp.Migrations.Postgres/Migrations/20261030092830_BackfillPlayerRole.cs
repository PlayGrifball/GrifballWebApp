using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPlayerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Accounts made by the Discord bot and the user management page got no role, so the signup form and setting a
            // gamertag were forbidden to them. Every real account without any role becomes a Player, as sign-up makes it.
            // Placeholder (dummy) accounts are left alone. Down leaves the roles: which were added here is not recorded.
            migrationBuilder.Sql("""
                INSERT INTO "Auth"."UserRoles" ("UserId", "RoleId", "CreatedAt", "ModifiedAt")
                SELECT u."Id", r."Id", now(), now()
                FROM "Auth"."Users" u
                CROSS JOIN "Auth"."Roles" r
                WHERE r."NormalizedName" = 'PLAYER'
                  AND NOT u."IsDummyUser"
                  AND NOT EXISTS (SELECT 1 FROM "Auth"."UserRoles" ur WHERE ur."UserId" = u."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
