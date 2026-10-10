{{/*
PostgreSQL's side of the migration steps and the backup CronJobs (database.provider postgres). They run
in the backend image, with PostgreSQL's client tools (psql, pg_dump, pg_restore) and BusyBox. libpq
reads the connection from PGHOST, PGPORT, PGUSER and the password from PGPASSWORD (grif.connectionEnv),
so no password is ever on a command line. Values reach SQL as psql variables (-v name=value, \getenv),
quoted by psql (:'name' a literal, :"name" an identifier) or by format() (%L, %I) and run with \gexec;
the scripts go through stdin with a quoted here-document, so the shell expands nothing in them either.
Kubernetes expands $(NAME) in args: the SQL here has none.
*/}}

{{/*
After grif.waitForDatabaseScript (same deadline): until PostgreSQL answers a query. It refuses
connections while it starts, or recovers after an unclean stop. Needs grif.dbEnv.
*/}}
{{- define "grif.pgWaitForServerScript" -}}
server_state() { psql -X -A -t -q -d postgres -c 'SELECT 1' 2>&1; }
until state=$(server_state) && [ "$state" = 1 ]; do
  if [ "$(date +%s)" -ge "$end" ]; then echo "PostgreSQL not ready: $(echo "$state" | head -c 300)"; exit 1; fi
  sleep 2
done
echo "PostgreSQL at $DB_HOST:$DB_PORT is ready."
{{- end }}

{{/*
applied_migrations: the migration IDs the database has, one per line; NO_DATABASE if it doesn't exist.
Connects to the postgres database first, to ask whether the app's exists; a database without the
history table has none. A function, defined where the script's lines start: its here-document ends
at the start of a line.
*/}}
{{- define "grif.pgAppliedMigrationsFunction" -}}
applied_migrations() {
  psql -X -A -t -q -v ON_ERROR_STOP=1 -d postgres -v db="$DB_NAME" <<'SQL'
SELECT NOT EXISTS (SELECT FROM pg_database WHERE datname = :'db') AS missing \gset
\if :missing
\echo NO_DATABASE
\else
\connect :"db"
SELECT to_regclass('public."__EFMigrationsHistory"') IS NOT NULL AS history \gset
\if :history
SELECT "MigrationId" FROM public."__EFMigrationsHistory";
\endif
\endif
SQL
}
{{- end }}

{{/*
backup_files <folder>: the chart's own backups of DB_NAME in it, oldest first: pg_dump archives (they
start PGDMP) named <database>_YYYYMMDD_HHMMSS.dump, which sort by time. Anything else - hand-named,
half-written (.partial), not a pg_dump archive - is never listed.
*/}}
{{- define "grif.pgBackupFilesFunction" -}}
backup_files() {
  ls -1 "$1" | awk -v prefix="${DB_NAME}_" 'index($0, prefix) == 1 && substr($0, length(prefix) + 1) ~ /^[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]_[0-9][0-9][0-9][0-9][0-9][0-9]\.dump$/' \
    | sort | while read -r name; do
      if [ -f "$1/$name" ] && [ "$(head -c 5 "$1/$name")" = PGDMP ]; then echo "$name"; fi
    done
}
{{- end }}

{{/*
backup_database: pg_dump's custom format to BACKUP_DIR/<database>_<date>_<time>.dump. Written as
.partial first (a name restore and retention never take) and moved into place only once pg_restore
has read it back.
*/}}
{{- define "grif.pgBackupFunction" -}}
backup_database() {
  mkdir -p "$BACKUP_DIR"
  file="$BACKUP_DIR/${DB_NAME}_$(date +%Y%m%d_%H%M%S).dump"
  echo "Backing up $DB_NAME to $file"
  trap 'rm -f "$file.partial"' EXIT
  pg_dump -Fc -d "$DB_NAME" -f "$file.partial"
  pg_restore --list "$file.partial" > /dev/null
  mv "$file.partial" "$file"
  trap - EXIT
  echo "Backed up and verified: $file"
}
{{- end }}

{{/*
The restore step (migrations.restore): on a first deploy, the newest backup in RESTORE_DIR into a new
database. Restored under a temporary name and renamed once complete, so a restore that fails part way
leaves no database for the next run to take as the real one. No owners or privileges from the source:
everything belongs to the administrator, and the login sync grants the app's rights afterwards.
*/}}
{{- define "grif.pgRestoreScript" -}}
if [ -e /work/db-exists ]; then echo "Database $DB_NAME exists: nothing to restore."; exit 0; fi
{{ include "grif.pgBackupFilesFunction" . }}
[ -d "$RESTORE_DIR" ] || { echo "No folder $RESTORE_DIR to restore from."; exit 1; }
newest=$(backup_files "$RESTORE_DIR" | tail -n 1)
[ -n "$newest" ] || { echo "No ${DB_NAME}_<date>_<time>.dump in $RESTORE_DIR to restore."; exit 1; }
file="$RESTORE_DIR/$newest"
# A dump of a database with row history creates the periods extension, and its tables' triggers call it.
if pg_restore --list "$file" | grep -Eq ' EXTENSION - periods( |$)' \
   && [ "$(psql -X -A -t -q -d postgres -c "SELECT count(*) FROM pg_available_extensions WHERE name = 'periods'")" != 1 ]; then
  echo "$file has row history (the periods extension), which this PostgreSQL hasn't got: it can't be restored here."
  echo "Restore it with database.history.enabled (the chart's PostgreSQL then runs postgres.historyImage); turned off"
  echo "again afterwards, the history is removed and kept aside (README, Row history)."
  exit 1
fi
echo "Restoring $file"
tmp="${DB_NAME}_restoring"
psql -X -q -v ON_ERROR_STOP=1 -d postgres -v db="$DB_NAME" -v tmp="$tmp" <<'SQL'
SET client_min_messages = warning;
SELECT format('DROP DATABASE IF EXISTS %I', :'tmp') \gexec
SELECT format('CREATE DATABASE %I', :'tmp') \gexec
SQL
pg_restore --no-owner --no-privileges --exit-on-error -d "$tmp" "$file"
psql -X -q -v ON_ERROR_STOP=1 -d postgres -v db="$DB_NAME" -v tmp="$tmp" <<'SQL'
SELECT format('ALTER DATABASE %I RENAME TO %I', :'tmp', :'db') \gexec
SQL
{{- end }}

{{/*
database.history: row history, as the administrator (a superuser: periods isn't a trusted extension),
after the migrations and before the logins, on every run. The periods extension; its functions closed
to PUBLIC (they run as their owner and check nothing: any login could turn a table's history off, or
purge it); the history objects read-only again where a restore left them writable; and SYSTEM
VERSIONING on every table with the period's columns (PeriodStart, PeriodEnd: the app gives them to the
tables SQL Server keeps history of, every one but MatchReschedules) that hasn't it yet - the tables of a
database from before history was on. The app's own migrations version the tables they create with
those columns or add them to (RowHistory, RowHistoryMigrationsSqlGenerator). Never EF's migration
history, anything an extension owns, nor the history tables kept when history was turned off (schema
row_history_off, grif.pgHistoryOffSql). One transaction: a failure leaves no table half versioned.

The history objects: periods gives each history table and view only SELECT (to the grantees of
SELECT on its table), each history function only EXECUTE (the same grantees), and from then on refuses
any GRANT in the database while one of them has anything more. A dump restored without privileges
(pg_restore --no-privileges, as migrations.restore does: with them, periods refuses the restore's own
grants) leaves them all on their owner's defaults, writes included, and the functions executable by
PUBLIC; versioning a table, or the login sync's grants, then fail. So whatever they have beyond what
periods gives is revoked - nothing on a database periods set up itself, where this is a no-op.
*/}}
{{- define "grif.pgHistoryScript" -}}
# Row history (database.history).
psql -X -q -v ON_ERROR_STOP=1 -d "$DB_NAME" <<'SQL'
SET client_min_messages = warning;
BEGIN;
CREATE EXTENSION IF NOT EXISTS periods CASCADE;
REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA periods FROM PUBLIC;
-- History tables and views: SELECT only. History functions: EXECUTE only for SELECT's grantees on the table.
SELECT format('REVOKE %s ON TABLE %s FROM %s', string_agg(DISTINCT acl.privilege_type, ', '),
              string_agg(DISTINCT c.oid::regclass::text, ', '), CASE acl.grantee WHEN 0 THEN 'PUBLIC' ELSE acl.grantee::regrole::text END)
  FROM periods.system_versioning AS sv
  JOIN pg_class AS c ON c.oid IN (sv.history_table_name, sv.view_name)
  CROSS JOIN LATERAL aclexplode(COALESCE(c.relacl, acldefault('r', c.relowner))) AS acl
 WHERE acl.privilege_type <> 'SELECT'
 GROUP BY acl.grantee \gexec
SELECT format('REVOKE EXECUTE ON FUNCTION %s FROM %s', string_agg(DISTINCT p.oid::regprocedure::text, ', '),
              CASE acl.grantee WHEN 0 THEN 'PUBLIC' ELSE acl.grantee::regrole::text END)
  FROM periods.system_versioning AS sv
  JOIN pg_class AS t ON t.oid = sv.table_name
  JOIN pg_proc AS p ON p.oid = ANY (ARRAY[sv.func_as_of, sv.func_between, sv.func_between_symmetric, sv.func_from_to]::regprocedure[])
  CROSS JOIN LATERAL aclexplode(COALESCE(p.proacl, acldefault('f', p.proowner))) AS acl
 WHERE NOT EXISTS (SELECT FROM aclexplode(COALESCE(t.relacl, acldefault('r', t.relowner))) AS b
                    WHERE b.grantee = acl.grantee AND b.privilege_type = 'SELECT')
 GROUP BY acl.grantee \gexec
-- Not printed: the calls' results, two per table.
\o /dev/null
SELECT format('SELECT periods.add_system_time_period(%L, %L, %L)', c.oid::regclass, 'PeriodStart', 'PeriodEnd'),
       format('SELECT periods.add_system_versioning(%L)', c.oid::regclass)
  FROM pg_class AS c JOIN pg_namespace AS n ON n.oid = c.relnamespace
 WHERE c.relkind = 'r' AND n.nspname NOT LIKE 'pg\_%' AND n.nspname NOT IN ('information_schema', 'periods', 'row_history_off')
   AND (n.nspname, c.relname) <> ('public', '__EFMigrationsHistory')
   AND c.oid NOT IN (SELECT table_name FROM periods.system_versioning UNION ALL SELECT history_table_name FROM periods.system_versioning)
   AND NOT EXISTS (SELECT FROM pg_depend AS d WHERE d.classid = 'pg_class'::regclass AND d.objid = c.oid AND d.deptype = 'e')
   -- The app's tables with the period's columns: those temporal on SQL Server.
   AND EXISTS (SELECT FROM pg_attribute AS a WHERE a.attrelid = c.oid AND a.attname = 'PeriodStart' AND NOT a.attisdropped)
   AND EXISTS (SELECT FROM pg_attribute AS a WHERE a.attrelid = c.oid AND a.attname = 'PeriodEnd' AND NOT a.attisdropped)
 ORDER BY n.nspname, c.relname \gexec
\o
COMMIT;
SELECT count(*) AS versioned FROM periods.system_versioning \gset
\echo Row history: :versioned tables versioned in :DBNAME.
SQL
{{- end }}

{{/*
Row history off (database.history.enabled false): the SQL that takes the periods extension out of the
database psql is on, if it's there; nothing otherwise. Run by the chart's PostgreSQL before it serves
(grif.pgHistoryOffStartScript) and by the migration Job (an external server). Needed because periods'
row triggers are C functions in periods.so: on a server without it (the chart's postgres.image), every
INSERT, UPDATE and DELETE on a versioned table fails ("could not access file "periods"") while reads,
DDL and TRUNCATE still work. Removing needs nothing from periods.so: drop_system_versioning and
drop_period are PL/pgSQL, and dropping a C function doesn't load its library.

Each versioned table: versioning off (its view and functions dropped), its period dropped (triggers, its
CHECK constraints), leaving its columns and defaults as on a database that never had
history. Its history table is kept, moved to the schema row_history_off as "<schema>.<table>_history
<UTC time>" (no name of the app's, so the history step never versions it nor takes it back): readable
by the administrator, not by the app's login, dropped by hand when no longer wanted. Turning history on
again starts each table's history afresh. Then the extension, which nothing else depends on (btree_gist,
which it brought, stays). One transaction: all of it, or nothing.
*/}}
{{- define "grif.pgHistoryOffSql" -}}
SET client_min_messages = warning;
SELECT EXISTS (SELECT FROM pg_extension WHERE extname = 'periods') AS has_periods \gset
\if :has_periods
BEGIN;
CREATE SCHEMA IF NOT EXISTS row_history_off;
SELECT count(*) AS versioned FROM periods.system_versioning \gset
-- Not printed: the calls' results.
\o /dev/null
SELECT format('SELECT periods.drop_system_versioning(%L)', sv.table_name),
       format('SELECT periods.drop_period(%L, %L, purge => true)', sv.table_name, 'system_time'),
       format('ALTER TABLE %s RENAME TO %I', sv.history_table_name, k.name),
       format('ALTER TABLE %I.%I SET SCHEMA row_history_off', n.nspname, k.name)
  FROM periods.system_versioning AS sv
  JOIN pg_class AS h ON h.oid = sv.history_table_name
  JOIN pg_namespace AS n ON n.oid = h.relnamespace
  CROSS JOIN LATERAL (SELECT left(format('%s.%s %s', n.nspname, h.relname,
                                         to_char(transaction_timestamp() AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS')), 63) AS name) AS k
 ORDER BY n.nspname, h.relname \gexec
-- Any other period (the chart makes none).
SELECT format('SELECT periods.drop_period(%L, %L, purge => true)', table_name, period_name) FROM periods.periods \gexec
\o
DROP EXTENSION periods;
COMMIT;
\echo Row history off in :DBNAME: periods removed, :versioned history tables kept in schema row_history_off.
\endif
{{- end }}

{{/*
The migration Job with history off, after the migrations (the database exists then): for an external
server, which keeps periods.so. The chart's own PostgreSQL has done it already as it started.
*/}}
{{- define "grif.pgHistoryOffScript" -}}
# Row history off (database.history.enabled false): the periods extension out of the database, if it's there.
psql -X -q -v ON_ERROR_STOP=1 -d "$DB_NAME" <<'SQL'
{{ include "grif.pgHistoryOffSql" . }}
SQL
{{- end }}

{{/*
The chart's PostgreSQL with history off: before the server starts, the extension out of every database
that has it (grif.pgHistoryOffSql), on a server of its own on a socket in /tmp that nothing else can
reach. So the image the chart switches to never serves a database whose writes would fail on the
missing periods.so, however a helm upgrade orders the pod and the migration Job, and whether or not the
Job runs. A second or two on a database without the extension; nothing on a new volume.
*/}}
{{- define "grif.pgHistoryOffStartScript" -}}
set -eu
if [ ! -s "$PGDATA/PG_VERSION" ]; then echo "No database yet: no row history to remove."; exit 0; fi
echo "local all all trust" > /tmp/history-off-hba.conf
if ! pg_ctl -D "$PGDATA" -w -t 600 -l /tmp/history-off.log \
     -o "-c listen_addresses='' -c unix_socket_directories=/tmp -c hba_file=/tmp/history-off-hba.conf" start > /dev/null; then
  cat /tmp/history-off.log; exit 1
fi
trap 'pg_ctl -D "$PGDATA" -w -m fast stop > /dev/null; rm -f /tmp/history-off-hba.conf /tmp/history-off-databases' EXIT
export PGHOST=/tmp PGUSER="${POSTGRES_USER:-postgres}"
psql -X -A -t -q -v ON_ERROR_STOP=1 -d postgres \
  -c "SELECT datname FROM pg_database WHERE datallowconn AND NOT datistemplate ORDER BY datname" > /tmp/history-off-databases
while IFS= read -r db; do
  PGDATABASE="$db" psql -X -q -v ON_ERROR_STOP=1 <<'SQL'
{{ include "grif.pgHistoryOffSql" . }}
SQL
done < /tmp/history-off-databases
echo "Row history: none on this server."
{{- end }}

{{/*
database.logins: create the roles or reset their passwords, and grant exactly their rights, as the
administrator, after the migrations (the database and its tables exist by then; the next run grants
on tables a later migration adds). psql reads the names and passwords from the environment itself
(\getenv). The app: CONNECT, USAGE on every schema but PostgreSQL's own and row_history_off (the history kept
aside when history was turned off: the administrator's), read and write on every table,
its sequences; no CREATE anywhere and owner of nothing, so it can't change the schema. Monitoring:
pg_monitor (statistics and settings), no table. With database.history, the app's go table by table, not to
the history tables and views: periods refuses any grant on them, and gives SELECT with their table's.
*/}}
{{- define "grif.pgLoginSyncScript" -}}
# Logins (database.logins).
psql -X -q -v ON_ERROR_STOP=1 -d "$DB_NAME" <<'SQL'
SET client_min_messages = warning;
\getenv app_user APP_USER
\getenv app_password APP_PASSWORD
SELECT format('CREATE ROLE %I', :'app_user') WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'app_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'app_user', :'app_password') \gexec
SELECT format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), :'app_user') \gexec
-- Before PostgreSQL 15 anyone could create tables in public.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
{{- if include "grif.history" . }}
-- Table by table, not ALL TABLES IN SCHEMA: periods refuses any grant on a history table or view,
-- which get SELECT from their table's. Nothing in periods' own schema.
SELECT format('GRANT USAGE ON SCHEMA %I TO %I', nspname, :'app_user'),
       format('GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA %I TO %I', nspname, :'app_user')
  FROM pg_namespace WHERE nspname NOT LIKE 'pg\_%' AND nspname NOT IN ('information_schema', 'periods', 'row_history_off') ORDER BY nspname \gexec
SELECT format('GRANT SELECT, INSERT, UPDATE, DELETE ON %s TO %I', c.oid::regclass, :'app_user')
  FROM pg_class AS c JOIN pg_namespace AS n ON n.oid = c.relnamespace
 WHERE c.relkind IN ('r', 'p', 'v', 'm', 'f') AND n.nspname NOT LIKE 'pg\_%' AND n.nspname NOT IN ('information_schema', 'periods', 'row_history_off')
   AND c.oid NOT IN (SELECT history_table_name FROM periods.system_versioning UNION ALL SELECT view_name FROM periods.system_versioning)
 ORDER BY n.nspname, c.relname \gexec
\echo Login :app_user: reads and writes :DBNAME, and reads its history.
{{- else }}
SELECT format('GRANT USAGE ON SCHEMA %I TO %I', nspname, :'app_user'),
       format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO %I', nspname, :'app_user'),
       format('GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA %I TO %I', nspname, :'app_user')
  FROM pg_namespace WHERE nspname NOT LIKE 'pg\_%' AND nspname NOT IN ('information_schema', 'row_history_off') ORDER BY nspname \gexec
\echo Login :app_user: reads and writes :DBNAME.
{{- end }}
\getenv mon_user MON_USER
\if :{?mon_user}
\getenv mon_password MON_PASSWORD
SELECT format('CREATE ROLE %I', :'mon_user') WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'mon_user') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'mon_user', :'mon_password') \gexec
SELECT format('GRANT pg_monitor TO %I', :'mon_user') \gexec
\echo Login :mon_user: reads server statistics (pg_monitor).
\endif
SQL
{{- end }}

{{/* The retention CronJob's script (migrations.backup.retention), for PostgreSQL's backup volume. */}}
{{- define "grif.pgRetentionScript" -}}
{{ include "grif.pgBackupFilesFunction" . }}
if [ ! -d "$BACKUP_DIR" ]; then echo "No backups in $BACKUP_DIR yet."; exit 0; fi
# Older than this, by the date and time in the name (the same clock and time zone that named it).
cutoff=$(date -d "@$(( $(date +%s) - KEEP_DAYS * 86400 ))" +%Y%m%d_%H%M%S)
old=$(backup_files "$BACKUP_DIR" | sort -r | awk -v keep="$KEEP_LAST" -v cutoff="$cutoff" \
  'NR > keep && substr($0, length($0) - 19, 15) < cutoff')
n=0
for name in $old; do
  rm -f "$BACKUP_DIR/$name"
  echo "Deleted $BACKUP_DIR/$name"
  n=$((n + 1))
done
echo "Deleted $n backup(s); kept the newest $KEEP_LAST and any from the last $KEEP_DAYS days."
{{- end }}

{{/* The backup volume (migrations.backup.volume), for the pods pg_dump and retention run in. */}}
{{- define "grif.pgBackupVolume" -}}
- name: backup
  persistentVolumeClaim:
    claimName: {{ .Values.migrations.backup.volume.existingClaim | default .Values.migrations.backup.volume.claimName }}
{{- end }}

{{/*
The pod security context of the Job and the CronJobs: migrations.podSecurityContext, and with
PostgreSQL's backup volume mounted, fsGroup (its runAsGroup) so a provisioned volume is writable.
*/}}
{{- define "grif.migrationPodSecurityContext" -}}
{{- $sc := deepCopy (.Values.migrations.podSecurityContext | default dict) -}}
{{- if and (include "grif.postgres" .) .Values.migrations.backup.enabled (not (hasKey $sc "fsGroup")) -}}
{{- $_ := set $sc "fsGroup" ($sc.runAsGroup | default 1654) -}}
{{- $_ := set $sc "fsGroupChangePolicy" "OnRootMismatch" -}}
{{- end -}}
{{- with $sc }}{{ toYaml . }}{{ end -}}
{{- end }}
