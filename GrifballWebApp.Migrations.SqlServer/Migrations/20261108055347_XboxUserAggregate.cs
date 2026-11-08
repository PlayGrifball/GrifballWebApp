using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class XboxUserAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the past gamertags the constraints would refuse (the old model set any dates and source): one first seen
            // after it was last seen starts as it ends (LastSeen, the bound matching reads, stays as it was), and one of a source
            // the model does not know was known by hand. Each row is otherwise as it was, and its history keeps the old values.
            migrationBuilder.Sql("UPDATE [Xbox].[PastGamertags] SET [FirstSeen] = [LastSeen] WHERE [FirstSeen] > [LastSeen];");
            migrationBuilder.Sql("UPDATE [Xbox].[PastGamertags] SET [Source] = 4 WHERE [Source] NOT IN (1, 2, 3, 4);");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Xbox_PastGamertags_KnownSource",
                schema: "Xbox",
                table: "PastGamertags",
                sql: "Source IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Xbox_PastGamertags_SeenInOrder",
                schema: "Xbox",
                table: "PastGamertags",
                sql: "FirstSeen IS NULL OR LastSeen IS NULL OR FirstSeen <= LastSeen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled rows stay settled: they held dates and sources no past gamertag can have
            migrationBuilder.DropCheckConstraint(
                name: "CK_Xbox_PastGamertags_KnownSource",
                schema: "Xbox",
                table: "PastGamertags");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Xbox_PastGamertags_SeenInOrder",
                schema: "Xbox",
                table: "PastGamertags");
        }
    }
}
