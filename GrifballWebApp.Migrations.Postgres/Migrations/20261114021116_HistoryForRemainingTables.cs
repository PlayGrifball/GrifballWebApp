using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class HistoryForRemainingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XuidLookups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XuidLookups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserMerges",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserMerges",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendationMatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendationMatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSources",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSources",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalImportSettings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalImportSettings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "DuplicateDismissals",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "DuplicateDismissals",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "'-infinity'::timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Xbox",
                table: "XuidLookups",
                type: "timestamp with time zone",
                nullable: false,
                computedColumnSql: "'infinity'::timestamp with time zone",
                stored: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserMerges",
                type: "timestamp with time zone",
                nullable: false,
                computedColumnSql: "'infinity'::timestamp with time zone",
                stored: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "ScheduleRecommendations",
                type: "timestamp with time zone",
                nullable: false,
                computedColumnSql: "'infinity'::timestamp with time zone",
                stored: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "ScheduleRecommendationMatches",
                type: "timestamp with time zone",
                nullable: false,
                computedColumnSql: "'infinity'::timestamp with time zone",
                stored: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "HistoricalSources",
                type: "timestamp with time zone",
                nullable: false,
                computedColumnSql: "'infinity'::timestamp with time zone",
                stored: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                type: "timestamp with time zone",
                nullable: false,
                computedColumnSql: "'infinity'::timestamp with time zone",
                stored: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "HistoricalImportSettings",
                type: "timestamp with time zone",
                nullable: false,
                computedColumnSql: "'infinity'::timestamp with time zone",
                stored: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "DuplicateDismissals",
                type: "timestamp with time zone",
                nullable: false,
                computedColumnSql: "'infinity'::timestamp with time zone",
                stored: true);

            migrationBuilder.CreateTable(
                name: "DuplicateDismissalsHistory",
                schema: "Auth",
                columns: table => new
                {
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DismissedByUserID = table.Column<int>(type: "integer", nullable: true),
                    HighUserID = table.Column<int>(type: "integer", nullable: true),
                    LowUserID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HistoricalImportSettingsHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    HistoricalImportSettingsID = table.Column<int>(type: "integer", nullable: true),
                    LastScheduledJobID = table.Column<int>(type: "integer", nullable: true),
                    LastScheduledRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastScheduledRunNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SchedulePaused = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HistoricalSeasonReviewsHistory",
                schema: "Event",
                columns: table => new
                {
                    ApprovalJobID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HistoricalImportJobID = table.Column<int>(type: "integer", nullable: true),
                    HistoricalSeasonReviewID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeasonKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    SeasonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    StatsBefore = table.Column<int>(type: "integer", nullable: true),
                    StatsRemoved = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    TeamsBefore = table.Column<int>(type: "integer", nullable: true),
                    TeamsRemoved = table.Column<int>(type: "integer", nullable: true),
                    TitlesBefore = table.Column<int>(type: "integer", nullable: true),
                    TitlesRemoved = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HistoricalSourcesHistory",
                schema: "Event",
                columns: table => new
                {
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    Config = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    CsvSheetName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    Enabled = table.Column<bool>(type: "boolean", nullable: true),
                    Format = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "case_insensitive"),
                    GameVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "case_insensitive"),
                    Gid = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "case_insensitive"),
                    HistoricalSourceID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    IsDead = table.Column<bool>(type: "boolean", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: true),
                    LastChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFetchStatus = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    LastFetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    League = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "case_insensitive"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SheetKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, collation: "case_insensitive"),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, collation: "case_insensitive"),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, collation: "case_insensitive"),
                    WaybackTimestamp = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true, collation: "case_insensitive"),
                    Year = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRecommendationMatchesHistory",
                schema: "Event",
                columns: table => new
                {
                    AwayTeamAvailable = table.Column<bool>(type: "boolean", nullable: true),
                    HomeTeamAvailable = table.Column<bool>(type: "boolean", nullable: true),
                    Overlaps = table.Column<bool>(type: "boolean", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduleRecommendationID = table.Column<int>(type: "integer", nullable: true),
                    ScheduleRecommendationMatchID = table.Column<int>(type: "integer", nullable: true),
                    ScheduledTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRecommendationsHistory",
                schema: "Event",
                columns: table => new
                {
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "case_insensitive"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Fitness = table.Column<double>(type: "double precision", nullable: true),
                    Generations = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "case_insensitive"),
                    ScheduleRecommendationID = table.Column<int>(type: "integer", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserMergesHistory",
                schema: "Auth",
                columns: table => new
                {
                    Manifest = table.Column<string>(type: "text", nullable: true, collation: "case_insensitive"),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    RowsAdded = table.Column<int>(type: "integer", nullable: true),
                    RowsMoved = table.Column<int>(type: "integer", nullable: true),
                    RowsRemoved = table.Column<int>(type: "integer", nullable: true),
                    SourceUserID = table.Column<int>(type: "integer", nullable: true),
                    SourceUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "case_insensitive"),
                    TargetUserID = table.Column<int>(type: "integer", nullable: true),
                    TargetUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "case_insensitive"),
                    Trigger = table.Column<int>(type: "integer", nullable: true),
                    TriggeredByUserID = table.Column<int>(type: "integer", nullable: true),
                    UndoNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, collation: "case_insensitive"),
                    UndoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UndoneByUserID = table.Column<int>(type: "integer", nullable: true),
                    UserMergeID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "XuidLookupsHistory",
                schema: "Xbox",
                columns: table => new
                {
                    Error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true, collation: "case_insensitive"),
                    LookedUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateDismissalsHistory_LowUserID_HighUserID_PeriodEnd",
                schema: "Auth",
                table: "DuplicateDismissalsHistory",
                columns: new[] { "LowUserID", "HighUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalImportSettingsHistory_HistoricalImportSettingsID_~",
                schema: "Event",
                table: "HistoricalImportSettingsHistory",
                columns: new[] { "HistoricalImportSettingsID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSeasonReviewsHistory_HistoricalImportJobID_Period~",
                schema: "Event",
                table: "HistoricalSeasonReviewsHistory",
                columns: new[] { "HistoricalImportJobID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSeasonReviewsHistory_HistoricalSeasonReviewID_Per~",
                schema: "Event",
                table: "HistoricalSeasonReviewsHistory",
                columns: new[] { "HistoricalSeasonReviewID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSourcesHistory_HistoricalSourceID_PeriodEnd",
                schema: "Event",
                table: "HistoricalSourcesHistory",
                columns: new[] { "HistoricalSourceID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRecommendationMatchesHistory_ScheduleRecomm~903120f7",
                schema: "Event",
                table: "ScheduleRecommendationMatchesHistory",
                columns: new[] { "ScheduleRecommendationMatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRecommendationMatchesHistory_ScheduleRecomm~aa6ada85",
                schema: "Event",
                table: "ScheduleRecommendationMatchesHistory",
                columns: new[] { "ScheduleRecommendationID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRecommendationsHistory_ScheduleRecommendationID_Per~",
                schema: "Event",
                table: "ScheduleRecommendationsHistory",
                columns: new[] { "ScheduleRecommendationID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRecommendationsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "ScheduleRecommendationsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMergesHistory_UserMergeID_PeriodEnd",
                schema: "Auth",
                table: "UserMergesHistory",
                columns: new[] { "UserMergeID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_XuidLookupsHistory_UserID_PeriodEnd",
                schema: "Xbox",
                table: "XuidLookupsHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"DuplicateDismissals\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"HistoricalImportSettings\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"HistoricalSeasonReviews\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"HistoricalSources\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"ScheduleRecommendationMatches\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"ScheduleRecommendations\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"UserMerges\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Xbox\".\"XuidLookups\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Xbox\".\"XuidLookups\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"UserMerges\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"ScheduleRecommendations\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"ScheduleRecommendationMatches\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"HistoricalSources\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"HistoricalSeasonReviews\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"HistoricalImportSettings\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"DuplicateDismissals\"');");

            migrationBuilder.DropTable(
                name: "DuplicateDismissalsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "HistoricalImportSettingsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "HistoricalSeasonReviewsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "HistoricalSourcesHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "ScheduleRecommendationMatchesHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "ScheduleRecommendationsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "UserMergesHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "XuidLookupsHistory",
                schema: "Xbox");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Xbox",
                table: "XuidLookups");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserMerges");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "ScheduleRecommendations");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "ScheduleRecommendationMatches");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "HistoricalSources");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "HistoricalSeasonReviews");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Event",
                table: "HistoricalImportSettings");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                schema: "Auth",
                table: "DuplicateDismissals");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XuidLookups");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserMerges");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendations");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendationMatches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSources");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSeasonReviews");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalImportSettings");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "DuplicateDismissals");
        }
    }
}
