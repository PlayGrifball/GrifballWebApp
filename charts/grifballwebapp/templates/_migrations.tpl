{{/*
Migrations (values.yaml, migrations): one Job changes the schema (templates/migrations-job.yaml); the
backend pod only waits until it's done. Steps share /work (the migrations-work volume).
sqlcmd reads environment variables as scripting variables (DB_NAME, BACKUP_DIR, BACKUP_FILE), written
with a doubled dollar sign so Kubernetes passes them on as they are.
*/}}

{{/* The migration IDs the database has, into /work/applied.txt; NO_DATABASE if it doesn't exist. */}}
{{- define "grif.appliedMigrationsQuery" -}}
/opt/mssql-tools/bin/sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -P "$DB_PASSWORD" -N -C -b -h -1 -W -Q "SET NOCOUNT ON;
    IF DB_ID(N'\$$(DB_NAME)') IS NULL SELECT 'NO_DATABASE'
    ELSE IF OBJECT_ID(N'[\$$(DB_NAME)].dbo.__EFMigrationsHistory') IS NOT NULL
      EXEC(N'SELECT MigrationId FROM [\$$(DB_NAME)].dbo.__EFMigrationsHistory');" \
  > /work/applied.txt
{{- end }}

{{- define "grif.migrationSmallResources" -}}
requests:
  cpu: 10m
  memory: 32Mi
limits:
  memory: 128Mi
{{- end }}

{{/* Copies this build's migration IDs (/app/migrations.txt) to /work. */}}
{{- define "grif.migrationListContainer" -}}
- name: migration-list
  image: {{ include "grif.backendImage" . }}
  imagePullPolicy: {{ .Values.backend.image.pullPolicy }}
  command: ["cp", "/app/migrations.txt", "/work/migrations.txt"]
  volumeMounts:
    - name: migrations-work
      mountPath: /work
  resources:
    {{- include "grif.migrationSmallResources" . | nindent 4 }}
  {{- with .Values.migrations.securityContext }}
  securityContext:
    {{- toYaml . | nindent 4 }}
  {{- end }}
{{- end }}

{{/*
The backend pod's init containers: wait until the database has every migration in the image, without
changing anything. The app starts as soon as the Job is done, rather than crashing and backing off.
*/}}
{{- define "grif.backendMigrationWait" -}}
{{- $m := .Values.migrations -}}
{{ include "grif.migrationListContainer" . }}
- name: wait-for-migrations
  image: {{ $m.sqlcmdImage }}
  imagePullPolicy: {{ $m.imagePullPolicy }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -u
      last=""
      while :; do
        if ( {{- include "grif.appliedMigrationsQuery" . | nindent 10 }}
           ) 2>/work/error.txt; then
          if grep -qxF NO_DATABASE /work/applied.txt; then
            state="Waiting for the migration Job to create database $DB_NAME."
          else
            grep -vxF -f /work/applied.txt /work/migrations.txt > /work/pending.txt || true
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
    {{- include "grif.dbEnv" . | nindent 4 }}
    {{- with $m.extraEnv }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  volumeMounts:
    - name: migrations-work
      mountPath: /work
  resources:
    {{- include "grif.migrationSmallResources" . | nindent 4 }}
  {{- with $m.securityContext }}
  securityContext:
    {{- toYaml . | nindent 4 }}
  {{- end }}
{{- end }}

{{/* The Job's init containers: wait, list, check, back up, your own steps. Its container migrates. */}}
{{- define "grif.migrationJobSteps" -}}
{{- $m := .Values.migrations -}}
- name: wait-for-database
  image: {{ include "grif.backendImage" . }}
  imagePullPolicy: {{ .Values.backend.image.pullPolicy }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -eu
      {{- include "grif.waitForDatabaseScript" . | nindent 6 }}
  env:
    - name: DB_HOST
      value: {{ include "grif.dbHost" . | quote }}
    - name: DB_PORT
      value: {{ include "grif.dbPort" . | quote }}
    - name: WAIT_SECONDS
      value: {{ $m.databaseWaitSeconds | quote }}
    {{- with $m.extraEnv }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  resources:
    {{- include "grif.migrationSmallResources" . | nindent 4 }}
  {{- with $m.securityContext }}
  securityContext:
    {{- toYaml . | nindent 4 }}
  {{- end }}
{{ include "grif.migrationListContainer" . }}
# Which of them the database lacks (/work/pending.txt: all of them if it doesn't exist);
# /work/db-exists if it does.
- name: check-pending
  image: {{ $m.sqlcmdImage }}
  imagePullPolicy: {{ $m.imagePullPolicy }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -eu
      {{- if $m.scripts.checkPending }}
      {{- $m.scripts.checkPending | nindent 6 }}
      {{- else }}
      {{- include "grif.appliedMigrationsQuery" . | nindent 6 }}
      if grep -qxF NO_DATABASE /work/applied.txt; then
        echo "Database $DB_NAME doesn't exist: the migration creates it."
        : > /work/applied.txt
      else
        touch /work/db-exists
      fi
      grep -vxF -f /work/applied.txt /work/migrations.txt > /work/pending.txt || true
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
  {{- with $m.securityContext }}
  securityContext:
    {{- toYaml . | nindent 4 }}
  {{- end }}
{{- if $m.backup.enabled }}
# Only an existing database with pending migrations. SQL Server writes the file, so the folder is on
# its side.
- name: backup
  image: {{ $m.sqlcmdImage }}
  imagePullPolicy: {{ $m.imagePullPolicy }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -eu
      {{- if $m.scripts.backup }}
      {{- $m.scripts.backup | nindent 6 }}
      {{- else }}
      if [ ! -e /work/db-exists ]; then echo "No database to back up."; exit 0; fi
      if [ ! -s /work/pending.txt ]; then echo "Nothing to migrate: no backup needed."; exit 0; fi
      export BACKUP_FILE="$BACKUP_DIR/${DB_NAME}_$(date +%Y%m%d_%H%M%S).bak"
      echo "Backing up $DB_NAME to $BACKUP_FILE"
      /opt/mssql-tools/bin/sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -P "$DB_PASSWORD" -N -C -b \
        -Q "EXEC master.sys.xp_create_subdir N'\$$(BACKUP_DIR)';
          BACKUP DATABASE [\$$(DB_NAME)] TO DISK = N'\$$(BACKUP_FILE)' WITH FORMAT, INIT, NAME = 'Pre-migration backup';"
      {{- end }}
  env:
    {{- include "grif.dbEnv" . | nindent 4 }}
    - name: BACKUP_DIR
      value: {{ $m.backup.directory | default (printf "/var/opt/mssql/backup/%s" .Release.Namespace) | quote }}
    {{- with $m.extraEnv }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  volumeMounts:
    - name: migrations-work
      mountPath: /work
  resources:
    {{- include "grif.migrationSmallResources" . | nindent 4 }}
  {{- with $m.securityContext }}
  securityContext:
    {{- toYaml . | nindent 4 }}
  {{- end }}
{{- end }}
{{- with $m.extraInitContainers }}
{{ tpl (toYaml .) $ }}
{{- end }}
{{- end }}

{{- define "grif.migrateContainer" -}}
{{- $m := .Values.migrations -}}
- name: migrate
  image: {{ include "grif.backendImage" . }}
  imagePullPolicy: {{ .Values.backend.image.pullPolicy }}
  command: ["/bin/sh", "-c"]
  args:
    - |
      set -eu
      {{- if $m.scripts.migrate }}
      {{- $m.scripts.migrate | nindent 6 }}
      {{- else }}
      if [ ! -s /work/pending.txt ]; then echo "Nothing to migrate."; exit 0; fi
      exec /app/efbundle
      {{- end }}
  env:
    {{- include "grif.migrationEnv" . | nindent 4 }}
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
  {{- with $m.securityContext }}
  securityContext:
    {{- toYaml . | nindent 4 }}
  {{- end }}
{{- end }}
