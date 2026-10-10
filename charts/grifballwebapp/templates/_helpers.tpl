{{/* Labels on every object. Selectors use only app: <name>, which stays stable across chart versions. */}}
{{- define "grif.labels" -}}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
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

{{/* The database host: database.host, else the chart's SQL Server. */}}
{{- define "grif.dbHost" -}}
{{- if .Values.database.host -}}
{{- .Values.database.host -}}
{{- else if .Values.mssql.enabled -}}
{{- .Values.mssql.service.name -}}
{{- else -}}
{{- fail "database.host is required when mssql.enabled is false" -}}
{{- end -}}
{{- end }}

{{- define "grif.dbPort" -}}
{{- if and .Values.mssql.enabled (not .Values.database.host) -}}
{{- .Values.mssql.service.port -}}
{{- else -}}
{{- .Values.database.port -}}
{{- end -}}
{{- end }}

{{/* DB_PASSWORD env entry, from database.password. */}}
{{- define "grif.dbPasswordEnv" -}}
- name: DB_PASSWORD
  valueFrom:
    secretKeyRef:
      name: {{ include "grif.secretName" (dict "secretName" .Values.database.password.secretName "root" .) }}
      key: {{ .Values.database.password.key }}
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

{{/* The database connection, for sqlcmd. */}}
{{- define "grif.dbEnv" -}}
- name: DB_HOST
  value: {{ include "grif.dbHost" . | quote }}
- name: DB_PORT
  value: {{ include "grif.dbPort" . | quote }}
- name: DB_NAME
  value: {{ .Values.database.name | quote }}
- name: DB_USER
  value: {{ .Values.database.user | quote }}
# sqlcmd reads the password from here, so it is never on a command line.
- name: SQLCMDPASSWORD
  valueFrom:
    secretKeyRef:
      name: {{ include "grif.secretName" (dict "secretName" .Values.database.password.secretName "root" .) }}
      key: {{ .Values.database.password.key }}
{{- end }}

{{/* DB_PASSWORD and the connection string the migration bundle reads, from database. */}}
{{- define "grif.migrationEnv" -}}
{{ include "grif.dbPasswordEnv" . }}
# Read by DesignTimeContextFactory, which logs only server and database, never this.
- name: ConnectionStrings__GrifballWebApp
  value: {{ printf "Server=%s,%s;Database=%s;User Id=%s;Password=$(DB_PASSWORD);%s" (include "grif.dbHost" .) (include "grif.dbPort" .) .Values.database.name .Values.database.user .Values.database.options | quote }}
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
after a restart, and a query against one still recovering fails. Needs grif.dbEnv.
*/}}
{{- define "grif.waitForDatabaseOnlineScript" -}}
until state=$(sqlcmd -S "$DB_HOST,$DB_PORT" -U "$DB_USER" -N -C -b -h -1 -W -v DB_NAME="$DB_NAME" -Q "SET NOCOUNT ON;
    SELECT CASE WHEN DB_ID(N'\$$(DB_NAME)') IS NULL THEN 'ONLINE'
      ELSE CONVERT(nvarchar(60), DATABASEPROPERTYEX(N'\$$(DB_NAME)', 'Status')) END" 2>&1) \
    && [ "$state" = ONLINE ]; do
  if [ "$(date +%s)" -ge "$end" ]; then echo "Database $DB_NAME not ready: $(echo "$state" | head -c 300)"; exit 1; fi
  sleep 2
done
echo "Database $DB_NAME is ready."
{{- end }}

{{/* The backup folder, on the SQL Server's side: migrations.backup.directory, or the chart's default. */}}
{{- define "grif.backupDir" -}}
{{- .Values.migrations.backup.directory | default (printf "/var/opt/mssql/backup/%s" .Release.Namespace) -}}
{{- end }}

{{/*
The backend's login: database.logins.app with logins on, else the administrator (database.user).
grif.appLogin returns the user; grif.appPasswordEnv an env entry named by .name from its Secret.
*/}}
{{- define "grif.appUser" -}}
{{- if .Values.database.logins.enabled }}{{ .Values.database.logins.app.user }}{{ else }}{{ .Values.database.user }}{{ end -}}
{{- end }}

{{- define "grif.appPasswordEnv" -}}
{{- $p := ternary .root.Values.database.logins.app.password .root.Values.database.password .root.Values.database.logins.enabled -}}
- name: {{ .name }}
  valueFrom:
    secretKeyRef:
      name: {{ include "grif.secretName" (dict "secretName" $p.secretName "root" .root) }}
      key: {{ $p.key }}
{{- end }}

{{/* The backend's connection for sqlcmd (its wait for the migrations): the app login. */}}
{{- define "grif.appDbEnv" -}}
- name: DB_HOST
  value: {{ include "grif.dbHost" . | quote }}
- name: DB_PORT
  value: {{ include "grif.dbPort" . | quote }}
- name: DB_NAME
  value: {{ .Values.database.name | quote }}
- name: DB_USER
  value: {{ include "grif.appUser" . | quote }}
# sqlcmd reads the password from here, so it is never on a command line.
{{ include "grif.appPasswordEnv" (dict "root" . "name" "SQLCMDPASSWORD") }}
{{- end }}

{{/* Whether the migration Job creates the monitoring login: logins on, sql-exporter on, its own login not set. */}}
{{- define "grif.monitoringLogin" -}}
{{- if and .Values.database.logins.enabled .Values.sqlExporter.enabled (not .Values.sqlExporter.user) }}true{{ end -}}
{{- end }}
