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
  # Never calls the Kubernetes API.
  automountServiceAccountToken: false
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
        {{- if $root.Values.database.connectionString.secretName }}
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
