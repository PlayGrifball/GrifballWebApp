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

{{/*
The Database Backup page's scratch server (backupScratch): "true" when it's PostgreSQL, empty for SQL Server.
Unset, the other kind than the app's: PostgreSQL beside SQL Server or SQLite, SQL Server beside PostgreSQL.
*/}}
{{- define "grif.backupScratchPostgres" -}}
{{- $p := .Values.backupScratch.provider -}}
{{- if or (eq $p "Postgres") (and (not $p) (not (include "grif.postgres" .))) }}true{{ end -}}
{{- end }}

{{/* The pods that run the jobs, the Database Backup page's backup and export among them: the worker's (worker.enabled), else the backend's. */}}
{{- define "grif.jobsApp" -}}
{{- if .Values.worker.enabled }}grif-worker{{ else }}grif-backend{{ end -}}
{{- end }}

{{/* "true" when the pod that runs the jobs (grif.jobsApp) starts and stops the scratch server (backupScratch.mode onDemand). */}}
{{- define "grif.backupScratchOnDemand" -}}
{{- if and .Values.backupScratch.enabled (eq .Values.backupScratch.mode "onDemand") }}true{{ end -}}
{{- end }}

{{/* Sablier's API (onDemand): onDemand.sablierUrl, else a Service named sablier in namespace sablier. No trailing slash. */}}
{{- define "grif.sablierUrl" -}}
{{- .Values.onDemand.sablierUrl | default "http://sablier.sablier.svc:10000" | trimSuffix "/" -}}
{{- end }}

{{/* The release's Sablier group (onDemand): onDemand.group, else the release's namespace. */}}
{{- define "grif.onDemandGroup" -}}
{{- .Values.onDemand.group | default .Release.Namespace -}}
{{- end }}

{{/*
The components onDemand keeps running, out of the group (a JSON list of app.kubernetes.io/component values):
onDemand.exclude, plus what the excluded apps need from the chart. The backend (backend) or the Discord bot
(discord, with worker.enabled) keeps the database, Redis and Ollama; the worker (worker, with worker.enabled) the
database and Redis. A database kept running keeps its SQL exporter.
*/}}
{{- define "grif.onDemandKept" -}}
{{- $root := . }}
{{- $kept := .Values.onDemand.exclude | default list }}
{{- $needs := dict "backend" (list "mssql" "postgres" "redis" "ollama") }}
{{- if .Values.worker.enabled }}
{{- $_ := set $needs "worker" (list "mssql" "postgres" "redis") }}
{{- $_ := set $needs "discord" (list "mssql" "postgres" "redis" "ollama") }}
{{- end }}
{{- range $app, $stores := $needs }}
{{- if has $app $kept }}{{ $kept = concat $kept $stores }}{{ end }}
{{- end }}
{{- if or (has "mssql" $kept) (has "postgres" $kept) }}{{ $kept = append $kept "sql-exporter" }}{{ end }}
{{- $kept | uniq | toJson }}
{{- end }}

{{/* "true" when a component (app.kubernetes.io/component) is in the onDemand group: onDemand on, and the
   component not kept running (grif.onDemandKept). Takes dict "root", "component". */}}
{{- define "grif.onDemandIn" -}}
{{- if and .root.Values.onDemand.enabled (not (has .component (include "grif.onDemandKept" .root | fromJsonArray))) }}true{{ end -}}
{{- end }}

{{/*
A Deployment's labels in the onDemand group (grif.onDemandIn): Sablier stops it while nobody visits and starts it
on a visit. Both key forms: sablier.* (every Sablier release) and sablierapp.dev/* (the Kubernetes form newer ones
prefer); the same values, so either reads the same. Sablier starts a Deployment at 1 replica unless
active.replicas says more. Takes dict "root", "component", "replicas".
*/}}
{{- define "grif.onDemandLabels" -}}
{{- if include "grif.onDemandIn" . }}
{{- $group := include "grif.onDemandGroup" .root }}
{{- range $prefix := list "sablier." "sablierapp.dev/" }}
{{ $prefix }}enable: "true"
{{ $prefix }}group: {{ $group | quote }}
{{- if gt (int $.replicas) 1 }}
{{ $prefix }}active.replicas: {{ $.replicas | quote }}
{{- end }}
{{- end }}
{{- end }}
{{- end }}

{{/* A Deployment's replicas: none in the onDemand group (grif.onDemandIn), where Sablier scales it (a count here
   would be set back by every Helm upgrade or Argo CD sync). Takes dict "root", "component", "replicas". */}}
{{- define "grif.replicas" -}}
{{- if not (include "grif.onDemandIn" .) }}replicas: {{ .replicas }}{{ end -}}
{{- end }}

{{/* "true" with the chart's own database in the onDemand group (stopped while nobody visits): the Jobs that wait
   for it start the group first (grif.waitForDatabaseScript). A database onDemand keeps running needs no start. */}}
{{- define "grif.onDemandWakesDatabase" -}}
{{- $database := ternary "postgres" "mssql" (eq .Values.database.provider "postgres") }}
{{- if and (include "grif.bundled" .) (not .Values.database.host) (include "grif.onDemandIn" (dict "root" . "component" $database)) }}true{{ end -}}
{{- end }}

{{/* "true" with database.provider postgres; empty with sqlserver. */}}
{{- define "grif.postgres" -}}
{{- if eq .Values.database.provider "postgres" }}true{{ end -}}
{{- end }}

{{/* "true" with database.provider sqlite: a file on the backend's state volume, no server (docs/sqlite.md). */}}
{{- define "grif.sqlite" -}}
{{- if eq .Values.database.provider "sqlite" }}true{{ end -}}
{{- end }}

{{/*
SQLite's requirements, which fail the render otherwise: one backend pod and no worker (one process opens the
file), the state volume (the file), and nothing that needs a server (the migration Job and its backups,
sql-exporter). The backend applies its migrations itself as it starts.
*/}}
{{- define "grif.validateSqlite" -}}
{{- if include "grif.sqlite" . -}}
{{- if gt (int .Values.backend.replicas) 1 }}{{ fail "database.provider sqlite runs one backend: set backend.replicas 1" }}{{ end -}}
{{- if .Values.worker.enabled }}{{ fail "database.provider sqlite: the worker would open the file from another pod; set worker.enabled false" }}{{ end -}}
{{- if not .Values.backend.persistence.enabled }}{{ fail "database.provider sqlite keeps its file on the state volume: set backend.persistence.enabled true" }}{{ end -}}
{{- if .Values.migrations.enabled }}{{ fail "database.provider sqlite: the backend migrates the file itself; set migrations.enabled false" }}{{ end -}}
{{- if .Values.sqlExporter.enabled }}{{ fail "database.provider sqlite has no server for sql-exporter: set sqlExporter.enabled false" }}{{ end -}}
{{- end -}}
{{- end }}

{{/* SQLite's file: database.sqlite.file on the state volume. */}}
{{- define "grif.sqliteFile" -}}
{{- printf "%s/%s" .Values.backend.persistence.mountPath .Values.database.sqlite.file -}}
{{- end }}

{{/* The provider's value: .pg with PostgreSQL, .sql with SQL Server. Takes dict "root", "sql", "pg". */}}
{{- define "grif.byProvider" -}}
{{- if include "grif.postgres" .root }}{{ .pg }}{{ else }}{{ .sql }}{{ end -}}
{{- end }}

{{/* "true" when the chart runs the provider's server: mssql.enabled for SQL Server, postgres.enabled for
   PostgreSQL; never for SQLite. The other provider's enabled is ignored, so switching provider takes one value. */}}
{{- define "grif.bundled" -}}
{{- if include "grif.sqlite" . }}
{{- else if include "grif.postgres" . }}{{ if .Values.postgres.enabled }}true{{ end }}
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

{{/* "true" when PostgreSQL's clients require TLS (postgres.tls.enabled). */}}
{{- define "grif.pgTls" -}}
{{- if and (include "grif.postgres" .) .Values.postgres.tls.enabled }}true{{ end -}}
{{- end }}

{{/* Connection string options: database.options, else the provider's default. */}}
{{- define "grif.dbOptions" -}}
{{- .Values.database.options | default (include "grif.byProvider" (dict "root" . "sql" "Encrypt=True;TrustServerCertificate=True" "pg" (printf "%sGSS Encryption Mode=Disable" (ternary "SSL Mode=Require;" "" (eq (include "grif.pgTls" .) "true"))))) -}}
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

{{/* The backend Deployment's strategy: backend.strategy, else Recreate while the ReadWriteOnce state
volume is mounted in its pods (persistence, and no grif-discord to mount it instead), RollingUpdate otherwise. */}}
{{- define "grif.backendStrategy" -}}
{{- $b := .Values.backend -}}
{{- if $b.strategy -}}
{{- toYaml $b.strategy -}}
{{- else -}}
type: {{ ternary "Recreate" "RollingUpdate" (and $b.persistence.enabled (not .Values.worker.enabled)) }}
{{- end -}}
{{- end }}

{{/* "true" when the backend has a SignalR backplane: the chart's Redis or an external one. */}}
{{- define "grif.redis" -}}
{{- if or .Values.redis.enabled .Values.redis.external.connectionString.secretName }}true{{ end -}}
{{- end }}

{{- define "grif.backendHealthPath" -}}
{{- $hc := .root.Values.backend.config.HealthChecks | default dict -}}
{{- if eq .probe "live" -}}{{- $hc.LivePath | default "/health/live" -}}
{{- else -}}{{- $hc.ReadyPath | default "/health/ready" -}}{{- end -}}
{{- end }}

{{/* The backend's health paths (HealthChecks: Path, LivePath, ReadyPath, read as the backend's HealthCheckPaths reads
   them: trimmed, one leading slash, the default when empty) as a PromQL regex, as it goes in the dashboard's JSON: any
   of them or below one, any case. Health probes are left out of the dashboard's latency panels with it. */}}
{{- define "grif.healthRoutesRegex" -}}
{{- $hc := .Values.backend.config.HealthChecks | default dict -}}
{{- $paths := list -}}
{{- range $p := list (list $hc.Path "health") (list $hc.LivePath "health/live") (list $hc.ReadyPath "health/ready") -}}
{{- $path := trimAll "/" (trim (toString (index $p 0 | default ""))) | default (index $p 1) -}}
{{- $paths = append $paths (printf "/%s" $path | regexQuoteMeta | replace "\\" "\\\\\\\\") -}}
{{- end -}}
(?i)(?:{{ join "|" $paths }})(?:/.*)?
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
{{- if include "grif.pgTls" $r }}
# TLS, without checking the certificate (postgres.tls).
- name: PGSSLMODE
  value: require
{{- end }}
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
Waits until host:port accepts connections: network policies may admit a new pod's IP only some time
after it starts, and until then its packets are dropped, name lookups included. Each try is cut off
after 3 seconds, lookup and all (nc -w covers only the connection): an untimed try hung for
61-110 seconds on every Job but a release's first (k3s, 2026-10-15).
Needs DB_HOST, DB_PORT, WAIT_SECONDS.
With onDemand and the chart's database, it first asks Sablier to start the group (its poke strategy: it starts
them and answers at once), as a visit would, so a Job run while nobody visits finds the database up. The
same network policies drop the poke too until they admit the pod (a single refused try left grif-test's
database asleep and failed its sync, 2026-11-16), so it retries for a minute, each try cut off likewise.
*/}}
{{- define "grif.waitForDatabaseScript" -}}
{{- if include "grif.onDemandWakesDatabase" . }}
{{- $o := .Values.onDemand }}
{{- $url := printf "%s/api/strategies/poke?group=%s&session_duration=%s" (include "grif.sablierUrl" .) (include "grif.onDemandGroup" . | urlquery) (urlquery $o.sessionDuration) -}}
poke_end=$(( $(date +%s) + 60 ))
until timeout 12 wget -q -T 10 -O /dev/null {{ $url | squote }}; do
  if [ "$(date +%s)" -ge "$poke_end" ]; then echo "Sablier didn't answer ({{ include "grif.sablierUrl" . }}): waiting for the database anyway."; break; fi
  sleep 2
done
{{ end -}}
end=$(( $(date +%s) + WAIT_SECONDS ))
until timeout 3 nc -w 2 "$DB_HOST" "$DB_PORT" </dev/null; do
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
# Its status reads ONLINE before SQL Server has started it after a restart (a migration Job then failed with Msg 904,
# "cannot be autostarted during server shutdown or startup"): a query in it (the probe) has to answer too, and fails
# (-b: sqlcmd exits non-zero) until it does.
database_state() {
  sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -h -1 -W -v DB_NAME="$DB_NAME" -Q "SET NOCOUNT ON;
    DECLARE @probe nvarchar(400) = N'DECLARE @n int; SELECT @n = COUNT(*) FROM ' + QUOTENAME(N'\$$(DB_NAME)') + N'.sys.objects;';
    IF DB_ID(N'\$$(DB_NAME)') IS NOT NULL EXEC (@probe);
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
{{/* The chart's SQL Server's folder for the Database Backup page's .bak (database.logins.app.nativeBackups): an emptyDir, outside /var/opt/mssql, which bulk reads never take without sysadmin. */}}
{{- define "grif.mssqlDownloadDir" -}}
/var/opt/grif-download
{{- end }}

{{- define "grif.backupDir" -}}
{{- .Values.migrations.backup.directory | default (printf "%s/%s" (include "grif.byProvider" (dict "root" . "sql" "/var/opt/mssql/backup" "pg" "/backup")) .Release.Namespace) -}}
{{- end }}

{{/* "true" when SQL Server log backups are on (migrations.backup.log): the database in FULL recovery, else SIMPLE. */}}
{{- define "grif.logBackups" -}}
{{- $m := .Values.migrations -}}
{{- if and $m.enabled $m.backup.enabled $m.backup.log.enabled (not (include "grif.postgres" .)) }}true{{ end -}}
{{- end }}

{{/* "true" when a grif-backup pod runs: the scheduled backup or the log backups. */}}
{{- define "grif.backupPods" -}}
{{- $m := .Values.migrations -}}
{{- if or (and $m.enabled $m.backup.enabled $m.backup.scheduled.enabled) (include "grif.logBackups" .) }}true{{ end -}}
{{- end }}

{{/* SQL Server: T-SQL setting @compression to COMPRESSION where the edition has it (Enterprise, Standard, Developer), else NO_COMPRESSION. */}}
{{- define "grif.mssqlCompression" -}}
DECLARE @compression nvarchar(20) = CASE WHEN CONVERT(int, SERVERPROPERTY('EngineEdition')) = 3 OR (CONVERT(int, SERVERPROPERTY('EngineEdition')) = 2 AND CONVERT(nvarchar(128), SERVERPROPERTY('Edition')) NOT LIKE N'Web%') THEN N'COMPRESSION' ELSE N'NO_COMPRESSION' END;
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

{{/*
The Halo mode the backend runs in: backend.config.HaloInfinite.Mode when config sets it, else backend.halo.mode;
"gateway" or "direct" (lower case, for comparing).
*/}}
{{- define "grif.haloMode" -}}
{{- $b := .Values.backend -}}
{{- $configured := dig "HaloInfinite" "Mode" "" ($b.config | default dict) -}}
{{- $configured | default $b.halo.mode | default "Gateway" | lower -}}
{{- end }}

{{/* "true" when this release runs its own Halo gateway (the halo-gateway subchart, halo-gateway.enabled). */}}
{{- define "grif.bundledHaloGateway" -}}
{{- if (index .Values "halo-gateway" | default dict).enabled }}true{{ end -}}
{{- end }}

{{/*
The bundled gateway's Service: the halo-gateway chart's fullname helper (the release name if it already contains
"halo-gateway", else <release>-halo-gateway), on its service.port.
*/}}
{{- define "grif.bundledHaloGatewayUrl" -}}
{{- $name := ternary (.Release.Name | trunc 63 | trimSuffix "-") (printf "%s-halo-gateway" .Release.Name | trunc 63 | trimSuffix "-") (contains "halo-gateway" .Release.Name) -}}
{{- $port := dig "service" "port" 8080 (index .Values "halo-gateway" | default dict) -}}
{{- printf "http://%s:%v/" $name $port -}}
{{- end }}

{{/* The Halo gateway URL set explicitly: config's HaloGateway.BaseUrl, else backend.halo.gateway.url. Empty: neither. */}}
{{- define "grif.haloGatewayExplicitUrl" -}}
{{- $b := .Values.backend -}}
{{- dig "HaloGateway" "BaseUrl" "" ($b.config | default dict) | default $b.halo.gateway.url -}}
{{- end }}

{{/* "true" when the backend calls the bundled gateway: it's enabled and no URL is set explicitly. */}}
{{- define "grif.haloGatewayInRelease" -}}
{{- if and (include "grif.bundledHaloGateway" .) (not (include "grif.haloGatewayExplicitUrl" .)) }}true{{ end -}}
{{- end }}

{{/*
The Halo gateway's URL the backend uses (Gateway mode): an explicit one (grif.haloGatewayExplicitUrl), else the
bundled gateway's Service when halo-gateway.enabled, else the shared gateway in the cluster (halo-gateway namespace).
*/}}
{{- define "grif.haloGatewayUrl" -}}
{{- $explicit := include "grif.haloGatewayExplicitUrl" . -}}
{{- if $explicit -}}
{{- $explicit -}}
{{- else if include "grif.bundledHaloGateway" . -}}
{{- include "grif.bundledHaloGatewayUrl" . -}}
{{- else -}}
http://halo-gateway.halo-gateway.svc:8080/
{{- end -}}
{{- end }}

{{/*
The Secret holding the backend's gateway API key, as JSON {secretName, key}: backend.halo.gateway.apiKey when its
secretName is set; else, with the bundled gateway, the first halo-gateway.apiKeys entry that reads its key from a
Secret (secretName/secretKey), so both read the one Secret. Empty secretName: no key.
*/}}
{{- define "grif.haloGatewayApiKey" -}}
{{- $own := .Values.backend.halo.gateway.apiKey -}}
{{- $ref := dict "secretName" $own.secretName "key" $own.key -}}
{{- if and (not $own.secretName) (include "grif.bundledHaloGateway" .) -}}
{{- range (index .Values "halo-gateway").apiKeys | default list -}}
{{- if and .secretName (not $ref.secretName) -}}
{{- $ref = dict "secretName" .secretName "key" .secretKey -}}
{{- end -}}
{{- end -}}
{{- end -}}
{{- toJson $ref -}}
{{- end }}

{{/* The OpenTelemetry identity the backend and the frontend share (backend.telemetry): service.name (default the
   namespace), the resource attributes and the metric interval, so both land as one service on the Telemetry page
   (and telemetry-gate allows one per environment). Env entries; takes the root.
   Always service.namespace, k8s.namespace.name (the release's namespace) and k8s.pod.name (the downward API, which the
   kubelet expands into the attributes: K8S_POD_NAME comes first), so the telemetry says where it's from without a
   collector adding it: Prometheus names a series' job <service.namespace>/<service.name>, and the Grafana dashboard
   selects on it, lists environments by namespace and charts by pod. resourceAttributes may replace any of them. */}}
{{- define "grif.otelServiceEnv" -}}
{{- $bt := .Values.backend.telemetry -}}
- name: K8S_POD_NAME
  valueFrom:
    fieldRef:
      fieldPath: metadata.name
- name: OTEL_SERVICE_NAME
  value: {{ $bt.serviceName | default .Release.Namespace | quote }}
{{- $attrs := dict "service.namespace" .Release.Namespace "k8s.namespace.name" .Release.Namespace "k8s.pod.name" "$(K8S_POD_NAME)" }}
{{- if $bt.environment }}{{ $_ := set $attrs "deployment.environment.name" $bt.environment }}{{ end }}
{{- range $k, $v := $bt.resourceAttributes }}{{ $_ := set $attrs $k (printf "%v" $v) }}{{ end }}
{{- $pairs := list }}
{{- range $k, $v := $attrs }}{{ $pairs = append $pairs (printf "%s=%s" $k $v) }}{{ end }}
- name: OTEL_RESOURCE_ATTRIBUTES
  value: {{ join "," $pairs | quote }}
{{- if $bt.metricExportInterval }}
- name: OTEL_METRIC_EXPORT_INTERVAL
  value: {{ $bt.metricExportInterval | quote }}
{{- end }}
{{- end }}

{{/* A URL's port: the one after its host's colon, else 443 for https and 80 otherwise. Only digits after a colon
   count, so a host's own digits (http://otel2) are no port. Takes the URL. */}}
{{- define "grif.urlPort" -}}
{{- $u := urlParse . -}}
{{- trimPrefix ":" (regexFind ":[0-9]+$" $u.host) | default (ternary "443" "80" (eq $u.scheme "https")) -}}
{{- end }}

{{/* The chats' model's server (Ollama:Url): the chart's Ollama, else an external one, else none (no chat model). */}}
{{- define "grif.ollamaUrl" -}}
{{- if .Values.ollama.enabled }}{{ printf "http://grif-ollama:%v/" .Values.ollama.port }}{{ else }}{{ .Values.ollama.external.url }}{{ end -}}
{{- end }}

{{/* The voice chat's speech server (Speech:Url): the chart's, else an external one, else none (no voice). */}}
{{- define "grif.speechUrl" -}}
{{- if .Values.speech.enabled }}{{ printf "http://grif-speech:%v/" .Values.speech.port }}{{ else }}{{ .Values.speech.external.url }}{{ end -}}
{{- end }}

{{/* The chart's Ollama's environment, its server's and its pull's: its home and models on the volume, and ollama.env. */}}
{{- define "grif.ollamaEnv" -}}
- name: HOME
  value: /data
- name: OLLAMA_MODELS
  value: /data/models
{{- range $name, $value := .Values.ollama.env }}
- name: {{ $name }}
  value: {{ $value | quote }}
{{- end }}
{{- end }}
