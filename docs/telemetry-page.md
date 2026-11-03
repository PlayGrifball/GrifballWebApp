# Telemetry page

`/usermanagement/telemetry`, for sysadmins: the app's own logs, metrics and traces, read back from where it exports
them (OpenTelemetry, `backend.telemetry` in the chart). The backend writes every query and pins it to the app's
`service.name` (`OTEL_SERVICE_NAME`), so the page shows this app's telemetry and nothing else the backends hold; the
browser never talks to Loki, Prometheus or Tempo.

| Tab | Reads | Shows |
|---|---|---|
| Overview | Prometheus (`/api/v1/query_range`, `/api/v1/query`) | Requests, 5xx, p95 response time, p95 database calls, exceptions, memory and CPU per pod; the busiest routes with their 5xx and p95. Health probes are left out |
| Logs | Loki (`/loki/api/v1/query_range`) | Lines newest first, by level (`detected_level`), text (any case) and trace. A line opens to its properties and links to its trace |
| Traces | Tempo (`/api/search`, `/api/v2/traces/{id}`) | Traces by failure and duration; one opens as a waterfall of the app's spans, with their attributes and events, and links to its log lines. `?trace=<id>` opens one directly |

Ranges are 5 minutes to 7 days; logs are 1,000 lines at most.

## Configuration

| Setting | Chart value | Example |
|---|---|---|
| `TelemetryQuery:LokiUrl` | `backend.telemetry.query.lokiUrl` | `http://loki-gateway.loki` |
| `TelemetryQuery:PrometheusUrl` | `backend.telemetry.query.prometheusUrl` | `http://prometheus.monitoring:9090` |
| `TelemetryQuery:TempoUrl` | `backend.telemetry.query.tempoUrl` | `http://tempo.tempo:3200` |

Each one unset turns its tab off (the page says which setting is empty). The chart sets them only with
`backend.telemetry.enabled`, and gives the backend's NetworkPolicy egress to each URL's port;
`networkPolicy.backend.telemetryQuery.<loki|prometheus|tempo>` narrows it (`to`) and corrects the port when the
Service's pods listen on another one (`port`: a Loki gateway Service on 80 runs its pods on 8080). The backends'
own policies must let the backend in.

The queries expect what the app exports: Serilog over OTLP into Loki with structured metadata (`trace_id`,
`SourceContext`...), the ASP.NET Core, .NET runtime and EF Core metrics in Prometheus with a `service_name` label, and
traces in Tempo under `resource.service.name`.
