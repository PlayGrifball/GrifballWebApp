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
database.logins: create the roles or reset their passwords, and grant exactly their rights, as the
administrator, after the migrations (the database and its tables exist by then; the next run grants
on tables a later migration adds). psql reads the names and passwords from the environment itself
(\getenv). The app: CONNECT, USAGE on every schema but PostgreSQL's own, read and write on every table,
its sequences; no CREATE anywhere and owner of nothing, so it can't change the schema. Monitoring:
pg_monitor (statistics and settings), no table.
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
SELECT format('GRANT USAGE ON SCHEMA %I TO %I', nspname, :'app_user'),
       format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO %I', nspname, :'app_user'),
       format('GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA %I TO %I', nspname, :'app_user')
  FROM pg_namespace WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema' ORDER BY nspname \gexec
\echo Login :app_user: reads and writes :DBNAME.
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
