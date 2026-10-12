using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.Internal;

namespace GrifballWebApp.Database;

/// <summary>
/// Row history's functions and procedures (PostgresHistory): the triggers' code, versioned. A model with
/// history carries the current version (<see cref="Annotation"/>), which the migrations snapshot records.
/// GrifballWebApp.Migrations.Postgres scaffolds from it: the migration that gives the first table history
/// creates them, one that takes away the last drops them, and when the version changes it creates the
/// new version over the old. To change a function, add a version, with the whole of its SQL: migrations
/// add then scaffolds the change, and its Down the version before. Old versions stay here, as the
/// migrations that go back to them need them.
/// </summary>
public static class PostgresHistoryFunctions
{
    /// <summary>On the model: the version of the functions its history uses.</summary>
    public const string Annotation = "Grif:HistoryFunctions";

    /// <summary>Every version, oldest first.</summary>
    public static IReadOnlyList<HistoryFunctionsVersion> Versions { get; } =
    [
        new(1, Create: V1, Drop: DropV1),
    ];

    /// <summary>The version a model with history gets.</summary>
    public static int Current => Versions[^1].Number;

    // Each statement replaces what it creates, so a version's Create installs it over any other version
    // (one that changes a signature or drops a function says so first: CREATE OR REPLACE can't change a
    // signature). Replaced, a function keeps its triggers, owner and grants. A version that changes what
    // grif_refresh_versioning writes calls it again for each table with history.
    private const string V1 = """
        -- BEFORE INSERT OR UPDATE, each row: when the row took its values. Set here, so anything the
        -- app writes there is replaced.
        CREATE OR REPLACE FUNCTION public.grif_period_start() RETURNS trigger
        LANGUAGE plpgsql AS $$
        BEGIN
          NEW."PeriodStart" := transaction_timestamp();
          RETURN NEW;
        END $$;

        -- BEFORE any write to a history table, each statement: only its owner (or a member of the
        -- owner's role) may. The Helm chart grants the app's login writes on every table, history
        -- tables included; this takes them back. In a table's versioning function, current_user is
        -- the owner.
        CREATE OR REPLACE FUNCTION public.grif_history_guard() RETURNS trigger
        LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
        BEGIN
          IF NOT pg_has_role(current_user, (SELECT relowner FROM pg_class WHERE oid = TG_RELID), 'USAGE') THEN
            RAISE EXCEPTION 'history table %.% is read-only', TG_TABLE_SCHEMA, TG_TABLE_NAME
              USING ERRCODE = 'insufficient_privilege';
          END IF;
          RETURN NULL;
        END $$;

        -- Writes a table's versioning function: its triggers' function, or a new one beside it,
        -- "<Table>_versioning", with its triggers. AFTER UPDATE and AFTER DELETE, each statement
        -- (old_rows: the rows as they were), and BEFORE TRUNCATE, it copies the rows' old versions
        -- into the history table, ending when the transaction began. Its INSERT names the tables, and
        -- the columns both have (by name, bar PeriodEnd: the table's is always infinity), as they are
        -- now: plain SQL, which Postgres parses once a session, not SQL built from the catalog on
        -- every write. So a migration that renames a table, or adds, drops or renames a column, calls
        -- this again once it has (HistoryMigrationsModelDiffer scaffolds the call). SECURITY DEFINER:
        -- the function runs as its owner, the administrator that ran the migrations, whom the history
        -- tables' guard lets write.
        CREATE OR REPLACE PROCEDURE public.grif_refresh_versioning(tbl regclass)
        LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp AS $$
        DECLARE
          s name;
          t name;
          hist regclass;
          cols text;
          fn regprocedure;
          fn_schema name;
          fn_name name;
          own name;
          i int := 0;
        BEGIN
          SELECT ns.nspname, c.relname INTO s, t FROM pg_class c JOIN pg_namespace ns ON ns.oid = c.relnamespace WHERE c.oid = tbl;
          hist := format('%I.%I', s, t || 'History')::regclass;
          SELECT string_agg(quote_ident(h.attname), ', ' ORDER BY h.attnum) INTO cols
            FROM pg_attribute h
            JOIN pg_attribute m ON m.attrelid = tbl AND m.attname = h.attname AND m.attnum > 0 AND NOT m.attisdropped
           WHERE h.attrelid = hist AND h.attnum > 0 AND NOT h.attisdropped AND h.attname <> 'PeriodEnd';

          -- The function takes its table's schema and name where they're free: a renamed table's too.
          own := t || '_versioning';
          SELECT tgfoid INTO fn FROM pg_trigger WHERE tgrelid = tbl AND tgname = 'versioning_update';
          IF fn IS NOT NULL THEN
            SELECT ns.nspname, p.proname INTO fn_schema, fn_name FROM pg_proc p JOIN pg_namespace ns ON ns.oid = p.pronamespace WHERE p.oid = fn;
            IF fn_schema <> s AND to_regprocedure(format('%I.%I()', s, fn_name)) IS NULL THEN
              EXECUTE format('ALTER FUNCTION %s SET SCHEMA %I', fn, s);
              fn_schema := s;
            END IF;
            IF fn_name <> own AND to_regprocedure(format('%I.%I()', fn_schema, own)) IS NULL THEN
              EXECUTE format('ALTER FUNCTION %s RENAME TO %I', fn, own);
              fn_name := own;
            END IF;
          ELSE
            fn_schema := s;
            fn_name := own;
            WHILE to_regprocedure(format('%I.%I()', fn_schema, fn_name)) IS NOT NULL LOOP
              i := i + 1;
              fn_name := own || i;
            END LOOP;
          END IF;

          -- Statement triggers fire for no rows too (a cascade from a parent with no children): no
          -- INSERT then, which would fire the history table's guard for nothing.
          EXECUTE format($f$
            CREATE OR REPLACE FUNCTION %I.%I() RETURNS trigger
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $body$
            BEGIN
              IF TG_OP = 'TRUNCATE' THEN
                INSERT INTO %s (%s, "PeriodEnd") SELECT %s, transaction_timestamp() FROM %s;
              ELSIF EXISTS (SELECT FROM old_rows) THEN
                INSERT INTO %s (%s, "PeriodEnd") SELECT %s, transaction_timestamp() FROM old_rows;
              END IF;
              RETURN NULL;
            END $body$
            $f$, fn_schema, fn_name, hist, cols, cols, tbl, hist, cols, cols);
          EXECUTE format('REVOKE ALL ON FUNCTION %I.%I() FROM PUBLIC', fn_schema, fn_name);

          -- UPDATE and DELETE have a trigger each: a trigger with a transition table takes one event.
          IF fn IS NULL THEN
            EXECUTE format('CREATE TRIGGER versioning_update AFTER UPDATE ON %s REFERENCING OLD TABLE AS old_rows FOR EACH STATEMENT EXECUTE FUNCTION %I.%I()', tbl, fn_schema, fn_name);
            EXECUTE format('CREATE TRIGGER versioning_delete AFTER DELETE ON %s REFERENCING OLD TABLE AS old_rows FOR EACH STATEMENT EXECUTE FUNCTION %I.%I()', tbl, fn_schema, fn_name);
            EXECUTE format('CREATE TRIGGER versioning_truncate BEFORE TRUNCATE ON %s FOR EACH STATEMENT EXECUTE FUNCTION %I.%I()', tbl, fn_schema, fn_name);
          END IF;
        END $$;

        -- Versioning on, for a table with a PeriodStart column and a "<Table>History" beside it.
        CREATE OR REPLACE PROCEDURE public.grif_enable_versioning(tbl regclass)
        LANGUAGE plpgsql AS $$
        DECLARE
          s text;
          t text;
        BEGIN
          SELECT n.nspname, c.relname INTO s, t FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = tbl;
          EXECUTE format('CREATE TRIGGER period_start BEFORE INSERT OR UPDATE ON %s FOR EACH ROW EXECUTE FUNCTION public.grif_period_start()', tbl);
          CALL public.grif_refresh_versioning(tbl);
          EXECUTE format('CREATE TRIGGER read_only BEFORE INSERT OR UPDATE OR DELETE OR TRUNCATE ON %I.%I FOR EACH STATEMENT EXECUTE FUNCTION public.grif_history_guard()', s, t || 'History');
        END $$;

        -- Versioning off, before a history table is dropped: the table's triggers, and its function.
        CREATE OR REPLACE PROCEDURE public.grif_disable_versioning(tbl regclass)
        LANGUAGE plpgsql AS $$
        DECLARE
          fn regprocedure;
        BEGIN
          SELECT tgfoid INTO fn FROM pg_trigger WHERE tgrelid = tbl AND tgname = 'versioning_update';
          EXECUTE format('DROP TRIGGER IF EXISTS period_start ON %s', tbl);
          EXECUTE format('DROP TRIGGER IF EXISTS versioning_update ON %s', tbl);
          EXECUTE format('DROP TRIGGER IF EXISTS versioning_delete ON %s', tbl);
          EXECUTE format('DROP TRIGGER IF EXISTS versioning_truncate ON %s', tbl);
          IF fn IS NOT NULL AND NOT EXISTS (SELECT FROM pg_trigger WHERE tgfoid = fn) THEN
            EXECUTE format('DROP FUNCTION %s', fn);
          END IF;
        END $$;
        """;

    private const string DropV1 = """
        DROP PROCEDURE public.grif_disable_versioning(regclass);
        DROP PROCEDURE public.grif_enable_versioning(regclass);
        DROP PROCEDURE public.grif_refresh_versioning(regclass);
        DROP FUNCTION public.grif_history_guard();
        DROP FUNCTION public.grif_period_start();
        """;
}

/// <summary>
/// A version of row history's functions: <see cref="Create"/> installs it, over nothing or over any other
/// version, and <see cref="Drop"/> takes it away. Both with \n line endings, whatever the checkout has.
/// </summary>
public sealed record HistoryFunctionsVersion(int Number, string Create, string Drop)
{
    public string Create { get; } = Create.ReplaceLineEndings("\n");
    public string Drop { get; } = Drop.ReplaceLineEndings("\n");
}

/// <summary>
/// Npgsql's annotations, plus the model's <see cref="PostgresHistoryFunctions.Annotation"/>: so EF's own
/// differ sees a change of version as a change to the database (an AlterDatabase), and has-pending-model-changes
/// and the app's Migrate find a version the snapshot doesn't have yet.
/// </summary>
#pragma warning disable EF1001 // Npgsql's annotation provider, which this extends.
public class PostgresHistoryAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : NpgsqlAnnotationProvider(dependencies)
{
    public override IEnumerable<IAnnotation> For(IRelationalModel model, bool designTime)
    {
        var annotations = base.For(model, designTime);
        return designTime && model.Model.FindAnnotation(PostgresHistoryFunctions.Annotation) is { } version
            ? annotations.Append(version)
            : annotations;
    }
}
