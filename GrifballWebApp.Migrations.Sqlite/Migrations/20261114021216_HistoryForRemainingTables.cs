using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Sqlite.Migrations
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
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XuidLookups",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserMerges",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserMerges",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendations",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendations",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendationMatches",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "ScheduleRecommendationMatches",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSources",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSources",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalImportSettings",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "HistoricalImportSettings",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "DuplicateDismissals",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "DuplicateDismissals",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "'0001-01-01 00:00:00'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Xbox",
                table: "XuidLookups",
                type: "TEXT",
                nullable: false,
                computedColumnSql: "'9999-12-31 23:59:59.9999999'",
                stored: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "UserMerges",
                type: "TEXT",
                nullable: false,
                computedColumnSql: "'9999-12-31 23:59:59.9999999'",
                stored: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "ScheduleRecommendations",
                type: "TEXT",
                nullable: false,
                computedColumnSql: "'9999-12-31 23:59:59.9999999'",
                stored: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "ScheduleRecommendationMatches",
                type: "TEXT",
                nullable: false,
                computedColumnSql: "'9999-12-31 23:59:59.9999999'",
                stored: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "HistoricalSources",
                type: "TEXT",
                nullable: false,
                computedColumnSql: "'9999-12-31 23:59:59.9999999'",
                stored: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "HistoricalSeasonReviews",
                type: "TEXT",
                nullable: false,
                computedColumnSql: "'9999-12-31 23:59:59.9999999'",
                stored: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Event",
                table: "HistoricalImportSettings",
                type: "TEXT",
                nullable: false,
                computedColumnSql: "'9999-12-31 23:59:59.9999999'",
                stored: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEnd",
                schema: "Auth",
                table: "DuplicateDismissals",
                type: "TEXT",
                nullable: false,
                computedColumnSql: "'9999-12-31 23:59:59.9999999'",
                stored: false);

            migrationBuilder.CreateTable(
                name: "DuplicateDismissalsHistory",
                schema: "Auth",
                columns: table => new
                {
                    DismissedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DismissedByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    HighUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    LowUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HistoricalImportSettingsHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    HistoricalImportSettingsID = table.Column<int>(type: "INTEGER", nullable: true),
                    LastScheduledJobID = table.Column<int>(type: "INTEGER", nullable: true),
                    LastScheduledRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastScheduledRunNote = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SchedulePaused = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HistoricalSeasonReviewsHistory",
                schema: "Event",
                columns: table => new
                {
                    ApprovalJobID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HistoricalImportJobID = table.Column<int>(type: "INTEGER", nullable: true),
                    HistoricalSeasonReviewID = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    SeasonName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    StatsBefore = table.Column<int>(type: "INTEGER", nullable: true),
                    StatsRemoved = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamsBefore = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamsRemoved = table.Column<int>(type: "INTEGER", nullable: true),
                    TitlesBefore = table.Column<int>(type: "INTEGER", nullable: true),
                    TitlesRemoved = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HistoricalSourcesHistory",
                schema: "Event",
                columns: table => new
                {
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    Config = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    CsvSheetName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    Format = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    GameVersion = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    Gid = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    HistoricalSourceID = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    IsDead = table.Column<bool>(type: "INTEGER", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: true),
                    LastChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastFetchStatus = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    LastFetchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    League = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SheetKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true, collation: "NOCASE"),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true, collation: "NOCASE"),
                    WaybackTimestamp = table.Column<string>(type: "TEXT", maxLength: 14, nullable: true, collation: "NOCASE"),
                    Year = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRecommendationMatchesHistory",
                schema: "Event",
                columns: table => new
                {
                    AwayTeamAvailable = table.Column<bool>(type: "INTEGER", nullable: true),
                    HomeTeamAvailable = table.Column<bool>(type: "INTEGER", nullable: true),
                    Overlaps = table.Column<bool>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScheduleRecommendationID = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduleRecommendationMatchID = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduledTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRecommendationsHistory",
                schema: "Event",
                columns: table => new
                {
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AppliedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    Fitness = table.Column<double>(type: "REAL", nullable: true),
                    Generations = table.Column<int>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedByID = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RequestedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    ScheduleRecommendationID = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserMergesHistory",
                schema: "Auth",
                columns: table => new
                {
                    Manifest = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    MergedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    RowsAdded = table.Column<int>(type: "INTEGER", nullable: true),
                    RowsMoved = table.Column<int>(type: "INTEGER", nullable: true),
                    RowsRemoved = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    TargetUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true, collation: "NOCASE"),
                    Trigger = table.Column<int>(type: "INTEGER", nullable: true),
                    TriggeredByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    UndoNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, collation: "NOCASE"),
                    UndoneAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UndoneByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    UserMergeID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "XuidLookupsHistory",
                schema: "Xbox",
                columns: table => new
                {
                    Error = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, collation: "NOCASE"),
                    LookedUpAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: true),
                    UserID = table.Column<int>(type: "INTEGER", nullable: true),
                    XboxUserID = table.Column<long>(type: "INTEGER", nullable: true)
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
                name: "IX_HistoricalImportSettingsHistory_HistoricalImportSettingsID_PeriodEnd",
                schema: "Event",
                table: "HistoricalImportSettingsHistory",
                columns: new[] { "HistoricalImportSettingsID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSeasonReviewsHistory_HistoricalImportJobID_PeriodEnd",
                schema: "Event",
                table: "HistoricalSeasonReviewsHistory",
                columns: new[] { "HistoricalImportJobID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalSeasonReviewsHistory_HistoricalSeasonReviewID_PeriodEnd",
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
                name: "IX_ScheduleRecommendationsHistory_ScheduleRecommendationID_PeriodEnd",
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

            migrationBuilder.Sql("""
                CREATE TRIGGER "DuplicateDismissals_History_Update" AFTER UPDATE ON "DuplicateDismissals" FOR EACH ROW BEGIN
                    INSERT INTO "DuplicateDismissalsHistory" ("DismissedAt", "DismissedByUserID", "HighUserID", "LowUserID", "PeriodStart", "PeriodEnd") VALUES (OLD."DismissedAt", OLD."DismissedByUserID", OLD."HighUserID", OLD."LowUserID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "DuplicateDismissals" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "DuplicateDismissals_History_Delete" AFTER DELETE ON "DuplicateDismissals" FOR EACH ROW BEGIN
                    INSERT INTO "DuplicateDismissalsHistory" ("DismissedAt", "DismissedByUserID", "HighUserID", "LowUserID", "PeriodStart", "PeriodEnd") VALUES (OLD."DismissedAt", OLD."DismissedByUserID", OLD."HighUserID", OLD."LowUserID", OLD."PeriodStart", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "HistoricalImportSettings_History_Update" AFTER UPDATE ON "HistoricalImportSettings" FOR EACH ROW BEGIN
                    INSERT INTO "HistoricalImportSettingsHistory" ("CreatedAt", "CreatedByID", "HistoricalImportSettingsID", "LastScheduledJobID", "LastScheduledRunAt", "LastScheduledRunNote", "ModifiedAt", "ModifiedByID", "PeriodStart", "SchedulePaused", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."HistoricalImportSettingsID", OLD."LastScheduledJobID", OLD."LastScheduledRunAt", OLD."LastScheduledRunNote", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SchedulePaused", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "HistoricalImportSettings" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "HistoricalImportSettings_History_Delete" AFTER DELETE ON "HistoricalImportSettings" FOR EACH ROW BEGIN
                    INSERT INTO "HistoricalImportSettingsHistory" ("CreatedAt", "CreatedByID", "HistoricalImportSettingsID", "LastScheduledJobID", "LastScheduledRunAt", "LastScheduledRunNote", "ModifiedAt", "ModifiedByID", "PeriodStart", "SchedulePaused", "PeriodEnd") VALUES (OLD."CreatedAt", OLD."CreatedByID", OLD."HistoricalImportSettingsID", OLD."LastScheduledJobID", OLD."LastScheduledRunAt", OLD."LastScheduledRunNote", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SchedulePaused", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "HistoricalSeasonReviews_History_Update" AFTER UPDATE ON "HistoricalSeasonReviews" FOR EACH ROW BEGIN
                    INSERT INTO "HistoricalSeasonReviewsHistory" ("ApprovalJobID", "CreatedAt", "CreatedByID", "DecidedAt", "HistoricalImportJobID", "HistoricalSeasonReviewID", "ModifiedAt", "ModifiedByID", "PeriodStart", "SeasonKey", "SeasonName", "StatsBefore", "StatsRemoved", "Status", "TeamsBefore", "TeamsRemoved", "TitlesBefore", "TitlesRemoved", "PeriodEnd") VALUES (OLD."ApprovalJobID", OLD."CreatedAt", OLD."CreatedByID", OLD."DecidedAt", OLD."HistoricalImportJobID", OLD."HistoricalSeasonReviewID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SeasonKey", OLD."SeasonName", OLD."StatsBefore", OLD."StatsRemoved", OLD."Status", OLD."TeamsBefore", OLD."TeamsRemoved", OLD."TitlesBefore", OLD."TitlesRemoved", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "HistoricalSeasonReviews" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "HistoricalSeasonReviews_History_Delete" AFTER DELETE ON "HistoricalSeasonReviews" FOR EACH ROW BEGIN
                    INSERT INTO "HistoricalSeasonReviewsHistory" ("ApprovalJobID", "CreatedAt", "CreatedByID", "DecidedAt", "HistoricalImportJobID", "HistoricalSeasonReviewID", "ModifiedAt", "ModifiedByID", "PeriodStart", "SeasonKey", "SeasonName", "StatsBefore", "StatsRemoved", "Status", "TeamsBefore", "TeamsRemoved", "TitlesBefore", "TitlesRemoved", "PeriodEnd") VALUES (OLD."ApprovalJobID", OLD."CreatedAt", OLD."CreatedByID", OLD."DecidedAt", OLD."HistoricalImportJobID", OLD."HistoricalSeasonReviewID", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SeasonKey", OLD."SeasonName", OLD."StatsBefore", OLD."StatsRemoved", OLD."Status", OLD."TeamsBefore", OLD."TeamsRemoved", OLD."TitlesBefore", OLD."TitlesRemoved", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "HistoricalSources_History_Update" AFTER UPDATE ON "HistoricalSources" FOR EACH ROW BEGIN
                    INSERT INTO "HistoricalSourcesHistory" ("Category", "Config", "CreatedAt", "CreatedByID", "CsvSheetName", "Enabled", "Format", "GameVersion", "Gid", "HistoricalSourceID", "IsDead", "Kind", "LastChangedAt", "LastFetchStatus", "LastFetchedAt", "League", "ModifiedAt", "ModifiedByID", "PeriodStart", "SheetKey", "Title", "Url", "WaybackTimestamp", "Year", "PeriodEnd") VALUES (OLD."Category", OLD."Config", OLD."CreatedAt", OLD."CreatedByID", OLD."CsvSheetName", OLD."Enabled", OLD."Format", OLD."GameVersion", OLD."Gid", OLD."HistoricalSourceID", OLD."IsDead", OLD."Kind", OLD."LastChangedAt", OLD."LastFetchStatus", OLD."LastFetchedAt", OLD."League", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SheetKey", OLD."Title", OLD."Url", OLD."WaybackTimestamp", OLD."Year", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "HistoricalSources" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "HistoricalSources_History_Delete" AFTER DELETE ON "HistoricalSources" FOR EACH ROW BEGIN
                    INSERT INTO "HistoricalSourcesHistory" ("Category", "Config", "CreatedAt", "CreatedByID", "CsvSheetName", "Enabled", "Format", "GameVersion", "Gid", "HistoricalSourceID", "IsDead", "Kind", "LastChangedAt", "LastFetchStatus", "LastFetchedAt", "League", "ModifiedAt", "ModifiedByID", "PeriodStart", "SheetKey", "Title", "Url", "WaybackTimestamp", "Year", "PeriodEnd") VALUES (OLD."Category", OLD."Config", OLD."CreatedAt", OLD."CreatedByID", OLD."CsvSheetName", OLD."Enabled", OLD."Format", OLD."GameVersion", OLD."Gid", OLD."HistoricalSourceID", OLD."IsDead", OLD."Kind", OLD."LastChangedAt", OLD."LastFetchStatus", OLD."LastFetchedAt", OLD."League", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."SheetKey", OLD."Title", OLD."Url", OLD."WaybackTimestamp", OLD."Year", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "ScheduleRecommendationMatches_History_Update" AFTER UPDATE ON "ScheduleRecommendationMatches" FOR EACH ROW BEGIN
                    INSERT INTO "ScheduleRecommendationMatchesHistory" ("AwayTeamAvailable", "HomeTeamAvailable", "Overlaps", "PeriodStart", "ScheduleRecommendationID", "ScheduleRecommendationMatchID", "ScheduledTime", "SeasonMatchID", "PeriodEnd") VALUES (OLD."AwayTeamAvailable", OLD."HomeTeamAvailable", OLD."Overlaps", OLD."PeriodStart", OLD."ScheduleRecommendationID", OLD."ScheduleRecommendationMatchID", OLD."ScheduledTime", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "ScheduleRecommendationMatches" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "ScheduleRecommendationMatches_History_Delete" AFTER DELETE ON "ScheduleRecommendationMatches" FOR EACH ROW BEGIN
                    INSERT INTO "ScheduleRecommendationMatchesHistory" ("AwayTeamAvailable", "HomeTeamAvailable", "Overlaps", "PeriodStart", "ScheduleRecommendationID", "ScheduleRecommendationMatchID", "ScheduledTime", "SeasonMatchID", "PeriodEnd") VALUES (OLD."AwayTeamAvailable", OLD."HomeTeamAvailable", OLD."Overlaps", OLD."PeriodStart", OLD."ScheduleRecommendationID", OLD."ScheduleRecommendationMatchID", OLD."ScheduledTime", OLD."SeasonMatchID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "ScheduleRecommendations_History_Update" AFTER UPDATE ON "ScheduleRecommendations" FOR EACH ROW BEGIN
                    INSERT INTO "ScheduleRecommendationsHistory" ("AppliedAt", "AppliedBy", "CreatedAt", "CreatedByID", "Fitness", "Generations", "ModifiedAt", "ModifiedByID", "PeriodStart", "RequestedBy", "ScheduleRecommendationID", "SeasonID", "PeriodEnd") VALUES (OLD."AppliedAt", OLD."AppliedBy", OLD."CreatedAt", OLD."CreatedByID", OLD."Fitness", OLD."Generations", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RequestedBy", OLD."ScheduleRecommendationID", OLD."SeasonID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "ScheduleRecommendations" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "ScheduleRecommendations_History_Delete" AFTER DELETE ON "ScheduleRecommendations" FOR EACH ROW BEGIN
                    INSERT INTO "ScheduleRecommendationsHistory" ("AppliedAt", "AppliedBy", "CreatedAt", "CreatedByID", "Fitness", "Generations", "ModifiedAt", "ModifiedByID", "PeriodStart", "RequestedBy", "ScheduleRecommendationID", "SeasonID", "PeriodEnd") VALUES (OLD."AppliedAt", OLD."AppliedBy", OLD."CreatedAt", OLD."CreatedByID", OLD."Fitness", OLD."Generations", OLD."ModifiedAt", OLD."ModifiedByID", OLD."PeriodStart", OLD."RequestedBy", OLD."ScheduleRecommendationID", OLD."SeasonID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "UserMerges_History_Update" AFTER UPDATE ON "UserMerges" FOR EACH ROW BEGIN
                    INSERT INTO "UserMergesHistory" ("Manifest", "MergedAt", "PeriodStart", "Reason", "RowsAdded", "RowsMoved", "RowsRemoved", "SourceUserID", "SourceUserName", "TargetUserID", "TargetUserName", "Trigger", "TriggeredByUserID", "UndoNote", "UndoneAt", "UndoneByUserID", "UserMergeID", "PeriodEnd") VALUES (OLD."Manifest", OLD."MergedAt", OLD."PeriodStart", OLD."Reason", OLD."RowsAdded", OLD."RowsMoved", OLD."RowsRemoved", OLD."SourceUserID", OLD."SourceUserName", OLD."TargetUserID", OLD."TargetUserName", OLD."Trigger", OLD."TriggeredByUserID", OLD."UndoNote", OLD."UndoneAt", OLD."UndoneByUserID", OLD."UserMergeID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "UserMerges" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "UserMerges_History_Delete" AFTER DELETE ON "UserMerges" FOR EACH ROW BEGIN
                    INSERT INTO "UserMergesHistory" ("Manifest", "MergedAt", "PeriodStart", "Reason", "RowsAdded", "RowsMoved", "RowsRemoved", "SourceUserID", "SourceUserName", "TargetUserID", "TargetUserName", "Trigger", "TriggeredByUserID", "UndoNote", "UndoneAt", "UndoneByUserID", "UserMergeID", "PeriodEnd") VALUES (OLD."Manifest", OLD."MergedAt", OLD."PeriodStart", OLD."Reason", OLD."RowsAdded", OLD."RowsMoved", OLD."RowsRemoved", OLD."SourceUserID", OLD."SourceUserName", OLD."TargetUserID", OLD."TargetUserName", OLD."Trigger", OLD."TriggeredByUserID", OLD."UndoNote", OLD."UndoneAt", OLD."UndoneByUserID", OLD."UserMergeID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "XuidLookups_History_Update" AFTER UPDATE ON "XuidLookups" FOR EACH ROW BEGIN
                    INSERT INTO "XuidLookupsHistory" ("Error", "LookedUpAt", "PeriodStart", "Status", "UserID", "XboxUserID", "PeriodEnd") VALUES (OLD."Error", OLD."LookedUpAt", OLD."PeriodStart", OLD."Status", OLD."UserID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                    UPDATE "XuidLookups" SET "PeriodStart" = rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.') WHERE rowid = NEW.rowid;
                END;
                CREATE TRIGGER "XuidLookups_History_Delete" AFTER DELETE ON "XuidLookups" FOR EACH ROW BEGIN
                    INSERT INTO "XuidLookupsHistory" ("Error", "LookedUpAt", "PeriodStart", "Status", "UserID", "XboxUserID", "PeriodEnd") VALUES (OLD."Error", OLD."LookedUpAt", OLD."PeriodStart", OLD."Status", OLD."UserID", OLD."XboxUserID", rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now'), '0'), '.'));
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "DuplicateDismissals_History_Update";
                DROP TRIGGER IF EXISTS "DuplicateDismissals_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "HistoricalImportSettings_History_Update";
                DROP TRIGGER IF EXISTS "HistoricalImportSettings_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "HistoricalSeasonReviews_History_Update";
                DROP TRIGGER IF EXISTS "HistoricalSeasonReviews_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "HistoricalSources_History_Update";
                DROP TRIGGER IF EXISTS "HistoricalSources_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "ScheduleRecommendationMatches_History_Update";
                DROP TRIGGER IF EXISTS "ScheduleRecommendationMatches_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "ScheduleRecommendations_History_Update";
                DROP TRIGGER IF EXISTS "ScheduleRecommendations_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "UserMerges_History_Update";
                DROP TRIGGER IF EXISTS "UserMerges_History_Delete";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "XuidLookups_History_Update";
                DROP TRIGGER IF EXISTS "XuidLookups_History_Delete";
                """);

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
