# grifballwebapp Helm chart

Deploys GrifballWebApp: the frontend (nginx with the Angular build), the backend (ASP.NET Core) and,
optionally, SQL Server. A migration Job backs up the database when it has migrations to apply, then
applies them with the EF Core bundle of the backend image being deployed; the backend waits for it.

Published to `oci://ghcr.io/playgrifball/charts/grifballwebapp` by the
[Helm chart workflow](../../.github/workflows/helm-chart.yml) whenever the chart changes on master, as
`<major>.<minor>.<run number>` (major and minor from `Chart.yaml`), and signed keyless with cosign:

```sh
cosign verify ghcr.io/playgrifball/charts/grifballwebapp:<version> \
  --certificate-identity-regexp '^https://github\.com/PlayGrifball/GrifballWebApp/\.github/workflows/helm-chart\.yml@refs/heads/master$' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Quick start

The smallest working install - the chart's SQL Server, a Kubernetes Ingress, defaults for the rest -
is [ci/minimal-values.yaml](ci/minimal-values.yaml), which the end-to-end test installs as is:

```yaml
mssql:
  acceptEula: true                # required to run the chart's SQL Server

backend:
  config:
    BaseUrl: https://grifball.example.com
    Discord:
      # Quote IDs. DraftChannel is required; the queue and events services stop without the others.
      DraftChannel: "123456789012345678"
      QueueChannel: "123456789012345679"
      EventsChannel: "123456789012345680"
  # Setting: key in the grif-secrets Secret.
  secretConfig:
    Discord:ClientId: DiscordClientId
    Discord:ClientSecret: DiscordClientSecret
    Discord:Token: DiscordToken

ingress:
  enabled: true
  className: nginx
  hosts: [grifball.example.com]
  tls:
    - secretName: grifball-tls
      hosts: [grifball.example.com]
```

with the Secret it reads:

```sh
kubectl create namespace grif
kubectl -n grif create secret generic grif-secrets \
  --from-literal=SA_PASSWORD='<a strong password>' \
  --from-literal=DiscordClientId=... \
  --from-literal=DiscordClientSecret=... \
  --from-literal=DiscordToken=...
helm install grif oci://ghcr.io/playgrifball/charts/grifballwebapp --version <version> -n grif -f values.yaml
```

That runs the frontend, the backend, SQL Server with data and backup volumes from the default storage
class, a network policy per pod, and the migration Job, which creates the database on the first install
and later backs it up before applying new migrations. Halo Infinite
stat pulls also need `ClientConfiguration:ClientId`, `ClientSecret` and `RedirectUrl` in
`secretConfig`; Google Sheets imports need `backend.googleCredentials` and `GoogleSheets:Sheets`.

Every value is described in [values.yaml](values.yaml); [values.schema.json](values.schema.json) checks them, so
a misspelled or unknown key, or a value of the wrong type, fails the install instead of being ignored.

## Platforms

The app images are built for linux/amd64 and linux/arm64. SQL Server's image is amd64 only, so the
chart's SQL Server runs on amd64 nodes (`mssql.nodeSelector`); everything else runs on either. The
third-party images the chart uses by default (SQL Server, busybox, sql_exporter) are pinned by digest.

## One release per namespace

The frontend's nginx proxies `/api` to `grifballwebapp-server:5295` (`grifballwebapp.client/default.conf`),
so object names are fixed rather than prefixed with the release name: `grif-backend`, `grif-frontend`,
`grif-mssql`, `sqlserver`, `grifballwebapp-server`. Install each environment in its own namespace.
Volume claim names (`backend.persistence.claimName`, `mssql.persistence.claimName`,
`mssql.backup.claimName`) can be set, or `existingClaim` used, to adopt volumes that already exist.

## Secrets

The chart reads secrets from Secrets you create; it never needs them in values. `secret.name` (default
`grif-secrets`) is where they are looked up unless an entry names its own:

```yaml
mssql:
  acceptEula: true             # required with the chart's SQL Server
  saPassword: { key: SA_PASSWORD }
backend:
  secretConfig:
    "Discord:ClientId": DiscordClientId          # key in secret.name
    "Discord:ClientSecret": DiscordClientSecret
    "Discord:Token": DiscordToken
    "ClientConfiguration:ClientId": { secretName: halo, key: client-id }
```

`secret.create` with `secret.stringData` makes the chart create it, for trying the chart out.

## App settings

`backend.config` takes any appsettings.json setting, nested the same way; each one becomes an
environment variable (`Discord__DraftChannel`, `GoogleSheets__Sheets__0__Name`), which ASP.NET Core
reads ahead of appsettings.json. `backend.secretConfig` does the same from Secrets. values.yaml lists
every setting the app reads.

**Quote Discord channel IDs and other large numbers.** Helm reads unquoted numbers as floating point,
which rounds anything above 2^53; the chart fails rather than deploy a rounded ID.

## Database

- **The chart's SQL Server** (`mssql.enabled: true`, the default): data on `mssql.persistence`, backups on
  `mssql.backup` (a claim from a storage class, an NFS export, or an existing claim).
- **Your own** (`mssql.enabled: false`): set `database.host`, `port`, `user` and `password`. The
  migrations' backup is written by that server, to `migrations.backup.directory` on its side.

The backend's connection string is built from `database` (password from its Secret), or read whole from
`database.connectionString`.

## Migrations

One Job changes the schema; nothing else does (`migrations`, on by default; the app's own
`ApplyMigrations` and `CreateDatabase` stay off). The same pattern as GitLab's chart: one migrator,
and app pods that wait for it. On every install and upgrade the Job:

1. waits for the database;
2. compares the image's `/app/migrations.txt` with the database's `__EFMigrationsHistory`;
3. if the database exists and lacks some, backs it up (`migrations.backup`) - SQL Server writes the
   file, so the folder is on its side;
4. runs your own steps (`migrations.extraInitContainers`);
5. applies the pending migrations with the image's bundle (`/app/efbundle`), creating the database if
   it's missing.

With nothing pending, steps 3 to 5 do nothing. Backend pods have two init containers that only wait:
they start the app as soon as the database has every migration in their image, and say what they're
waiting for until then (`kubectl logs <pod> -c wait-for-migrations`). They never change the schema, so
a deploy that skips the Job leaves the backend waiting rather than migrating without a backup.

**Argo CD.** The Job is a normal resource, not a hook, in sync wave -1: after the chart's Secret and the
network policies (-3) and SQL Server (-2), before the app (0). The backend changes only once the Job has
succeeded; if it fails, the sync stops and the running backend is left as it was. The same holds on a
first sync, where the Job creates the database once SQL Server is up.

**Helm.** Waves don't apply: the Job and the new backend start together, and the backend waits.

**Re-running.** A Job can't be changed once created, so its name carries a hash of its spec and the
release revision: every `helm upgrade` makes a new Job and removes the previous one. Argo CD renders
the same revision every time, so the same spec keeps the same Job; delete it and Argo recreates it
(after restoring a backup, say). A failed Job isn't retried (`migrations.backoffLimit`): fix the cause,
delete it, sync again.

Every step runs in the backend image being deployed, which carries the bundle, its migration list and
sqlcmd (Microsoft's go-sqlcmd), so there is no other image to pull; the password reaches sqlcmd as
`SQLCMDPASSWORD`, never on a command line. The chart needs a backend image built with sqlcmd (from
this chart's first version on).

Each step's script can be replaced (`migrations.scripts`); `extraEnv`, annotations,
labels, security contexts, scheduling and resources are values. `migrations.enabled: false` leaves
migrating to the app (`ApplyMigrations`, `CreateDatabase`), without backups.

**Old backups.** Each migration leaves a backup, and nothing deletes them unless
`migrations.backup.retention.enabled`: a nightly CronJob that has SQL Server delete its own old ones,
keeping the newest `keepLast` and any younger than `keepDays`. It only considers backups SQL Server
recorded writing (its msdb history) named `<database>_*.bak` in the backup folder, and deletes with
`xp_delete_file`, which only deletes SQL Server backups - anything else in the folder is never touched.
It mounts no volume, so it works the same against an external SQL Server.

The Job, the CronJob and the backend run as the backend image's non-root user (1654), checked by
Kubernetes (`runAsNonRoot`); SQL Server runs as its own (10001). Only SQL Server's permission-fixing init
container and the frontend's nginx start as root.

The backend runs one replica: the Discord bot's gateway connection, the background queue and events
services, and Grunt's token file on a ReadWriteOnce volume all assume a single instance. The migrations
don't: more backend pods would only be more waiters.

## Metrics

`sqlExporter.enabled` runs [sql_exporter](https://github.com/burningalchemist/sql_exporter) against the database (the
chart's or an external one): connections, deadlocks, errors, page life expectancy, batch requests, IO
stalls, memory. The login defaults to `database`'s; a monitoring login only needs `VIEW SERVER STATE`
and `VIEW ANY DEFINITION` (`sqlExporter.user`, `password`). `sqlExporter.serviceMonitor` adds a
Prometheus Operator ServiceMonitor; `extraCollectorFiles` and `collectors` add queries of your own.

## Network policies

On by default (`networkPolicy.enabled`): each of the chart's pods gets a NetworkPolicy allowing only
what it needs.

| Pod | In | Out |
| --- | --- | --- |
| frontend | port 80 from anyone (`frontend.from`) | DNS, the backend |
| backend | the frontend | DNS, the database, HTTPS to the internet (`backend.httpsTo`: Discord, Halo, Google), the OpenTelemetry endpoint's port |
| SQL Server | backend, migration Job, sql-exporter; the LoadBalancer if enabled (`mssql.loadBalancerFrom`) | DNS |
| migration Job | - | DNS, the database |
| sql-exporter | its metrics port from anyone (`sqlExporter.from`) | DNS, the database |

An external database is allowed anywhere on `database.port` unless `networkPolicy.database.to` says
where. Every policy takes `extraIngress` / `extraEgress` rules; `networkPolicy.extraPolicies` adds
whole policies of your own, and `extraObjects` any other objects.

## Argo CD and Image Updater

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
spec:
  source:
    repoURL: ghcr.io/playgrifball/charts
    chart: grifballwebapp
    targetRevision: 0.1.0
    helm:
      valueFiles: [...]
```

Image Updater can write digests back with its Helm target: `backend.image.tag` and `frontend.image.tag`
accept `tag`, `tag@sha256:...` or `sha256:...`, and `*.image.digest` takes a digest on its own.

## Developing

```sh
helm lint charts/grifballwebapp --strict -f charts/grifballwebapp/ci/full-values.yaml
helm plugin install https://github.com/helm-unittest/helm-unittest   # once; add --verify=false on Helm 4
helm unittest charts/grifballwebapp
```

No version bump needed: master publishes every change as a new patch version. Bump the major or minor
in `Chart.yaml` for changes that need it.

`e2e/run.sh bundled|external|minimal` runs the chart on a real cluster (the workflow uses k3s in
Docker, with app images built from the checkout): the header of the script lists what it checks.
