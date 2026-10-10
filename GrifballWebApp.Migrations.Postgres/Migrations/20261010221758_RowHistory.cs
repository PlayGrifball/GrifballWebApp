using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrifballWebApp.Migrations.Postgres.Migrations
{
    /// <summary>
    /// Row history (readme.md, Database): when the database has the periods extension, every table gets a
    /// SYSTEM_TIME period (PeriodStart, PeriodEnd, as SQL Server's temporal tables have) and SYSTEM
    /// VERSIONING, as SQL Server's tables are temporal. Without it, nothing: the extension is the switch,
    /// installed by a superuser (the Helm chart's database.history.enabled does it), never by this
    /// migration. The tables are the model's as this was written; tables created later are versioned as
    /// they're created (RowHistoryMigrationsSqlGenerator). A table already versioned is left as it is.
    /// Down removes it all, the history with it.
    /// </summary>
    public partial class RowHistory : Migration
    {
        private const string Tables = """
            ARRAY[
                '"Auth"."DataProtectionKeys"',
                '"Auth"."PasswordResetLinks"',
                '"Auth"."RoleClaims"',
                '"Auth"."Roles"',
                '"Auth"."UserClaims"',
                '"Auth"."UserLogins"',
                '"Auth"."UserRoles"',
                '"Auth"."Users"',
                '"Auth"."UserTokens"',
                '"Discord"."Messages"',
                '"Event"."AvailabilityOptions"',
                '"Event"."MatchBracketInfo"',
                '"Event"."MatchLinks"',
                '"Event"."SeasonAvailability"',
                '"Event"."SeasonMatches"',
                '"Event"."Seasons"',
                '"Event"."SeasonSignups"',
                '"Event"."SignupAvailability"',
                '"Event"."TeamAvailability"',
                '"Event"."TeamPlayers"',
                '"Event"."Teams"',
                '"Infinite"."Matches"',
                '"Infinite"."MatchParticipants"',
                '"Infinite"."MatchTeams"',
                '"Infinite"."MedalDifficulties"',
                '"Infinite"."MedalEarned"',
                '"Infinite"."Medals"',
                '"Infinite"."MedalTypes"',
                '"Matchmaking"."MatchedKickVotes"',
                '"Matchmaking"."MatchedMatches"',
                '"Matchmaking"."MatchedPlayers"',
                '"Matchmaking"."MatchedTeams"',
                '"Matchmaking"."MatchedWinnerVotes"',
                '"Matchmaking"."QueuedPlayers"',
                '"Matchmaking"."Ranks"',
                '"Other"."GameVersions"',
                '"Other"."Regions"',
                '"Other"."UserExperiences"',
                '"public"."MatchReschedules"',
                '"User"."Discord"',
                '"Xbox"."XboxUsers"'
            ]::regclass[]
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // periods' catalog and functions are named only in statements that run with the extension:
            // PL/pgSQL plans each statement as it first runs it.
            migrationBuilder.Sql($"""
                DO $grif_history$
                DECLARE
                    t regclass;
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_extension WHERE extname = 'periods') THEN
                        RETURN;
                    END IF;
                    FOREACH t IN ARRAY {Tables} LOOP
                        IF NOT EXISTS (SELECT FROM periods.system_versioning AS v WHERE v.table_name = t) THEN
                            PERFORM periods.add_system_time_period(t, 'PeriodStart', 'PeriodEnd');
                            PERFORM periods.add_system_versioning(t);
                        END IF;
                    END LOOP;
                END $grif_history$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Purged: the history tables, views, functions, triggers and constraints; the period's columns
            // stay unless dropped.
            migrationBuilder.Sql($"""
                DO $grif_history$
                DECLARE
                    t regclass;
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_extension WHERE extname = 'periods') THEN
                        RETURN;
                    END IF;
                    FOREACH t IN ARRAY {Tables} LOOP
                        IF EXISTS (SELECT FROM periods.system_versioning AS v WHERE v.table_name = t) THEN
                            PERFORM periods.drop_system_versioning(t, purge => true);
                        END IF;
                        IF EXISTS (SELECT FROM periods.periods AS p WHERE p.table_name = t AND p.period_name = 'system_time') THEN
                            PERFORM periods.drop_system_time_period(t, purge => true);
                        END IF;
                        EXECUTE format('ALTER TABLE %s DROP COLUMN IF EXISTS "PeriodStart", DROP COLUMN IF EXISTS "PeriodEnd"', t);
                    END LOOP;
                END $grif_history$;
                """);
        }
    }
}
