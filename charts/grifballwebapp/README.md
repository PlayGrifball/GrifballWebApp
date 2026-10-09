# grifballwebapp Helm chart

Deploys GrifballWebApp: the frontend (nginx with the Angular build), the backend (ASP.NET Core) and,
optionally, SQL Server. Before every upgrade a hook backs up the database and applies the EF Core
migrations of the backend image being deployed.

Published to `oci://ghcr.io/playgrifball/charts/grifballwebapp` by the
[Helm chart workflow](../../.github/workflows/helm-chart.yml) whenever `version` in `Chart.yaml` changes on
master.

```sh
helm install grif oci://ghcr.io/playgrifball/charts/grifballwebapp --version 0.1.0 \
  -n grif --create-namespace -f my-values.yaml
```

Every value is described in [values.yaml](values.yaml).

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
  deploy hook's backup is written by that server, to `deployHook.backup.directory` on its side.

The backend's connection string is built from `database` (password from its Secret), or read whole from
`database.connectionString`.

## The deploy hook

Runs as a Helm `pre-upgrade` hook and an Argo CD `PreSync` hook:

1. waits for the database (on a first Argo CD sync, before SQL Server exists, it does nothing);
2. compares the image's `/app/migrations.txt` with `__EFMigrationsHistory`;
3. if migrations are pending, scales backend and frontend to 0 (`deployHook.scaleDown`);
4. backs up the database (`deployHook.backup`);
5. runs `/app/efbundle`.

A failure stops the upgrade. A first `helm install` runs no hook: the backend applies migrations at
startup (`backend.config.ApplyMigrations`, `CreateDatabase`).

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

Bump `version` in Chart.yaml with every change; the workflow fails a pull request that changes the chart
without it.
