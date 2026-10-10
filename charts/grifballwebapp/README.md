# grifballwebapp Helm chart

Deploys GrifballWebApp: the frontend (nginx with the Angular build), the backend (ASP.NET Core) and,
optionally, its database: SQL Server (the default) or PostgreSQL. A migration Job backs up the database when it has migrations to apply, then
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

Kubernetes 1.34 to 1.37 - the versions upstream supports - are tested (`kubeVersion` in `Chart.yaml`
refuses older): every end-to-end scenario on 1.36, the broadest on 1.34 and 1.37 too, and every CI values
file against both ends' API schemas. The chart uses nothing newer than 1.23's APIs, but only tested
versions are claimed.

The app images are built for linux/amd64 and linux/arm64. SQL Server's image is amd64 only, so the
chart's SQL Server runs on amd64 nodes (`mssql.nodeSelector`); everything else, the chart's PostgreSQL
included, runs on either. The third-party images the chart uses by default (SQL Server, PostgreSQL,
busybox, sql_exporter) are pinned by digest.

## One release per namespace

The frontend's nginx proxies `/api` to `grifballwebapp-server:5295` (`grifballwebapp.client/default.conf`),
so object names are fixed rather than prefixed with the release name: `grif-backend`, `grif-frontend`,
`grif-mssql`, `sqlserver`, `grif-postgres`, `postgres`, `grifballwebapp-server`. Install each environment
in its own namespace. Volume claim names (`backend.persistence.claimName`, `mssql.persistence.claimName`,
`mssql.backup.claimName`, `postgres.persistence.claimName`, `migrations.backup.volume.claimName`) can be
set, or `existingClaim` used, to adopt volumes that already exist.

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

With PostgreSQL the database password is `POSTGRES_PASSWORD` instead of `SA_PASSWORD` (`postgres.password`,
and `database.password` by default).

`secret.create` with `secret.stringData` makes the chart create it, for trying the chart out.

## App settings

`backend.config` takes any appsettings.json setting, nested the same way; each one becomes an
environment variable (`Discord__DraftChannel`, `GoogleSheets__Sheets__0__Name`), which ASP.NET Core
reads ahead of appsettings.json. `backend.secretConfig` does the same from Secrets. values.yaml lists
every setting the app reads.

**Quote Discord channel IDs and other large numbers.** Helm reads unquoted numbers as floating point,
which rounds anything above 2^53; the chart fails rather than deploy a rounded ID.

## Database

`database.provider` picks the database: `sqlserver` (the default) or `postgres`. The backend's
provider (`Database__Provider`), its connection string, the migration Job's bundle and tools, backups
and sql-exporter's queries all follow it. Each provider has a server the chart can run, and only the
selected provider's runs - so switching is the one value (a new release; the chart doesn't move data
between providers):

- **The chart's SQL Server** (`mssql.enabled: true`, the default): data on `mssql.persistence`, backups on
  `mssql.backup` (a claim from a storage class, an NFS export, or an existing claim).
- **The chart's PostgreSQL** (`database.provider: postgres`; `postgres.enabled` is true by default):
  PostgreSQL 18 (Alpine, pinned by digest) as its own user on a read-only root, data on
  `postgres.persistence` (its `<major>/docker` folder, so a new major refuses the old data rather than
  starting empty beside it), the superuser's password from `postgres.password` (`POSTGRES_PASSWORD`),
  read when the data is first created. `postgres.settings` are passed as `postgres -c name=value`.
- **Your own** (`mssql.enabled: false` or `postgres.enabled: false`): set `database.host`, and `port`,
  `user` and `password` unless the provider's defaults do (1433, `sa`, `SA_PASSWORD`; 5432, `postgres`,
  `POSTGRES_PASSWORD`). SQL Server writes the migrations' backups itself, to `migrations.backup.directory`
  on its side; PostgreSQL's are written by the chart's pods (below), so they need no access to its disk.

```yaml
database:
  provider: postgres   # the chart's PostgreSQL, its password POSTGRES_PASSWORD in grif-secrets
```

The backend's connection string is built from `database` (password from its Secret; `options`
appended, SQL Server's `Encrypt=True;TrustServerCertificate=True` by default, none for PostgreSQL), or
read whole from `database.connectionString`.

**Least-privilege logins** (`database.logins.enabled`, off by default). By default everything connects
as `database.user` (`sa` or `postgres`), the administrator. With logins on, the migration Job - still
the administrator - creates and keeps in sync, on every run, passwords from your Secret included:

| Login | Used by | Can |
| --- | --- | --- |
| `grif_app` (`logins.app`) | the backend and its wait for the migrations | read and write the database's data - SQL Server: `db_datareader`, `db_datawriter`; PostgreSQL: `SELECT`, `INSERT`, `UPDATE`, `DELETE` on every table and use of its sequences, in every schema (the row history tables only read: a trigger refuses writes by anyone but their owner); not change its schema (no `CREATE`, owns nothing) or the server |
| `grif_monitor` (`logins.monitoring`) | sql-exporter (with `sqlExporter.enabled`, unless `sqlExporter.user` is set) | SQL Server: `VIEW SERVER STATE`, `VIEW ANY DEFINITION`, not open the database; PostgreSQL: `pg_monitor`, not read the app's tables |

The backend pod then holds no administrator password. Backup retention and the scheduled backup still
run as the administrator. The passwords go to SQL Server through a file in the Job's scratch volume, run
with variable substitution off, never on a command line; its password policy applies. PostgreSQL's psql
reads them from the environment itself (`\getenv`) and quotes them (`format('%L')`); the statement that
sets one reaches the server in plain text, so keep `log_statement` below `ddl` on an external server.
On PostgreSQL the grants are made again after every migration, so tables a new migration adds are
covered.

## Migrations

One Job changes the schema; nothing else does (`migrations`, on by default; the app's own
`ApplyMigrations` and `CreateDatabase` stay off). The same pattern as GitLab's chart: one migrator,
and app pods that wait for it. On every install and upgrade the Job:

1. waits for the database;
2. compares the image's `/app/migrations.txt` with the database's `__EFMigrationsHistory`;
3. on a first deploy (no database yet), restores the newest backup from a folder if
   `migrations.restore` is on (below);
4. if the database exists and lacks some, backs it up (`migrations.backup`) - SQL Server writes the
   file, so the folder is on its side; PostgreSQL's is written by `pg_dump` in the step;
5. runs your own steps (`migrations.extraInitContainers`);
6. applies the pending migrations with the image's bundle (`/app/efbundle`), creating the database if
   it's missing.

PostgreSQL uses the image's `/app/postgres/efbundle` and `/app/postgres/migrations.txt`.

With nothing pending, steps 4 to 6 do nothing. Backend pods have two init containers that only wait:
they start the app as soon as the database has every migration in their image, and say what they're
waiting for until then (`kubectl logs <pod> -c wait-for-migrations`). They never change the schema, so
a deploy that skips the Job leaves the backend waiting rather than migrating without a backup.

**Argo CD.** The Job is a normal resource, not a hook, in sync wave -1: after the chart's Secret and the
network policies (-3) and the database server and backup volume (-2), before the app (0). The backend
changes only once the Job has succeeded; if it fails, the sync stops and the running backend is left as
it was. The same holds on a first sync, where the Job creates the database once the server is up.

**Helm.** Waves don't apply: the Job and the new backend start together, and the backend waits.

**Re-running.** A Job can't be changed once created, so its name carries a hash of its spec and the
release revision: every `helm upgrade` makes a new Job and removes the previous one. Argo CD renders
the same revision every time, so the same spec keeps the same Job; delete it and Argo recreates it
(after restoring a backup, say). A failed Job isn't retried (`migrations.backoffLimit`): fix the cause,
delete it, sync again.

Every step runs in the backend image being deployed, which carries the bundles, their migration lists,
sqlcmd (Microsoft's go-sqlcmd) and PostgreSQL's client tools (`psql`, `pg_dump`, `pg_restore`), so there
is no other image to pull. The password reaches sqlcmd as `SQLCMDPASSWORD` and libpq as `PGPASSWORD`,
never on a command line. The chart needs a backend image built with sqlcmd (from this chart's first
version on), and for PostgreSQL one with its bundle and client.

Each step's script can be replaced (`migrations.scripts`); `extraEnv`, annotations,
labels, security contexts, scheduling and resources are values. `migrations.enabled: false` leaves
migrating to the app (`ApplyMigrations`, `CreateDatabase`), without backups.

**PostgreSQL's backups** are written by the chart's pods, not the server: `pg_dump -Fc` to
`<database>_<date>_<time>.dump`, read back with `pg_restore --list` before it takes its name. They go to
`migrations.backup.volume` (a claim from a storage class, an NFS export as `<namespace>-pg-nfs-pv`, or an
existing claim), mounted at `/backup` by the Job's backup step and the CronJobs below, in
`/backup/<namespace>` unless `migrations.backup.directory` says otherwise. The pods write as the backend
image's user (1654) with that fsGroup, which makes a provisioned volume writable; an NFS export has to
let that user write. An external PostgreSQL needs nothing more: the backups never touch its disk.

**Scheduled backups.** The Job backs up only when a migration is pending, so a database whose schema
rarely changes is rarely backed up. `migrations.backup.scheduled.enabled` adds a nightly CronJob
(`schedule`, default `0 3 * * *`) that writes a backup to the same folder under the same
`<database>_<date>_<time>` names, then verifies it - SQL Server's copy-only and checksummed
(`RESTORE VERIFYONLY`), PostgreSQL's with `pg_restore --list`; retention below covers these too. A
database that doesn't exist yet is skipped. With SQL Server, like retention, it mounts nothing.

**Old backups.** Each migration leaves a backup, and nothing deletes them unless
`migrations.backup.retention.enabled`: a nightly CronJob that deletes old ones, keeping the newest
`keepLast` and any younger than `keepDays`; anything else in the folder is never touched. SQL Server
deletes its own: only backups it recorded writing (its msdb history) named `<database>_*.bak` in the
backup folder, with `xp_delete_file`, which only deletes SQL Server backups. It mounts no volume, so it
works the same against an external SQL Server. PostgreSQL's mounts the backup volume and considers only
`pg_dump` archives named `<database>_YYYYMMDD_HHMMSS.dump`, ranked and aged by the date and time in
their name; it doesn't connect to the database.

**Restore** (`migrations.restore.enabled`, off by default). On a release's first deploy - its database
doesn't exist yet - the Job restores the newest backup from a folder, then migrates it: a preview or a
test environment starting from another's data. Later deploys find the database and keep it. Only the
chart's own names count (`<database>_YYYYMMDD_HHMMSS.bak` or `.dump`, newest by name); hand-named files
in the folder are ignored, and finding none fails the Job. The restored database isn't backed up again.

- SQL Server reads the file itself, so the folder (`directory`, default `/var/opt/mssql/restore`) is on
  its side: mount one with `mssql.extraVolumes` and `extraVolumeMounts`, or point at its own backup
  folder. Each of the backup's files goes to this server's default data or log folder; a restore that
  fails part way is dropped, so the next run tries again. With logins on, the restored database's
  `grif_app` user is mapped to this server's login.
- PostgreSQL's folder is on `migrations.restore.volume` (any pod volume source: an NFS export, another
  claim), which the restore step mounts read-only at `/restore` (`directory`, default `/restore`). The
  dump is restored with `pg_restore --no-owner --no-privileges` into a new database under a temporary
  name, renamed once complete; the login sync grants the app's rights afterwards. The row history comes
  with it, and its triggers after the data, so restoring adds none; they then run as this administrator,
  who owns everything restored.

```yaml
migrations:
  restore:
    enabled: true
    directory: /restore/grif-test
    volume:
      nfs: { server: nas.example, path: /pg-backups, readOnly: true }
```

The Job, the CronJobs and the backend run as the backend image's non-root user (1654), checked by
Kubernetes (`runAsNonRoot`); SQL Server runs as its own (10001), PostgreSQL as its own (70). Only the
database servers' permission-fixing init containers and the frontend's nginx start as root.

The backend runs one replica: the Discord bot's gateway connection, the background queue and events
services, and Grunt's token file on a ReadWriteOnce volume all assume a single instance. The migrations
don't: more backend pods would only be more waiters.

## Metrics

`sqlExporter.enabled` runs [sql_exporter](https://github.com/burningalchemist/sql_exporter) against the database (the
chart's or an external one), with the provider's collector: for SQL Server connections, deadlocks,
errors, page life expectancy, batch requests, IO stalls, memory (`mssql_standard`); for PostgreSQL
connections by state, database sizes, commits and rollbacks, cache hits and reads, deadlocks,
conflicts, temporary bytes, locks by mode and the oldest open transaction (`pg_standard`). The login
defaults to the monitoring login (`database.logins`), else `database`'s; one only needs `VIEW SERVER
STATE` and `VIEW ANY DEFINITION` on SQL Server, `pg_monitor` on PostgreSQL (`sqlExporter.user`,
`password`). PostgreSQL is read over pgx from its `postgres` database, `sslmode=prefer` unless
`dsnOptions` says otherwise. `sqlExporter.serviceMonitor` adds a
Prometheus Operator ServiceMonitor; `extraCollectorFiles` and `collectors` add queries of your own.

## Network policies

On by default (`networkPolicy.enabled`): each of the chart's pods gets a NetworkPolicy allowing only
what it needs.

| Pod | In | Out |
| --- | --- | --- |
| frontend | port 80 from anyone (`frontend.from`) | DNS, the backend |
| backend | the frontend | DNS, the database, HTTPS to the internet (`backend.httpsTo`: Discord, Halo, Google), the OpenTelemetry endpoint's port |
| SQL Server | backend, migration Job, backup CronJobs, sql-exporter; the LoadBalancer if enabled (`mssql.loadBalancerFrom`) | DNS |
| PostgreSQL | backend, migration Job, scheduled backup, sql-exporter; the LoadBalancer if enabled (`postgres.loadBalancerFrom`) | DNS |
| migration Job | - | DNS, the database |
| backup CronJobs | - | DNS, the database (PostgreSQL's retention: DNS only, it only deletes files) |
| sql-exporter | its metrics port from anyone (`sqlExporter.from`) | DNS, the database |

An external database is allowed anywhere on `database.port` unless `networkPolicy.database.to` says
where. PostgreSQL's backup volume, an NFS export included, is mounted by the kubelet, so it
needs no rule. Every policy takes `extraIngress` / `extraEgress` rules; `networkPolicy.extraPolicies` adds
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

`e2e/run.sh bundled|external|postgres|minimal` runs the chart on a real cluster (the workflow uses k3s in
Docker, with app images built from the checkout): the header of the script lists what it checks.
