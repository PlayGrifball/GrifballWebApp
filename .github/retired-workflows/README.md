# Retired workflows: deploying to Kubernetes from GitHub Actions

These four workflows deployed GrifballWebApp to the homelab's Kubernetes cluster until
September 2026. They live here, outside `.github/workflows/`, so GitHub no longer runs them, and are
kept as a working example of deploying to Kubernetes straight from GitHub Actions.

## What they did

`deploy-to-env.yml` is a reusable workflow (`workflow_call`), called by the other three with an
environment's namespace and secrets:

| Caller | Trigger | Namespace |
| --- | --- | --- |
| `deploy-to-test.yml` | after every successful `Docker` run on `master` (`workflow_run`), or by hand | `grif-test` |
| `deploy-to-staging.yml` | by hand (`workflow_dispatch`) | `grif-staging` |
| `deploy-to-prod.yml` | by hand (`workflow_dispatch`) | `grif-prod` |

On a self-hosted runner inside the cluster (`runs-on: grif-k8s`, an Actions Runner Controller scale
set), it:

1. wrote a kubeconfig from a secret (`KUBECONFIG_DATA_<ENV>`), scoped to one namespace;
2. scaled the backend and frontend to 0, so nothing wrote to the database during the next steps;
3. backed up the database with `sqlcmd` through `kubectl exec` into the `mssql-tools` pod;
4. built the database project and ran `dotnet ef database update` against SQL Server;
5. scaled the backend and frontend back to 1, which pulled the new `:latest` images
   (`imagePullPolicy: Always`).

## Why they were retired

Deploys now happen in the cluster itself, through ArgoCD (see NoahSurprenant/homelab):

- ArgoCD Image Updater follows `:latest` by digest and commits each new digest to git.
- Every ArgoCD sync first runs a PreSync hook: the same database backup, then the EF migrations
  bundle built into the backend image (`/app/efbundle`, see `GrifballWebApp.Server/Dockerfile`).
- test and staging deploy automatically; prod deploys from the Sync button in ArgoCD.

Compared with this approach, nothing outside the cluster needs a kubeconfig or database
credentials, every deploy is a git commit, and the migrations always come from the same build as
the code being deployed.

## Using them as an example

Paths inside still point at `.github/workflows/` (for instance
`uses: ./.github/workflows/deploy-to-env.yml`); move them back there and adjust the secrets,
runner label and namespaces to reuse them.
