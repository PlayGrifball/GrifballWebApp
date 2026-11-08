using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleRecommendationAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settle the rows the constraints would refuse first (the job and the apply never wrote them; anything else could):
            // a fitness out of range is clamped to 0 to 1, negative generations are 0, and who applied one never applied is cleared
            migrationBuilder.Sql("UPDATE \"ScheduleRecommendations\" SET \"Fitness\" = 0 WHERE \"Fitness\" < 0;");
            migrationBuilder.Sql("UPDATE \"ScheduleRecommendations\" SET \"Fitness\" = 1 WHERE \"Fitness\" > 1;");
            migrationBuilder.Sql("UPDATE \"ScheduleRecommendations\" SET \"Generations\" = 0 WHERE \"Generations\" < 0;");
            migrationBuilder.Sql("UPDATE \"ScheduleRecommendations\" SET \"AppliedBy\" = NULL WHERE \"AppliedAt\" IS NULL AND \"AppliedBy\" IS NOT NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_ScheduleRecommendations_AppliedByOnlyWhenApplied",
                schema: "Event",
                table: "ScheduleRecommendations",
                sql: "AppliedAt IS NOT NULL OR AppliedBy IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_ScheduleRecommendations_FitnessInRange",
                schema: "Event",
                table: "ScheduleRecommendations",
                sql: "Fitness >= 0 AND Fitness <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Event_ScheduleRecommendations_GenerationsNotNegative",
                schema: "Event",
                table: "ScheduleRecommendations",
                sql: "Generations >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The settled rows stay settled: they were in no state a recommendation can have
            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_ScheduleRecommendations_AppliedByOnlyWhenApplied",
                schema: "Event",
                table: "ScheduleRecommendations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_ScheduleRecommendations_FitnessInRange",
                schema: "Event",
                table: "ScheduleRecommendations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Event_ScheduleRecommendations_GenerationsNotNegative",
                schema: "Event",
                table: "ScheduleRecommendations");
        }
    }
}
