{{/*
The backend's pod template, shared by the backend Deployment and the worker's (worker.enabled): the same
image, settings, secrets, probes and security. dict: root, app (the pods' app label), component, role
(App__Role; empty: unset, the app's default, all), state (the state volume mounted, else an emptyDir),
pod (resources, podLabels, podAnnotations, nodeSelector, tolerations, affinity).
*/}}
{{- define "grif.backendPodTemplate" -}}
{{- $root := .root -}}
{{- $b := $root.Values.backend -}}
{{- $p := .pod -}}
metadata:
  labels:
    app: {{ .app }}
    {{- include "grif.podLabels" $root | nindent 4 }}
    app.kubernetes.io/component: {{ .component }}
    {{- with $p.podLabels }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  {{- with $p.podAnnotations }}
  annotations:
    {{- toYaml . | nindent 4 }}
  {{- end }}
spec:
  {{- if and (eq .app (include "grif.jobsApp" $root)) (include "grif.backupScratchOnDemand" $root) }}
  # Scales the Database Backup page's scratch server, and nothing else (backup-scratch.yaml's Role): the pod
  # that runs the jobs (grif.jobsApp), whose backup job uses it.
  serviceAccountName: grif-backend
  automountServiceAccountToken: true
  {{- else }}
  # Never calls the Kubernetes API.
  automountServiceAccountToken: false
  {{- end }}
  {{- with $root.Values.imagePullSecrets }}
  imagePullSecrets:
    {{- toYaml . | nindent 4 }}
  {{- end }}
  {{- with $b.podSecurityContext }}
  securityContext:
    {{- toYaml . | nindent 4 }}
  {{- end }}
  {{- if $root.Values.migrations.enabled }}
  # Wait until the migration Job has brought the database up to date (templates/_migrations.tpl).
  initContainers:
    {{- include "grif.backendMigrationWait" $root | nindent 4 }}
  {{- end }}
  containers:
    - name: grif-backend
      image: {{ include "grif.backendImage" $root }}
      imagePullPolicy: {{ $b.image.pullPolicy }}
      # The working directory is the state volume (persistence); ASPNETCORE_CONTENTROOT keeps the
      # app's own files (appsettings, wwwroot) at /app, and the dll is named by its absolute path.
      workingDir: {{ $b.persistence.mountPath }}
      command: ["dotnet", "/app/GrifballWebApp.Server.dll"]
      ports:
        - name: http
          containerPort: {{ $b.port }}
      startupProbe:
        httpGet:
          path: {{ include "grif.backendHealthPath" (dict "root" $root "probe" "live") }}
          port: http
        {{- toYaml $b.startupProbe | nindent 8 }}
      livenessProbe:
        httpGet:
          path: {{ include "grif.backendHealthPath" (dict "root" $root "probe" "live") }}
          port: http
        {{- toYaml $b.livenessProbe | nindent 8 }}
      readinessProbe:
        httpGet:
          path: {{ include "grif.backendHealthPath" (dict "root" $root "probe" "ready") }}
          port: http
        {{- toYaml $b.readinessProbe | nindent 8 }}
      {{- with $b.securityContext }}
      securityContext:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      {{- with $p.resources }}
      resources:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      env:
        - name: ASPNETCORE_CONTENTROOT
          value: /app
        - name: ASPNETCORE_HTTP_PORTS
          value: {{ $b.port | quote }}
        {{- with .role }}
        - name: App__Role
          value: {{ . }}
        {{- end }}
        {{- if include "grif.sqlite" $root }}
        - name: ConnectionStrings__GrifballWebApp
          value: {{ printf "Data Source=%s" (include "grif.sqliteFile" $root) | quote }}
        - name: Database__Provider
          value: Sqlite
        # No migration Job on SQLite: the backend applies its migrations as it starts, making the file the first time.
        - name: ApplyMigrations
          value: "true"
        - name: CreateDatabase
          value: "true"
        {{- else if $root.Values.database.connectionString.secretName }}
        - name: ConnectionStrings__GrifballWebApp
          valueFrom:
            secretKeyRef:
              name: {{ $root.Values.database.connectionString.secretName }}
              key: {{ required "database.connectionString.key is required with its secretName" $root.Values.database.connectionString.key }}
        {{- else }}
        {{- include "grif.appPasswordEnv" (dict "root" $root "name" "DB_PASSWORD") | nindent 8 }}
        - name: ConnectionStrings__GrifballWebApp
          value: {{ include "grif.connectionString" (dict "root" $root "user" (include "grif.appUser" $root)) | quote }}
        {{- end }}
        {{- if include "grif.postgres" $root }}
        - name: Database__Provider
          value: Postgres
        {{- else if and (not (include "grif.sqlite" $root)) $root.Values.mssql.enabled $root.Values.database.logins.app.nativeBackups }}
        # Where the chart's SQL Server writes the Database Backup page's .bak and reads it back (on its side).
        - name: DatabaseBackup__SqlServerDirectory
          value: {{ include "grif.mssqlDownloadDir" $root | quote }}
        {{- end }}
        {{- if $root.Values.backupScratch.enabled }}
        # The Database Backup page's scratch server: its kind on every pod (an API pod offers and accepts its
        # format), and its login, as its administrator, only on the pod that runs the jobs (grif.jobsApp), whose
        # backup job uses it. It holds nothing but the copies the page backs up.
        - name: DatabaseBackup__Scratch__Provider
          value: {{ ternary "Postgres" "SqlServer" (eq (include "grif.backupScratchPostgres" $root) "true") }}
        {{- if eq .app (include "grif.jobsApp" $root) }}
        - name: SCRATCH_DB_PASSWORD
          valueFrom:
            secretKeyRef:
              name: {{ include "grif.secretName" (dict "secretName" $root.Values.backupScratch.password.secretName "root" $root) }}
              key: {{ $root.Values.backupScratch.password.key }}
        {{- if include "grif.backupScratchPostgres" $root }}
        - name: DatabaseBackup__Scratch__ConnectionString
          value: "Host=grif-backup-scratch;Port=5432;Database=postgres;Username=postgres;Password=$(SCRATCH_DB_PASSWORD);GSS Encryption Mode=Disable"
        {{- else }}
        - name: DatabaseBackup__Scratch__ConnectionString
          value: "Server=grif-backup-scratch,1433;Database=master;User Id=sa;Password=$(SCRATCH_DB_PASSWORD);Encrypt=True;TrustServerCertificate=True"
        {{- end }}
        {{- if and (eq .app (include "grif.jobsApp" $root)) (include "grif.backupScratchOnDemand" $root) }}
        # Stopped until a backup needs it: started, and stopped again, by scaling this Deployment.
        - name: DatabaseBackup__Scratch__Deployment
          value: grif-backup-scratch
        {{- end }}
        {{- end }}
        {{- end }}
        {{- if eq .app "grif-worker" }}
        # This pod's own address, recorded with the Database Backup page's file its backup job makes, so an API pod
        # fetches it from this worker (not any: there may be several).
        - name: POD_IP
          valueFrom:
            fieldRef:
              fieldPath: status.podIP
        - name: DatabaseBackup__NodeUrl
          value: {{ printf "http://$(POD_IP):%v" $b.port | quote }}
        {{- end }}
        {{- with $root.Values.redis }}
        {{- if .enabled }}
        # The SignalR backplane: the chart's Redis.
        - name: ConnectionStrings__Redis
          value: {{ printf "grif-redis:%v" .port | quote }}
        {{- else if .external.connectionString.secretName }}
        # The SignalR backplane: an external Redis.
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: {{ .external.connectionString.secretName }}
              key: {{ required "redis.external.connectionString.key is required with its secretName" .external.connectionString.key }}
        {{- end }}
        {{- if and (include "grif.redis" $root) .channelPrefix }}
        - name: SignalR__RedisChannelPrefix
          value: {{ .channelPrefix | quote }}
        {{- end }}
        {{- end }}
        {{- with $b.dataProtection.certificate }}
        {{- if .secretName }}
        - name: DataProtection__Certificate
          value: {{ printf "%s/tls.crt" .mountPath | quote }}
        - name: DataProtection__CertificateKey
          value: {{ printf "%s/tls.key" .mountPath | quote }}
        {{- end }}
        {{- end }}
        {{- $sheets := $b.config.GoogleSheets | default dict }}
        {{- if and $b.googleCredentials.secretName (not (hasKey $sheets "Key")) }}
        - name: GoogleSheets__Key
          value: {{ printf "%s/%s" $b.googleCredentials.mountPath $b.googleCredentials.key | quote }}
        {{- end }}
        {{- with $b.telemetry }}
        {{- if .enabled }}
        - name: OTEL_SERVICE_NAME
          value: {{ .serviceName | default $root.Release.Namespace | quote }}
        - name: OTEL_EXPORTER_OTLP_ENDPOINT
          value: {{ required "backend.telemetry.endpoint is required when telemetry is enabled" .endpoint | quote }}
        - name: OTEL_EXPORTER_OTLP_PROTOCOL
          value: {{ .protocol | quote }}
        {{- $attrs := list }}
        {{- if .environment }}{{ $attrs = append $attrs (printf "deployment.environment.name=%s" .environment) }}{{ end }}
        {{- range $k, $v := .resourceAttributes }}{{ $attrs = append $attrs (printf "%s=%v" $k $v) }}{{ end }}
        {{- if $attrs }}
        - name: OTEL_RESOURCE_ATTRIBUTES
          value: {{ join "," $attrs | quote }}
        {{- end }}
        {{- if .metricExportInterval }}
        - name: OTEL_METRIC_EXPORT_INTERVAL
          value: {{ .metricExportInterval | quote }}
        {{- end }}
        {{- if .consoleExporter }}
        - name: OTLP_CONSOLE_EXPORTER_ENABLED
          value: "true"
        {{- end }}
        {{- range $name, $url := .query }}
        {{- if $url }}
        - name: TelemetryQuery__{{ title $name }}
          value: {{ $url | quote }}
        {{- end }}
        {{- end }}
        {{- end }}
        {{- end }}
        {{- /* backend.halo, each one unless config or secretConfig already sets that setting. */}}
        {{- $taken := lower (include "grif.configEnv" (dict "name" "" "value" $b.config)) }}
        {{- range $path, $_ := $b.secretConfig }}
        {{- $taken = printf "%s\n- name: %s" $taken (include "grif.envName" $path | quote | lower) }}
        {{- end }}
        {{- with $b.halo }}
        {{- if not (contains "\"haloinfinite__mode\"" $taken) }}
        - name: HaloInfinite__Mode
          value: {{ .mode | quote }}
        {{- end }}
        {{- if eq (include "grif.haloMode" $root) "gateway" }}
        {{- /* backend.halo.gateway.url, else the bundled gateway (halo-gateway.enabled), else the shared one. */}}
        {{- if not (contains "\"halogateway__baseurl\"" $taken) }}
        - name: HaloGateway__BaseUrl
          value: {{ include "grif.haloGatewayUrl" $root | quote }}
        {{- end }}
        {{- $apiKey := include "grif.haloGatewayApiKey" $root | fromJson }}
        {{- if and $apiKey.secretName (not (contains "\"halogateway__apikey\"" $taken)) }}
        - name: HaloGateway__ApiKey
          valueFrom:
            secretKeyRef:
              name: {{ $apiKey.secretName }}
              key: {{ required "backend.halo.gateway.apiKey.key (or the halo-gateway.apiKeys entry's secretKey) is required with its secretName" $apiKey.key }}
        {{- end }}
        {{- /* Else the bundled gateway's admin page on its public host (halo-gateway.ingressRoute.host). */}}
        {{- $adminUrl := .gateway.adminUrl }}
        {{- if and (not $adminUrl) (include "grif.haloGatewayInRelease" $root) }}
        {{- with dig "ingressRoute" "host" "" (index $root.Values "halo-gateway") }}{{ $adminUrl = printf "https://%s/admin" . }}{{ end }}
        {{- end }}
        {{- if and $adminUrl (not (contains "\"halogateway__adminurl\"" $taken)) }}
        - name: HaloGateway__AdminUrl
          value: {{ $adminUrl | quote }}
        {{- end }}
        {{- end }}
        {{- end }}
        {{- include "grif.configEnv" (dict "name" "" "value" $b.config) | trim | nindent 8 }}
        {{- range $path, $ref := $b.secretConfig }}
        {{- $r := ternary (dict "key" $ref) $ref (kindIs "string" $ref) }}
        - name: {{ include "grif.envName" $path | quote }}
          valueFrom:
            secretKeyRef:
              name: {{ include "grif.secretName" (dict "secretName" $r.secretName "root" $root) }}
              key: {{ required (printf "backend.secretConfig.%s: key is required" $path) $r.key }}
              {{- if $r.optional }}
              optional: true
              {{- end }}
        {{- end }}
        {{- with $b.extraEnv }}
        {{- toYaml . | nindent 8 }}
        {{- end }}
      {{- with $b.extraEnvFrom }}
      envFrom:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      volumeMounts:
        - name: state
          mountPath: {{ $b.persistence.mountPath }}
        - name: tmp
          mountPath: /tmp
        {{- if $b.dataProtection.certificate.secretName }}
        - name: data-protection-certificate
          mountPath: {{ $b.dataProtection.certificate.mountPath }}
          readOnly: true
        {{- end }}
        {{- if $b.googleCredentials.secretName }}
        - name: google-credentials
          mountPath: {{ $b.googleCredentials.mountPath }}
          readOnly: true
        {{- end }}
        {{- with $b.extraVolumeMounts }}
        {{- toYaml . | nindent 8 }}
        {{- end }}
  volumes:
    - name: state
      {{- if .state }}
      persistentVolumeClaim:
        claimName: {{ $b.persistence.existingClaim | default $b.persistence.claimName }}
      {{- else }}
      emptyDir: {}
      {{- end }}
    - name: tmp
      emptyDir: {}
    {{- if $root.Values.migrations.enabled }}
    # Shared by the migration wait steps.
    - name: migrations-work
      emptyDir: {}
    {{- end }}
    {{- if $b.dataProtection.certificate.secretName }}
    - name: data-protection-certificate
      secret:
        secretName: {{ $b.dataProtection.certificate.secretName }}
    {{- end }}
    {{- if $b.googleCredentials.secretName }}
    - name: google-credentials
      secret:
        secretName: {{ $b.googleCredentials.secretName }}
        {{- if $b.googleCredentials.optional }}
        optional: true
        {{- end }}
    {{- end }}
    {{- with $b.extraVolumes }}
    {{- toYaml . | nindent 4 }}
    {{- end }}
  {{- with $p.nodeSelector }}
  nodeSelector:
    {{- toYaml . | nindent 4 }}
  {{- end }}
  {{- with $p.tolerations }}
  tolerations:
    {{- toYaml . | nindent 4 }}
  {{- end }}
  {{- with $p.affinity }}
  affinity:
    {{- toYaml . | nindent 4 }}
  {{- end }}
{{- end }}
