{{/*
Migrations (values.yaml, migrations): one Job changes the schema (templates/migrations-job.yaml); the
backend pod only waits until it's done. Every step runs in the backend image being deployed, which
carries the migrations bundle (/app/efbundle), its list (/app/migrations.txt) and sqlcmd (go-sqlcmd).
Steps share /work (the migrations-work volume). sqlcmd takes the password from SQLCMDPASSWORD, never
the command line, and values as -v scripting variables, written $$(NAME) so Kubernetes passes them on
as they are. PostgreSQL's own scripts (database.provider postgres) are in _postgres.tpl.
*/}}

{{/* The migration IDs the database has, into /work/applied.txt; NO_DATABASE if it doesn't exist. */}}
{{- define "grif.appliedMigrationsQuery" -}}
sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -h -1 -W -v DB_NAME="$DB_NAME" -Q "SET NOCOUNT ON;
    IF DB_ID(N'\$$(DB_NAME)') IS NULL SELECT 'NO_DATABASE'
    ELSE IF OBJECT_ID(N'[\$$(DB_NAME)].dbo.__EFMigrationsHistory') IS NOT NULL
      EXEC(N'SELECT MigrationId FROM [\$$(DB_NAME)].dbo.__EFMigrationsHistory');" \
  > /work/applied.txt
{{- end }}

{{/* The same for either provider, after grif.migrationQueryFunctions; it fails as the query does. */}}
{{- define "grif.readAppliedMigrations" -}}
{{- if include "grif.postgres" . -}}
applied_migrations > /work/applied.txt
{{- else -}}
{{- include "grif.appliedMigrationsQuery" . -}}
{{- end -}}
{{- end }}

{{/* What grif.readAppliedMigrations needs defined first, where a line starts: PostgreSQL's function. */}}
{{- define "grif.migrationQueryFunctions" -}}
{{- if include "grif.postgres" . -}}
{{- include "grif.pgAppliedMigrationsFunction" . -}}
{{- end -}}
{{- end }}

{{/* The image's migration list, the provider's bundle's. */}}
{{- define "grif.migrationList" -}}
{{- include "grif.byProvider" (dict "root" . "sql" "/app/migrations.txt" "pg" "/app/postgres/migrations.txt") -}}
{{- end }}

{{/*
The image's migrations the database lacks (/work/applied.txt), one per line. awk, not grep -vxF -f: BusyBox
grep (Alpine, the backend image) matches every line against an empty pattern file, so a database with
no migrations would look up to date.
*/}}
{{- define "grif.pendingMigrations" -}}
awk 'FILENAME == ARGV[1] { if ($0 != "") applied[$0]; next } !($0 in applied)' /work/applied.txt {{ include "grif.migrationList" . }}
{{- end }}

{{- define "grif.migrationSmallResources" -}}
requests:
  cpu: 10m
  memory: 32Mi
limits:
  memory: 128Mi
{{- end }}

{{/* A step's container: the backend image, its pull policy, the migrations' security context. */}}
{{- define "grif.migrationStepImage" -}}
image: {{ include "grif.backendImage" . }}
imagePullPolicy: {{ .Values.backend.image.pullPolicy }}
{{- with .Values.migrations.securityContext }}
securityContext:
  {{- toYaml . | nindent 2 }}
{{- end }}
{{- end }}

{{/*
The backend pod's init container: wait until the database has every migration in the image, without
changing anything. The app starts as soon as the Job is done, rather than crashing and backing off.
*/}}
{{- define "grif.backendMigrationWait" -}}
{{- $m := .Values.migrations -}}
- name: wait-for-migrations
  {{- include "grif.migrationStepImage" . | nindent 2 }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -u
      {{- with include "grif.migrationQueryFunctions" . }}
      {{- . | nindent 6 }}
      {{- end }}
      last=""
      while :; do
        {{- if include "grif.postgres" . }}
        if {{ include "grif.readAppliedMigrations" . }} 2>/work/error.txt; then
        {{- else }}
        if ( {{- include "grif.appliedMigrationsQuery" . | nindent 10 }}
           ) 2>/work/error.txt; then
        {{- end }}
          if grep -qxF NO_DATABASE /work/applied.txt; then
            state="Waiting for the migration Job to create database $DB_NAME."
          else
            {{ include "grif.pendingMigrations" . }} > /work/pending.txt
            if [ ! -s /work/pending.txt ]; then echo "Database $DB_NAME is up to date."; exit 0; fi
            state="Waiting for the migration Job to apply: $(tr '\n' ' ' < /work/pending.txt)"
          fi
        else
          state="Waiting for the database: $(cat /work/error.txt /work/applied.txt 2>/dev/null | head -c 300 | tr '\n' ' ')"
        fi
        [ "$state" = "$last" ] || echo "$state"
        last=$state
        sleep 5
      done
  env:
    {{- include "grif.appDbEnv" . | nindent 4 }}
    {{- with $m.extraEnv }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  volumeMounts:
    - name: migrations-work
      mountPath: /work
  resources:
    {{- include "grif.migrationSmallResources" . | nindent 4 }}
{{- end }}

{{/* The Job's init containers: wait, check, restore, back up, your own steps. Its container migrates. */}}
{{- define "grif.migrationJobSteps" -}}
{{- $m := .Values.migrations -}}
{{- $pg := include "grif.postgres" . -}}
- name: wait-for-database
  {{- include "grif.migrationStepImage" . | nindent 2 }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -eu
      {{- include "grif.waitForDatabaseScript" . | nindent 6 }}
      {{- include "grif.waitForDatabaseOnlineScript" . | nindent 6 }}
  env:
    {{- include "grif.dbEnv" . | nindent 4 }}
    - name: WAIT_SECONDS
      value: {{ $m.databaseWaitSeconds | quote }}
    {{- with $m.extraEnv }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  resources:
    {{- include "grif.migrationSmallResources" . | nindent 4 }}
# Which of the image's migrations the database lacks (/work/pending.txt: all of them if it doesn't
# exist); /work/db-exists if it does.
- name: check-pending
  {{- include "grif.migrationStepImage" . | nindent 2 }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -eu
      {{- if $m.scripts.checkPending }}
      {{- $m.scripts.checkPending | nindent 6 }}
      {{- else }}
      {{- with include "grif.migrationQueryFunctions" . }}
      {{- . | nindent 6 }}
      {{- end }}
      {{- include "grif.readAppliedMigrations" . | nindent 6 }} || { echo "Reading $DB_NAME's migrations failed:"; cat /work/applied.txt; exit 1; }
      if grep -qxF NO_DATABASE /work/applied.txt; then
        echo "Database $DB_NAME doesn't exist: the migration creates it."
        : > /work/applied.txt
      else
        touch /work/db-exists
      fi
      {{ include "grif.pendingMigrations" . }} > /work/pending.txt
      if [ -s /work/pending.txt ]; then
        echo "Pending migrations:"; cat /work/pending.txt
      else
        echo "Up to date."
      fi
      {{- end }}
  env:
    {{- include "grif.dbEnv" . | nindent 4 }}
    {{- with $m.extraEnv }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  volumeMounts:
    - name: migrations-work
      mountPath: /work
  resources:
    {{- include "grif.migrationSmallResources" . | nindent 4 }}
{{- if $m.restore.enabled }}
# Only a database that doesn't exist yet (a first deploy): the newest backup in the restore folder,
# then which migrations it lacks, so migrate applies only those. db-exists stays absent, so the backup
# step doesn't back up what was just restored; /work/restored names the file.
- name: restore
  {{- include "grif.migrationStepImage" . | nindent 2 }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -eu
      {{- if $m.scripts.restore }}
      {{- $m.scripts.restore | nindent 6 }}
      {{- else }}
      {{- if $pg }}
      {{- include "grif.pgRestoreScript" . | nindent 6 }}
      {{- else }}
      {{- include "grif.mssqlRestoreScript" . | nindent 6 }}
      {{- end }}
      {{- with include "grif.migrationQueryFunctions" . }}
      {{- . | nindent 6 }}
      {{- end }}
      {{- include "grif.readAppliedMigrations" . | nindent 6 }} || { echo "Reading $DB_NAME's migrations failed:"; cat /work/applied.txt; exit 1; }
      {{ include "grif.pendingMigrations" . }} > /work/pending.txt
      echo "$file" > /work/restored
      if [ -s /work/pending.txt ]; then
        echo "Restored $file. Pending migrations:"; cat /work/pending.txt
      else
        echo "Restored $file. Up to date."
      fi
      {{- end }}
  env:
    {{- include "grif.dbEnv" . | nindent 4 }}
    - name: RESTORE_DIR
      value: {{ $m.restore.directory | default (include "grif.byProvider" (dict "root" . "sql" "/var/opt/mssql/restore" "pg" "/restore")) | quote }}
    {{- with $m.extraEnv }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  volumeMounts:
    - name: migrations-work
      mountPath: /work
    {{- if $pg }}
    - name: restore
      mountPath: /restore
      readOnly: true
    {{- end }}
  resources:
    {{- include "grif.migrationSmallResources" . | nindent 4 }}
{{- end }}
{{- if $m.backup.enabled }}
{{- if $pg }}
# Only an existing database with pending migrations. pg_dump writes the file, to the backup volume.
{{- else }}
# Only an existing database with pending migrations. SQL Server writes the file, so the folder is on
# its side.
{{- end }}
- name: backup
  {{- include "grif.migrationStepImage" . | nindent 2 }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -eu
      {{- if $m.scripts.backup }}
      {{- $m.scripts.backup | nindent 6 }}
      {{- else }}
      if [ ! -e /work/db-exists ]; then echo "No database to back up."; exit 0; fi
      if [ ! -s /work/pending.txt ]; then echo "Nothing to migrate: no backup needed."; exit 0; fi
      {{- if $pg }}
      {{- include "grif.pgBackupFunction" . | nindent 6 }}
      backup_database
      {{- else }}
      file="$BACKUP_DIR/${DB_NAME}_$(date +%Y%m%d_%H%M%S).bak"
      echo "Backing up $DB_NAME to $file"
      sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b \
        -v BACKUP_DIR="$BACKUP_DIR" BACKUP_FILE="$file" DB_NAME="$DB_NAME" \
        -Q "EXEC master.sys.xp_create_subdir N'\$$(BACKUP_DIR)';
          BACKUP DATABASE [\$$(DB_NAME)] TO DISK = N'\$$(BACKUP_FILE)' WITH FORMAT, INIT, NAME = 'Pre-migration backup';"
      {{- end }}
      {{- end }}
  env:
    {{- include "grif.dbEnv" . | nindent 4 }}
    - name: BACKUP_DIR
      value: {{ include "grif.backupDir" . | quote }}
    {{- with $m.extraEnv }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  volumeMounts:
    - name: migrations-work
      mountPath: /work
    {{- if $pg }}
    - name: backup
      mountPath: /backup
    {{- end }}
  resources:
    {{- include "grif.migrationSmallResources" . | nindent 4 }}
{{- end }}
{{- with $m.extraInitContainers }}
{{ tpl (toYaml .) $ }}
{{- end }}
{{- end }}

{{- define "grif.migrateContainer" -}}
{{- $m := .Values.migrations -}}
- name: migrate
  {{- include "grif.migrationStepImage" . | nindent 2 }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -eu
      {{- if $m.scripts.migrate }}
      {{- $m.scripts.migrate | nindent 6 }}
      {{- else }}
      if [ -s /work/pending.txt ]; then {{ include "grif.byProvider" (dict "root" . "sql" "/app/efbundle" "pg" "/app/postgres/efbundle") }}; else echo "Nothing to migrate."; fi
      {{- end }}
      {{- if include "grif.history" . }}
      {{- include "grif.pgHistoryScript" . | nindent 6 }}
      {{- end }}
      {{- if .Values.database.logins.enabled }}
      {{- if include "grif.postgres" . }}
      {{- include "grif.pgLoginSyncScript" . | nindent 6 }}
      {{- else }}
      {{- include "grif.loginSyncScript" . | nindent 6 }}
      {{- end }}
      {{- end }}
  env:
    {{- include "grif.migrationEnv" . | nindent 4 }}
    {{- if or .Values.database.logins.enabled (include "grif.history" .) }}
    {{- include "grif.dbEnv" . | nindent 4 }}
    {{- end }}
    {{- if .Values.database.logins.enabled }}
    - name: APP_USER
      value: {{ .Values.database.logins.app.user | quote }}
    {{- include "grif.appPasswordEnv" (dict "root" . "name" "APP_PASSWORD") | nindent 4 }}
    {{- if include "grif.monitoringLogin" . }}
    - name: MON_USER
      value: {{ .Values.database.logins.monitoring.user | quote }}
    - name: MON_PASSWORD
      valueFrom:
        secretKeyRef:
          name: {{ include "grif.secretName" (dict "secretName" .Values.database.logins.monitoring.password.secretName "root" .) }}
          key: {{ .Values.database.logins.monitoring.password.key }}
    {{- end }}
    {{- end }}
    {{- with $m.extraEnv }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  volumeMounts:
    - name: migrations-work
      mountPath: /work
    - name: tmp
      mountPath: /tmp
  {{- with $m.resources }}
  resources:
    {{- toYaml . | nindent 4 }}
  {{- end }}
{{- end }}

{{/*
database.logins: create the logins or reset their passwords, and grant exactly their rights, as the
administrator, after the migrations (the database exists by then). The SQL goes through a file in the
Job's scratch volume with its quotes escaped, run with -x (no variable substitution), so no password
is on a command line or expanded again. SQL Server's password policy applies.
*/}}
{{- define "grif.loginSyncScript" -}}
# Logins (database.logins).
lit() { printf %s "$1" | sed "s/'/''/g"; }
ident() { printf %s "$1" | sed 's/]/]]/g'; }
login() {
  printf '%s\n' "IF SUSER_ID(N'$(lit "$1")') IS NULL CREATE LOGIN [$(ident "$1")] WITH PASSWORD = N'$(lit "$2")';"
  printf '%s\n' "ELSE ALTER LOGIN [$(ident "$1")] WITH PASSWORD = N'$(lit "$2")';"
}
umask 077
trap 'rm -f /work/logins.sql' EXIT
{
  printf '%s\n' "SET NOCOUNT ON;"
  login "$APP_USER" "$APP_PASSWORD"
  printf '%s\n' "USE [$(ident "$DB_NAME")];"
  printf '%s\n' "IF USER_ID(N'$(lit "$APP_USER")') IS NULL CREATE USER [$(ident "$APP_USER")] FOR LOGIN [$(ident "$APP_USER")];"
  {{- if .Values.migrations.restore.enabled }}
  # A restored database's user may be another server's login of the same name (orphaned): this one's.
  printf '%s\n' "ELSE ALTER USER [$(ident "$APP_USER")] WITH LOGIN = [$(ident "$APP_USER")];"
  {{- end }}
  printf '%s\n' "ALTER ROLE db_datareader ADD MEMBER [$(ident "$APP_USER")];"
  printf '%s\n' "ALTER ROLE db_datawriter ADD MEMBER [$(ident "$APP_USER")];"
  printf '%s\n' "PRINT N'Login $(lit "$APP_USER"): reads and writes $(lit "$DB_NAME").';"
  if [ -n "${MON_USER:-}" ]; then
    printf '%s\n' "USE [master];"
    login "$MON_USER" "$MON_PASSWORD"
    printf '%s\n' "GRANT VIEW SERVER STATE TO [$(ident "$MON_USER")];"
    printf '%s\n' "GRANT VIEW ANY DEFINITION TO [$(ident "$MON_USER")];"
    printf '%s\n' "PRINT N'Login $(lit "$MON_USER"): views server state.';"
  fi
} > /work/logins.sql
sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -x -i /work/logins.sql
{{- end }}

{{/*
SQL Server's restore step (migrations.restore): on a first deploy, the newest of the chart's backups in
RESTORE_DIR, a folder on SQL Server's side (it reads the file), each of the backup's files moved into
this server's default data or log folder under its own name. A restore that fails part way leaves the
database RESTORING; it's dropped, so the next run restores again. The statement goes through a file
with its quotes escaped, run with -x, as the logins' do.
*/}}
{{- define "grif.mssqlRestoreScript" -}}
if [ -e /work/db-exists ]; then echo "Database $DB_NAME exists: nothing to restore."; exit 0; fi
# Functions, not sqlcmd inline in $(...) (grif.waitForDatabaseOnlineScript says why).
# The chart's own names only (<database>_YYYYMMDD_HHMMSS.bak), which sort by time; hand-named ones in
# the folder are ignored.
newest() {
  sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -h -1 -W -v RESTORE_DIR="$RESTORE_DIR" DB_NAME="$DB_NAME" -Q "SET NOCOUNT ON;
    SELECT TOP 1 full_filesystem_path FROM sys.dm_os_enumerate_filesystem(N'\$$(RESTORE_DIR)', N'\$$(DB_NAME)_*.bak')
    WHERE is_directory = 0
      AND LEN(file_or_directory_name) = LEN(N'\$$(DB_NAME)') + 20
      AND LEFT(file_or_directory_name, LEN(N'\$$(DB_NAME)') + 1) = N'\$$(DB_NAME)_'
      AND RIGHT(file_or_directory_name, 19) LIKE N'[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][_][0-9][0-9][0-9][0-9][0-9][0-9].bak'
    ORDER BY file_or_directory_name DESC"
}
# Where this server puts a new database's files: data|log.
folders() {
  sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -h -1 -W -s '|' -Q "SET NOCOUNT ON;
    SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath')), CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath'))"
}
filelist() {
  sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -h -1 -W -s '|' -v FILE="$1" -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'\$$(FILE)'"
}
drop_restoring() {
  sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -v DB_NAME="$DB_NAME" -Q "SET NOCOUNT ON;
    IF CONVERT(nvarchar(60), DATABASEPROPERTYEX(N'\$$(DB_NAME)', 'Status')) = N'RESTORING' DROP DATABASE [\$$(DB_NAME)];"
}
lit() { printf %s "$1" | sed "s/'/''/g"; }
ident() { printf %s "$1" | sed 's/]/]]/g'; }
file=$(newest)
[ -n "$file" ] || { echo "No ${DB_NAME}_<date>_<time>.bak in $RESTORE_DIR to restore."; exit 1; }
echo "Restoring $file"
paths=$(folders)
# One MOVE per file in the backup (LogicalName|PhysicalName|Type|...): the log (type L) to the log
# folder, the rest to the data folder.
moves=$(filelist "$file" | awk -F'|' -v q="'" -v paths="$paths" '
  BEGIN { split(paths, f, "|") }
  NF > 2 {
    n = split($2, p, /[\\\/]/); dir = ($3 == "L") ? f[2] : f[1]
    if (dir !~ /[\\\/]$/) dir = dir "/"
    name = $1; gsub(q, q q, name); path = dir p[n]; gsub(q, q q, path)
    printf "%sMOVE N%s%s%s TO N%s%s%s", (c++ ? ", " : ""), q, name, q, q, path, q
  }')
[ -n "$moves" ] || { echo "No files listed in $file"; exit 1; }
printf '%s\n' "RESTORE DATABASE [$(ident "$DB_NAME")] FROM DISK = N'$(lit "$file")' WITH $moves, REPLACE, RECOVERY, STATS = 25;" > /work/restore.sql
if ! sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -x -i /work/restore.sql; then
  drop_restoring || true
  echo "Restoring $file failed."; exit 1
fi
{{- end }}
