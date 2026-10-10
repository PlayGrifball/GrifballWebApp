{{/* Labels on every object. Selectors use only app: <name>, which stays stable across chart versions. */}}
{{- define "grif.labels" -}}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/* Labels on pod templates: grif.labels without the chart's version, so a new chart version alone
   restarts nothing (nor makes a new migration Job). */}}
{{- define "grif.podLabels" -}}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/* repository:tag, repository:tag@digest, or repository@digest. tag may itself carry a digest (tag@sha256:...
   or sha256:..., what Image Updater writes); digest, when set, replaces it. Takes an image values block. */}}
{{- define "grif.image" -}}
{{- $ref := .repository -}}
{{- $tag := toString (.tag | default "") -}}
{{- if .digest }}{{ $tag = regexReplaceAll "@.*$" $tag "" }}{{ if hasPrefix "sha256:" $tag }}{{ $tag = "" }}{{ end }}{{ end -}}
{{- if hasPrefix "sha256:" $tag }}{{ $ref = printf "%s@%s" $ref $tag }}
{{- else if $tag }}{{ $ref = printf "%s:%s" $ref $tag }}{{ end -}}
{{- if and .digest (not (contains "@" $ref)) }}{{ $ref = printf "%s@%s" $ref .digest }}{{ end -}}
{{- $ref -}}
{{- end }}

{{- define "grif.backendImage" -}}
{{- include "grif.image" .Values.backend.image -}}
{{- end }}

{{- define "grif.secretName" -}}
{{- .secretName | default .root.Values.secret.name -}}
{{- end }}

{{/* "true" with database.provider postgres; empty with sqlserver. */}}
{{- define "grif.postgres" -}}
{{- if eq .Values.database.provider "postgres" }}true{{ end -}}
{{- end }}

{{/* The provider's value: .pg with PostgreSQL, .sql with SQL Server. Takes dict "root", "sql", "pg". */}}
{{- define "grif.byProvider" -}}
{{- if include "grif.postgres" .root }}{{ .pg }}{{ else }}{{ .sql }}{{ end -}}
{{- end }}

{{/* "true" when the chart runs the provider's server: mssql.enabled for SQL Server, postgres.enabled for
   PostgreSQL. The other provider's enabled is ignored, so switching provider takes one value. */}}
{{- define "grif.bundled" -}}
{{- if include "grif.postgres" . }}{{ if .Values.postgres.enabled }}true{{ end }}
{{- else if .Values.mssql.enabled }}true{{ end -}}
{{- end }}

{{/* The database host: database.host, else the chart's server. */}}
{{- define "grif.dbHost" -}}
{{- if .Values.database.host -}}
{{- .Values.database.host -}}
{{- else if include "grif.bundled" . -}}
{{- include "grif.byProvider" (dict "root" . "sql" .Values.mssql.service.name "pg" .Values.postgres.service.name) -}}
{{- else -}}
{{- fail (printf "database.host is required when %s.enabled is false" (include "grif.byProvider" (dict "root" . "sql" "mssql" "pg" "postgres"))) -}}
{{- end -}}
{{- end }}

{{/* The chart's server's Service port, else database.port, else the provider's default. */}}
{{- define "grif.dbPort" -}}
{{- if and (include "grif.bundled" .) (not .Values.database.host) -}}
{{- include "grif.byProvider" (dict "root" . "sql" .Values.mssql.service.port "pg" .Values.postgres.service.port) -}}
{{- else -}}
{{- .Values.database.port | default (include "grif.byProvider" (dict "root" . "sql" 1433 "pg" 5432)) -}}
{{- end -}}
{{- end }}

{{/* The administrator: database.user, else the provider's (sa, postgres). */}}
{{- define "grif.dbUser" -}}
{{- .Values.database.user | default (include "grif.byProvider" (dict "root" . "sql" "sa" "pg" "postgres")) -}}
{{- end }}

{{/* The administrator's password as a Secret reference (JSON; secretName empty: secret.name). */}}
{{- define "grif.dbPassword" -}}
{{- dict "secretName" .Values.database.password.secretName "key" (.Values.database.password.key | default (include "grif.byProvider" (dict "root" . "sql" "SA_PASSWORD" "pg" "POSTGRES_PASSWORD"))) | toJson -}}
{{- end }}

{{/* Connection string options: database.options, else the provider's default. */}}
{{- define "grif.dbOptions" -}}
{{- .Values.database.options | default (include "grif.byProvider" (dict "root" . "sql" "Encrypt=True;TrustServerCertificate=True" "pg" "")) -}}
{{- end }}

{{/*
The connection string the app and the migration bundle read, as .user, the password from $(DB_PASSWORD):
SqlClient's for SQL Server, Npgsql's for PostgreSQL. Takes dict "root" and "user".
*/}}
{{- define "grif.connectionString" -}}
{{- $r := .root -}}
{{- if include "grif.postgres" $r -}}
{{- printf "Host=%s;Port=%s;Database=%s;Username=%s;Password=$(DB_PASSWORD)" (include "grif.dbHost" $r) (include "grif.dbPort" $r) $r.Values.database.name .user -}}
{{- with include "grif.dbOptions" $r }};{{ . }}{{ end -}}
{{- else -}}
{{- printf "Server=%s,%s;Database=%s;User Id=%s;Password=$(DB_PASSWORD);%s" (include "grif.dbHost" $r) (include "grif.dbPort" $r) $r.Values.database.name .user (include "grif.dbOptions" $r) -}}
{{- end -}}
{{- end }}

{{/* DB_PASSWORD env entry, from database.password. */}}
{{- define "grif.dbPasswordEnv" -}}
{{- $p := include "grif.dbPassword" . | fromJson -}}
- name: DB_PASSWORD
  valueFrom:
    secretKeyRef:
      name: {{ include "grif.secretName" (dict "secretName" $p.secretName "root" .) }}
      key: {{ $p.key }}
{{- end }}

{{/*
One setting as an environment variable value: strings as they are, numbers without float notation.
Kubernetes expands $(VAR) in env values, so '$' is doubled to keep a value literal.
*/}}
{{- define "grif.configValue" -}}
{{- $v := .value -}}
{{- if kindIs "float64" $v -}}
  {{- if eq $v (floor $v) -}}
    {{- if or (gt $v 9007199254740991.0) (lt $v -9007199254740991.0) -}}
      {{- fail (printf "%s: %v is too large to be exact as an unquoted number (Helm reads it as floating point); quote it" .name $v) -}}
    {{- end -}}
    {{- printf "%.0f" $v -}}
  {{- else -}}
    {{- toString $v -}}
  {{- end -}}
{{- else -}}
  {{- toString $v | replace "$" "$$" -}}
{{- end -}}
{{- end }}

{{/*
appsettings-style nested settings as env entries: maps become Section__Key, lists Section__0, null is
skipped. Takes dict "name" (prefix, "" at the top) and "value".
*/}}
{{- define "grif.configEnv" -}}
{{- $name := .name -}}
{{- if kindIs "map" .value -}}
  {{- range $k, $v := .value -}}
    {{- include "grif.configEnv" (dict "name" (ternary $k (printf "%s__%s" $name $k) (eq $name "")) "value" $v) -}}
  {{- end -}}
{{- else if kindIs "slice" .value -}}
  {{- range $i, $v := .value -}}
    {{- include "grif.configEnv" (dict "name" (printf "%s__%d" $name $i) "value" $v) -}}
  {{- end -}}
{{- else if not (kindIs "invalid" .value) -}}
{{- printf "\n- name: %s\n  value: %s" ($name | quote) (include "grif.configValue" (dict "name" $name "value" .value) | quote) -}}
{{- end -}}
{{- end }}

{{/* The setting path as an environment variable name: ':' becomes '__'. */}}
{{- define "grif.envName" -}}
{{- . | replace ":" "__" -}}
{{- end }}

{{- define "grif.backendHealthPath" -}}
{{- $hc := .root.Values.backend.config.HealthChecks | default dict -}}
{{- if eq .probe "live" -}}{{- $hc.LivePath | default "/health/live" -}}
{{- else -}}{{- $hc.ReadyPath | default "/health/ready" -}}{{- end -}}
{{- end }}

{{/* The database connection as the administrator: for sqlcmd, or for psql, pg_dump and pg_restore. */}}
{{- define "grif.dbEnv" -}}
{{- include "grif.connectionEnv" (dict "root" . "user" (include "grif.dbUser" .) "password" (include "grif.dbPassword" . | fromJson)) -}}
{{- end }}

{{/*
DB_HOST, DB_PORT, DB_NAME and DB_USER, and the password where the provider's tools read it: sqlcmd from
SQLCMDPASSWORD; libpq (psql, pg_dump, pg_restore) from PGPASSWORD, the rest of the connection from
PGHOST, PGPORT and PGUSER. Takes dict "root", "user" and "password" (a Secret reference).
*/}}
{{- define "grif.connectionEnv" -}}
{{- $r := .root -}}
- name: DB_HOST
  value: {{ include "grif.dbHost" $r | quote }}
- name: DB_PORT
  value: {{ include "grif.dbPort" $r | quote }}
- name: DB_NAME
  value: {{ $r.Values.database.name | quote }}
- name: DB_USER
  value: {{ .user | quote }}
{{- if include "grif.postgres" $r }}
# libpq reads the connection from these, and the password from PGPASSWORD: never on a command
# line. A connection attempt gives up after PGCONNECT_TIMEOUT seconds instead of hanging.
- name: PGHOST
  value: {{ include "grif.dbHost" $r | quote }}
- name: PGPORT
  value: {{ include "grif.dbPort" $r | quote }}
- name: PGUSER
  value: {{ .user | quote }}
- name: PGCONNECT_TIMEOUT
  value: "10"
{{ include "grif.secretEnv" (dict "root" $r "name" "PGPASSWORD" "ref" .password) }}
{{- else }}
# sqlcmd reads the password from here, so it is never on a command line.
{{ include "grif.secretEnv" (dict "root" $r "name" "SQLCMDPASSWORD" "ref" .password) }}
{{- end }}
{{- end }}

{{/* An env entry named .name from a Secret reference .ref (secretName empty: secret.name). */}}
{{- define "grif.secretEnv" -}}
- name: {{ .name }}
  valueFrom:
    secretKeyRef:
      name: {{ include "grif.secretName" (dict "secretName" .ref.secretName "root" .root) }}
      key: {{ .ref.key }}
{{- end }}

{{/* DB_PASSWORD and the connection string the migration bundle reads, from database. */}}
{{- define "grif.migrationEnv" -}}
{{ include "grif.dbPasswordEnv" . }}
# Read by DesignTimeContextFactory, which logs only server and database, never this.
- name: ConnectionStrings__GrifballWebApp
  value: {{ include "grif.connectionString" (dict "root" . "user" (include "grif.dbUser" .)) | quote }}
{{- if include "grif.postgres" . }}
- name: Database__Provider
  value: Postgres
{{- end }}
{{- end }}

{{/*
Waits until host:port accepts connections: network policies may admit a new pod's IP only seconds after
it starts. Needs DB_HOST, DB_PORT, WAIT_SECONDS.
*/}}
{{- define "grif.waitForDatabaseScript" -}}
end=$(( $(date +%s) + WAIT_SECONDS ))
until nc -w 2 "$DB_HOST" "$DB_PORT" </dev/null; do
  if [ "$(date +%s)" -ge "$end" ]; then echo "$DB_HOST:$DB_PORT unreachable"; exit 1; fi
  sleep 2
done
{{- end }}

{{/*
After grif.waitForDatabaseScript (same deadline): until SQL Server answers a query and the database,
if it exists, is ONLINE. SQL Server accepts logins before it has finished recovering its databases
after a restart, and a query against one still recovering fails. Needs grif.dbEnv. PostgreSQL:
grif.pgWaitForServerScript.
*/}}
{{- define "grif.waitForDatabaseOnlineScript" -}}
{{- if include "grif.postgres" . -}}
{{- include "grif.pgWaitForServerScript" . -}}
{{- else -}}
# A function, not inline in $(...): inside a multi-line command substitution the backend image's
# BusyBox sh ran the first $(DB_NAME) as a command (DB_NAME: not found) instead of passing it to
# sqlcmd, so the query asked about database '$' - which doesn't exist, so it read as ready.
database_state() {
  sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -h -1 -W -v DB_NAME="$DB_NAME" -Q "SET NOCOUNT ON;
    SELECT CASE WHEN DB_ID(N'\$$(DB_NAME)') IS NULL THEN 'ONLINE'
      ELSE CONVERT(nvarchar(60), DATABASEPROPERTYEX(N'\$$(DB_NAME)', 'Status')) END" 2>&1
}
until state=$(database_state) && [ "$state" = ONLINE ]; do
  if [ "$(date +%s)" -ge "$end" ]; then echo "Database $DB_NAME not ready: $(echo "$state" | head -c 300)"; exit 1; fi
  sleep 2
done
echo "Database $DB_NAME is ready."
{{- end }}
{{- end }}

{{/*
The backup folder: migrations.backup.directory, or the chart's default - on SQL Server's side (it writes
the file), or for PostgreSQL in the backup volume the chart's pods mount at /backup (pg_dump writes it).
*/}}
{{- define "grif.backupDir" -}}
{{- .Values.migrations.backup.directory | default (printf "%s/%s" (include "grif.byProvider" (dict "root" . "sql" "/var/opt/mssql/backup" "pg" "/backup")) .Release.Namespace) -}}
{{- end }}

{{/*
The backend's login: database.logins.app with logins on, else the administrator (database.user).
grif.appUser returns the user; grif.appPassword its password's Secret reference (JSON);
grif.appPasswordEnv an env entry named by .name from it.
*/}}
{{- define "grif.appUser" -}}
{{- if .Values.database.logins.enabled }}{{ .Values.database.logins.app.user }}{{ else }}{{ include "grif.dbUser" . }}{{ end -}}
{{- end }}

{{- define "grif.appPassword" -}}
{{- if .Values.database.logins.enabled }}{{ .Values.database.logins.app.password | toJson }}{{ else }}{{ include "grif.dbPassword" . }}{{ end -}}
{{- end }}

{{- define "grif.appPasswordEnv" -}}
{{- include "grif.secretEnv" (dict "root" .root "name" .name "ref" (include "grif.appPassword" .root | fromJson)) -}}
{{- end }}

{{/* The backend's connection for its wait for the migrations (sqlcmd or psql): the app login. */}}
{{- define "grif.appDbEnv" -}}
{{- include "grif.connectionEnv" (dict "root" . "user" (include "grif.appUser" .) "password" (include "grif.appPassword" . | fromJson)) -}}
{{- end }}

{{/* Whether the migration Job creates the monitoring login: logins on, sql-exporter on, its own login not set. */}}
{{- define "grif.monitoringLogin" -}}
{{- if and .Values.database.logins.enabled .Values.sqlExporter.enabled (not .Values.sqlExporter.user) }}true{{ end -}}
{{- end }}
