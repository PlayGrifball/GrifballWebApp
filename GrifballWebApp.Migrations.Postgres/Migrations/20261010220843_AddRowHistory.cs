using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRowHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Row history's triggers (GrifballWebApp.Database/PostgresHistory.cs), written by hand. A later
            // migration that creates a history table calls grif_enable_versioning, and one that drops it but
            // keeps its table grif_disable_versioning: scaffolded (HistoryMigrationsModelDiffer). Timestamps
            // are UTC without a time zone, like SQL Server's period columns.
            migrationBuilder.Sql("""
                -- BEFORE INSERT OR UPDATE, each row: when the row took its values. Set here, so anything the
                -- app writes there is replaced.
                CREATE FUNCTION public.grif_period_start() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                  NEW."PeriodStart" := transaction_timestamp() AT TIME ZONE 'UTC';
                  RETURN NEW;
                END $$;

                -- AFTER UPDATE and AFTER DELETE, each statement (old_rows: the rows as they were), and BEFORE
                -- TRUNCATE: the rows' old versions into the history table, ending when the transaction
                -- began. Copies the columns both tables have, by name, so a column only one of them has
                -- never fails the app's write. SECURITY DEFINER: runs as its owner, the administrator that
                -- ran the migrations, whom the history tables' guard lets write.
                CREATE FUNCTION public.grif_versioning() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
                DECLARE
                  main regclass := TG_RELID;
                  hist regclass := format('%I.%I', TG_TABLE_SCHEMA, TG_TABLE_NAME || 'History')::regclass;
                  cols text;
                  src text := CASE WHEN TG_OP = 'TRUNCATE' THEN main::text ELSE 'old_rows' END;
                BEGIN
                  -- Statement triggers fire for no rows too: a cascade from a parent with no children.
                  IF TG_OP <> 'TRUNCATE' THEN
                    IF NOT EXISTS (SELECT FROM old_rows) THEN
                      RETURN NULL;
                    END IF;
                  END IF;
                  SELECT string_agg(quote_ident(h.attname), ', ' ORDER BY h.attnum) INTO cols
                    FROM pg_attribute h
                    JOIN pg_attribute m ON m.attrelid = main AND m.attname = h.attname AND m.attnum > 0 AND NOT m.attisdropped
                   WHERE h.attrelid = hist AND h.attnum > 0 AND NOT h.attisdropped AND h.attname <> 'PeriodEnd';
                  EXECUTE format('INSERT INTO %s (%s, "PeriodEnd") SELECT %s, $1 FROM %s', hist, cols, cols, src)
                    USING transaction_timestamp() AT TIME ZONE 'UTC';
                  RETURN NULL;
                END $$;
                REVOKE ALL ON FUNCTION public.grif_versioning() FROM PUBLIC;

                -- BEFORE any write to a history table, each statement: only its owner (or a member of the
                -- owner's role) may. The Helm chart grants the app's login writes on every table, history
                -- tables included; this takes them back. In grif_versioning, current_user is the owner.
                CREATE FUNCTION public.grif_history_guard() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
                BEGIN
                  IF NOT pg_has_role(current_user, (SELECT relowner FROM pg_class WHERE oid = TG_RELID), 'USAGE') THEN
                    RAISE EXCEPTION 'history table %.% is read-only', TG_TABLE_SCHEMA, TG_TABLE_NAME
                      USING ERRCODE = 'insufficient_privilege';
                  END IF;
                  RETURN NULL;
                END $$;

                -- Versioning on, for a table with a PeriodStart column and a "<Table>History" beside it.
                -- UPDATE and DELETE have a trigger each: a trigger with a transition table takes one event.
                CREATE PROCEDURE public.grif_enable_versioning(tbl regclass)
                LANGUAGE plpgsql AS $$
                DECLARE
                  s text;
                  t text;
                BEGIN
                  SELECT n.nspname, c.relname INTO s, t FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = tbl;
                  EXECUTE format('CREATE TRIGGER period_start BEFORE INSERT OR UPDATE ON %s FOR EACH ROW EXECUTE FUNCTION public.grif_period_start()', tbl);
                  EXECUTE format('CREATE TRIGGER versioning_update AFTER UPDATE ON %s REFERENCING OLD TABLE AS old_rows FOR EACH STATEMENT EXECUTE FUNCTION public.grif_versioning()', tbl);
                  EXECUTE format('CREATE TRIGGER versioning_delete AFTER DELETE ON %s REFERENCING OLD TABLE AS old_rows FOR EACH STATEMENT EXECUTE FUNCTION public.grif_versioning()', tbl);
                  EXECUTE format('CREATE TRIGGER versioning_truncate BEFORE TRUNCATE ON %s FOR EACH STATEMENT EXECUTE FUNCTION public.grif_versioning()', tbl);
                  EXECUTE format('CREATE TRIGGER read_only BEFORE INSERT OR UPDATE OR DELETE OR TRUNCATE ON %I.%I FOR EACH STATEMENT EXECUTE FUNCTION public.grif_history_guard()', s, t || 'History');
                END $$;

                -- Versioning off, before a history table is dropped and its table kept.
                CREATE PROCEDURE public.grif_disable_versioning(tbl regclass)
                LANGUAGE plpgsql AS $$
                BEGIN
                  EXECUTE format('DROP TRIGGER IF EXISTS period_start ON %s', tbl);
                  EXECUTE format('DROP TRIGGER IF EXISTS versioning_update ON %s', tbl);
                  EXECUTE format('DROP TRIGGER IF EXISTS versioning_delete ON %s', tbl);
                  EXECUTE format('DROP TRIGGER IF EXISTS versioning_truncate ON %s', tbl);
                END $$;
                """);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XboxUsers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserTokens",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "Users",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserRoles",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserLogins",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Other",
                table: "UserExperiences",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserClaims",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "Teams",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamPlayers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamAvailability",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SignupAvailability",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonSignups",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "Seasons",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonMatches",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonAvailability",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "Roles",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "RoleClaims",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Other",
                table: "Regions",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "Ranks",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "QueuedPlayers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "PasswordResetLinks",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Discord",
                table: "Messages",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalTypes",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Medals",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalEarned",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalDifficulties",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchTeams",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchParticipants",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchLinks",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Matches",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedTeams",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedPlayers",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedMatches",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedKickVotes",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchBracketInfo",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Other",
                table: "GameVersions",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "User",
                table: "Discord",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Auth",
                table: "DataProtectionKeys",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                schema: "Event",
                table: "AvailabilityOptions",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "transaction_timestamp() AT TIME ZONE 'UTC'");

            migrationBuilder.CreateTable(
                name: "AvailabilityOptionsHistory",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeysHistory",
                schema: "Auth",
                columns: table => new
                {
                    FriendlyName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Id = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Xml = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "DiscordHistory",
                schema: "User",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: true),
                    DiscordUsername = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "GameVersionsHistory",
                schema: "Other",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    GameVersionName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu"),
                    GameVesionID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchBracketInfoHistory",
                schema: "Event",
                columns: table => new
                {
                    AwayTeamPreviousMatchBracketInfoID = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamSeedNumber = table.Column<int>(type: "integer", nullable: true),
                    Bracket = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamPreviousMatchBracketInfoID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamSeedNumber = table.Column<int>(type: "integer", nullable: true),
                    MatchBracketInfoID = table.Column<int>(type: "integer", nullable: true),
                    MatchNumber = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedKickVotesHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    KickMatchedPlayerId = table.Column<int>(type: "integer", nullable: true),
                    MatchId = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    VoterMatchedPlayerId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedMatchesHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    Active = table.Column<bool>(type: "boolean", nullable: true),
                    AwayTeamId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamId = table.Column<int>(type: "integer", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ThreadID = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    VoteMessageID = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedPlayersHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: true),
                    Kicked = table.Column<bool>(type: "boolean", nullable: true),
                    MatchedTeamID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedTeamsHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchedTeamId = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchedWinnerVotesHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchId = table.Column<int>(type: "integer", nullable: true),
                    MatchedPlayerId = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    WinnerVote = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchesHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StatsPullDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchLinksHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchLinkID = table.Column<int>(type: "integer", nullable: true),
                    MatchNumber = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchParticipantsHistory",
                schema: "Infinite",
                columns: table => new
                {
                    Accuracy = table.Column<float>(type: "real", nullable: true),
                    Assists = table.Column<int>(type: "integer", nullable: true),
                    AverageLife = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Betrayals = table.Column<int>(type: "integer", nullable: true),
                    CalloutAssists = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DamageDealt = table.Column<int>(type: "integer", nullable: true),
                    DamageTaken = table.Column<int>(type: "integer", nullable: true),
                    Deaths = table.Column<int>(type: "integer", nullable: true),
                    FirstJoinedTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    JoinedInProgress = table.Column<bool>(type: "boolean", nullable: true),
                    Kda = table.Column<float>(type: "real", nullable: true),
                    Kills = table.Column<int>(type: "integer", nullable: true),
                    LastLeaveTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LeftInProgress = table.Column<bool>(type: "boolean", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    MaxKillingSpree = table.Column<int>(type: "integer", nullable: true),
                    MeleeKills = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    ObjectivesCompleted = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PersonalScore = table.Column<int>(type: "integer", nullable: true),
                    PowerWeaponKills = table.Column<int>(type: "integer", nullable: true),
                    PresentAtBeginning = table.Column<bool>(type: "boolean", nullable: true),
                    PresentAtCompletion = table.Column<bool>(type: "boolean", nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    RoundsLost = table.Column<int>(type: "integer", nullable: true),
                    RoundsTied = table.Column<int>(type: "integer", nullable: true),
                    RoundsWon = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    ShotsFired = table.Column<int>(type: "integer", nullable: true),
                    ShotsHit = table.Column<int>(type: "integer", nullable: true),
                    Spawns = table.Column<int>(type: "integer", nullable: true),
                    Suicides = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    TimePlayed = table.Column<TimeSpan>(type: "interval", nullable: true),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MatchTeamsHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MedalDifficultiesHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MedalDifficultyID = table.Column<int>(type: "integer", nullable: true),
                    MedalDifficultyName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MedalEarnedHistory",
                schema: "Infinite",
                columns: table => new
                {
                    Count = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MatchID = table.Column<Guid>(type: "uuid", nullable: true),
                    MedalID = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalPersonalScoreAwarded = table.Column<int>(type: "integer", nullable: true),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MedalsHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    MedalDifficultyID = table.Column<int>(type: "integer", nullable: true),
                    MedalID = table.Column<long>(type: "bigint", nullable: true),
                    MedalName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    MedalTypeID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PersonalScore = table.Column<int>(type: "integer", nullable: true),
                    SortingWeight = table.Column<int>(type: "integer", nullable: true),
                    SpriteIndex = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MedalTypesHistory",
                schema: "Infinite",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    MedalTypeID = table.Column<int>(type: "integer", nullable: true),
                    MedalTypeName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "MessagesHistory",
                schema: "Discord",
                columns: table => new
                {
                    Content = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    FromDiscordUserId = table.Column<long>(type: "bigint", nullable: true),
                    Id = table.Column<long>(type: "bigint", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ToDiscordUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetLinksHistory",
                schema: "Auth",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true, collation: "und-x-icu"),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "QueuedPlayersHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "RanksHistory",
                schema: "Matchmaking",
                columns: table => new
                {
                    Color = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Icon = table.Column<byte[]>(type: "bytea", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: true),
                    MmrThreshold = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "RegionsHistory",
                schema: "Other",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RegionID = table.Column<int>(type: "integer", nullable: true),
                    RegionName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "RoleClaimsHistory",
                schema: "Auth",
                columns: table => new
                {
                    ClaimType = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ClaimValue = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Id = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "RolesHistory",
                schema: "Auth",
                columns: table => new
                {
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SeasonAvailabilityHistory",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SeasonID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SeasonMatchesHistory",
                schema: "Event",
                columns: table => new
                {
                    ActiveRescheduleRequestId = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamID = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamResult = table.Column<int>(type: "integer", nullable: true),
                    AwayTeamScore = table.Column<int>(type: "integer", nullable: true),
                    BestOf = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamID = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamResult = table.Column<int>(type: "integer", nullable: true),
                    HomeTeamScore = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ScheduledTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    SeasonMatchID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SeasonsHistory",
                schema: "Event",
                columns: table => new
                {
                    CaptainsLocked = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DraftStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PublicAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SeasonEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    SeasonName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu"),
                    SeasonStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SignupsClose = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SignupsOpen = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SeasonSignupsHistory",
                schema: "Event",
                columns: table => new
                {
                    Approved = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RequiresAssistanceDrafting = table.Column<bool>(type: "boolean", nullable: true),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    SeasonSignupID = table.Column<int>(type: "integer", nullable: true),
                    TeamName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: true),
                    WillCaptain = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SignupAvailabilityHistory",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SeasonSignupID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "TeamAvailabilityHistory",
                schema: "Event",
                columns: table => new
                {
                    AvailabilityOptionID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TeamID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "TeamPlayersHistory",
                schema: "Event",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DraftCaptainOrder = table.Column<int>(type: "integer", nullable: true),
                    DraftPick = table.Column<int>(type: "integer", nullable: true),
                    DraftRound = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    TeamPlayerID = table.Column<int>(type: "integer", nullable: true),
                    UserID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "TeamsHistory",
                schema: "Event",
                columns: table => new
                {
                    CaptainID = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SeasonID = table.Column<int>(type: "integer", nullable: true),
                    TeamID = table.Column<int>(type: "integer", nullable: true),
                    TeamName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserClaimsHistory",
                schema: "Auth",
                columns: table => new
                {
                    ClaimType = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ClaimValue = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserExperiencesHistory",
                schema: "Other",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    GameVersionID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserLoginsHistory",
                schema: "Auth",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    LoginProvider = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ProviderKey = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserRolesHistory",
                schema: "Auth",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UsersHistory",
                schema: "Auth",
                columns: table => new
                {
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    DiscordUserID = table.Column<long>(type: "bigint", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true, collation: "und-x-icu"),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: true),
                    IsDummyUser = table.Column<bool>(type: "boolean", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LossStreak = table.Column<int>(type: "integer", nullable: true),
                    Losses = table.Column<int>(type: "integer", nullable: true),
                    MMR = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    PasswordHash = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: true),
                    RegionID = table.Column<int>(type: "integer", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, collation: "und-x-icu"),
                    WinStreak = table.Column<int>(type: "integer", nullable: true),
                    Wins = table.Column<int>(type: "integer", nullable: true),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "UserTokensHistory",
                schema: "Auth",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    Name = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "XboxUsersHistory",
                schema: "Xbox",
                columns: table => new
                {
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByID = table.Column<int>(type: "integer", nullable: true),
                    Gamertag = table.Column<string>(type: "text", nullable: true, collation: "und-x-icu"),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModifiedByID = table.Column<int>(type: "integer", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    XboxUserID = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityOptionsHistory_AvailabilityOptionID_PeriodEnd",
                schema: "Event",
                table: "AvailabilityOptionsHistory",
                columns: new[] { "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_DataProtectionKeysHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "DataProtectionKeysHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscordHistory_DiscordUserID_PeriodEnd",
                schema: "User",
                table: "DiscordHistory",
                columns: new[] { "DiscordUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_GameVersionsHistory_GameVesionID_PeriodEnd",
                schema: "Other",
                table: "GameVersionsHistory",
                columns: new[] { "GameVesionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchBracketInfoHistory_MatchBracketInfoID_PeriodEnd",
                schema: "Event",
                table: "MatchBracketInfoHistory",
                columns: new[] { "MatchBracketInfoID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedKickVotesHistory_MatchId_VoterMatchedPlayerId_Period~",
                schema: "Matchmaking",
                table: "MatchedKickVotesHistory",
                columns: new[] { "MatchId", "VoterMatchedPlayerId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedMatchesHistory_Id_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedMatchesHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedPlayersHistory_Id_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedPlayersHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedTeamsHistory_MatchedTeamId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedTeamsHistory",
                columns: new[] { "MatchedTeamId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchedWinnerVotesHistory_MatchId_MatchedPlayerId_PeriodEnd",
                schema: "Matchmaking",
                table: "MatchedWinnerVotesHistory",
                columns: new[] { "MatchId", "MatchedPlayerId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchesHistory_MatchID_PeriodEnd",
                schema: "Infinite",
                table: "MatchesHistory",
                columns: new[] { "MatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchLinksHistory_MatchLinkID_PeriodEnd",
                schema: "Event",
                table: "MatchLinksHistory",
                columns: new[] { "MatchLinkID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchParticipantsHistory_MatchID_XboxUserID_PeriodEnd",
                schema: "Infinite",
                table: "MatchParticipantsHistory",
                columns: new[] { "MatchID", "XboxUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchTeamsHistory_MatchID_TeamID_PeriodEnd",
                schema: "Infinite",
                table: "MatchTeamsHistory",
                columns: new[] { "MatchID", "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalDifficultiesHistory_MedalDifficultyID_PeriodEnd",
                schema: "Infinite",
                table: "MedalDifficultiesHistory",
                columns: new[] { "MedalDifficultyID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalEarnedHistory_MedalID_MatchID_XboxUserID_PeriodEnd",
                schema: "Infinite",
                table: "MedalEarnedHistory",
                columns: new[] { "MedalID", "MatchID", "XboxUserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalsHistory_MedalID_PeriodEnd",
                schema: "Infinite",
                table: "MedalsHistory",
                columns: new[] { "MedalID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MedalTypesHistory_MedalTypeID_PeriodEnd",
                schema: "Infinite",
                table: "MedalTypesHistory",
                columns: new[] { "MedalTypeID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_MessagesHistory_Id_PeriodEnd",
                schema: "Discord",
                table: "MessagesHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetLinksHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "PasswordResetLinksHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_QueuedPlayersHistory_UserID_PeriodEnd",
                schema: "Matchmaking",
                table: "QueuedPlayersHistory",
                columns: new[] { "UserID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_RanksHistory_Id_PeriodEnd",
                schema: "Matchmaking",
                table: "RanksHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_RegionsHistory_RegionID_PeriodEnd",
                schema: "Other",
                table: "RegionsHistory",
                columns: new[] { "RegionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaimsHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "RoleClaimsHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_RolesHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "RolesHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonAvailabilityHistory_SeasonID_AvailabilityOptionID_Per~",
                schema: "Event",
                table: "SeasonAvailabilityHistory",
                columns: new[] { "SeasonID", "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonMatchesHistory_SeasonMatchID_PeriodEnd",
                schema: "Event",
                table: "SeasonMatchesHistory",
                columns: new[] { "SeasonMatchID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonsHistory_SeasonID_PeriodEnd",
                schema: "Event",
                table: "SeasonsHistory",
                columns: new[] { "SeasonID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSignupsHistory_SeasonSignupID_PeriodEnd",
                schema: "Event",
                table: "SeasonSignupsHistory",
                columns: new[] { "SeasonSignupID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_SignupAvailabilityHistory_SeasonSignupID_AvailabilityOption~",
                schema: "Event",
                table: "SignupAvailabilityHistory",
                columns: new[] { "SeasonSignupID", "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamAvailabilityHistory_TeamID_AvailabilityOptionID_PeriodE~",
                schema: "Event",
                table: "TeamAvailabilityHistory",
                columns: new[] { "TeamID", "AvailabilityOptionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayersHistory_TeamPlayerID_PeriodEnd",
                schema: "Event",
                table: "TeamPlayersHistory",
                columns: new[] { "TeamPlayerID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamsHistory_TeamID_PeriodEnd",
                schema: "Event",
                table: "TeamsHistory",
                columns: new[] { "TeamID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserClaimsHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "UserClaimsHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserExperiencesHistory_UserID_GameVersionID_PeriodEnd",
                schema: "Other",
                table: "UserExperiencesHistory",
                columns: new[] { "UserID", "GameVersionID", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginsHistory_LoginProvider_ProviderKey_PeriodEnd",
                schema: "Auth",
                table: "UserLoginsHistory",
                columns: new[] { "LoginProvider", "ProviderKey", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRolesHistory_UserId_RoleId_PeriodEnd",
                schema: "Auth",
                table: "UserRolesHistory",
                columns: new[] { "UserId", "RoleId", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UsersHistory_Id_PeriodEnd",
                schema: "Auth",
                table: "UsersHistory",
                columns: new[] { "Id", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTokensHistory_UserId_LoginProvider_Name_PeriodEnd",
                schema: "Auth",
                table: "UserTokensHistory",
                columns: new[] { "UserId", "LoginProvider", "Name", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_XboxUsersHistory_XboxUserID_PeriodEnd",
                schema: "Xbox",
                table: "XboxUsersHistory",
                columns: new[] { "XboxUserID", "PeriodEnd" });

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"AvailabilityOptions\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"DataProtectionKeys\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"User\".\"Discord\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Other\".\"GameVersions\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"MatchBracketInfo\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"MatchedKickVotes\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"MatchedMatches\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"MatchedPlayers\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"MatchedTeams\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"MatchedWinnerVotes\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Infinite\".\"Matches\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"MatchLinks\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Infinite\".\"MatchParticipants\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Infinite\".\"MatchTeams\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Infinite\".\"MedalDifficulties\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Infinite\".\"MedalEarned\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Infinite\".\"Medals\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Infinite\".\"MedalTypes\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Discord\".\"Messages\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"PasswordResetLinks\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"QueuedPlayers\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Matchmaking\".\"Ranks\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Other\".\"Regions\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"RoleClaims\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"Roles\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"SeasonAvailability\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"SeasonMatches\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"Seasons\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"SeasonSignups\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"SignupAvailability\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"TeamAvailability\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"TeamPlayers\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Event\".\"Teams\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"UserClaims\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Other\".\"UserExperiences\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"UserLogins\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"UserRoles\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"Users\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Auth\".\"UserTokens\"');");

            migrationBuilder.Sql("CALL public.grif_enable_versioning('\"Xbox\".\"XboxUsers\"');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Xbox\".\"XboxUsers\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"UserTokens\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"Users\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"UserRoles\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"UserLogins\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Other\".\"UserExperiences\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"UserClaims\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"Teams\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"TeamPlayers\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"TeamAvailability\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"SignupAvailability\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"SeasonSignups\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"Seasons\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"SeasonMatches\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"SeasonAvailability\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"Roles\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"RoleClaims\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Other\".\"Regions\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"Ranks\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"QueuedPlayers\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"PasswordResetLinks\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Discord\".\"Messages\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Infinite\".\"MedalTypes\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Infinite\".\"Medals\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Infinite\".\"MedalEarned\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Infinite\".\"MedalDifficulties\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Infinite\".\"MatchTeams\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Infinite\".\"MatchParticipants\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"MatchLinks\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Infinite\".\"Matches\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"MatchedWinnerVotes\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"MatchedTeams\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"MatchedPlayers\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"MatchedMatches\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Matchmaking\".\"MatchedKickVotes\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"MatchBracketInfo\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Other\".\"GameVersions\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"User\".\"Discord\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Auth\".\"DataProtectionKeys\"');");

            migrationBuilder.Sql("CALL public.grif_disable_versioning('\"Event\".\"AvailabilityOptions\"');");

            migrationBuilder.DropTable(
                name: "AvailabilityOptionsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "DataProtectionKeysHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "DiscordHistory",
                schema: "User");

            migrationBuilder.DropTable(
                name: "GameVersionsHistory",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "MatchBracketInfoHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchedKickVotesHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedMatchesHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedPlayersHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedTeamsHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchedWinnerVotesHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "MatchesHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MatchLinksHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "MatchParticipantsHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MatchTeamsHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalDifficultiesHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalEarnedHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalsHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MedalTypesHistory",
                schema: "Infinite");

            migrationBuilder.DropTable(
                name: "MessagesHistory",
                schema: "Discord");

            migrationBuilder.DropTable(
                name: "PasswordResetLinksHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "QueuedPlayersHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "RanksHistory",
                schema: "Matchmaking");

            migrationBuilder.DropTable(
                name: "RegionsHistory",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "RoleClaimsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "RolesHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "SeasonAvailabilityHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonMatchesHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SeasonSignupsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "SignupAvailabilityHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamAvailabilityHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamPlayersHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "TeamsHistory",
                schema: "Event");

            migrationBuilder.DropTable(
                name: "UserClaimsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserExperiencesHistory",
                schema: "Other");

            migrationBuilder.DropTable(
                name: "UserLoginsHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserRolesHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UsersHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "UserTokensHistory",
                schema: "Auth");

            migrationBuilder.DropTable(
                name: "XboxUsersHistory",
                schema: "Xbox");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Xbox",
                table: "XboxUsers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserTokens");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Other",
                table: "UserExperiences");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "UserClaims");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "TeamAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SignupAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonSignups");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonMatches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "SeasonAvailability");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "RoleClaims");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Other",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "Ranks");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "QueuedPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "PasswordResetLinks");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Discord",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalTypes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Medals");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalEarned");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MedalDifficulties");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchTeams");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "MatchParticipants");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchLinks");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Infinite",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedWinnerVotes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedTeams");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedPlayers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedMatches");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Matchmaking",
                table: "MatchedKickVotes");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "MatchBracketInfo");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Other",
                table: "GameVersions");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "User",
                table: "Discord");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Auth",
                table: "DataProtectionKeys");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                schema: "Event",
                table: "AvailabilityOptions");

            migrationBuilder.Sql("""
                DROP PROCEDURE public.grif_disable_versioning(regclass);
                DROP PROCEDURE public.grif_enable_versioning(regclass);
                DROP FUNCTION public.grif_history_guard();
                DROP FUNCTION public.grif_versioning();
                DROP FUNCTION public.grif_period_start();
                """);
        }
    }
}
