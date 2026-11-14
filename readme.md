# Grifball Web App

Plan and run Grifball leagues and tournaments: seasons, signups, availability, drafts, schedules, brackets, Late League
matchmaking through Discord, and match stats pulled from Halo Infinite.

**Build and test**
[![.NET Tests](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/dotnet-tests.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/dotnet-tests.yml)
[![Frontend Tests](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/frontend-tests.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/frontend-tests.yml)
[![Migrations](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/migrations.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/migrations.yml)
[![Docker](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/docker-publish.yml)
[![Helm chart](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/helm-chart.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/helm-chart.yml)
[![Reproducible images](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/reproducible.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/reproducible.yml)

**Quality and security**
[![codecov](https://codecov.io/gh/PlayGrifball/GrifballWebApp/branch/master/graph/badge.svg)](https://codecov.io/gh/PlayGrifball/GrifballWebApp)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=PlayGrifball_GrifballWebApp&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=PlayGrifball_GrifballWebApp)
[![SonarCloud](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/sonarcloud.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/sonarcloud.yml)
[![Trivy](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/trivy.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/trivy.yml)
[![Lighthouse](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/lighthouse.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/lighthouse.yml)
[![Alpine package lock](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/apk-lock.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/apk-lock.yml)

**Deploy and previews**
[![Promote to staging](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/promote-to-staging.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/promote-to-staging.yml)
[![Promote to prod](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/promote-to-prod.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/promote-to-prod.yml)
[![Preview smoke tests](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/preview-smoke.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/preview-smoke.yml)
[![Preview label](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/preview-label.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/preview-label.yml)
[![Preview cleanup](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/preview-cleanup.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/preview-cleanup.yml)
[![Copilot Setup Steps](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/copilot-setup-steps.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/copilot-setup-steps.yml)

## Environments

| Environment | URL | What runs there |
| --- | --- | --- |
| Production | https://grifball.xyz | Staging's images, moved by the [Promote to prod](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/promote-to-prod.yml) workflow |
| Staging | https://grif-staging.grifball.xyz | Test's images, moved by the [Promote to staging](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/promote-to-staging.yml) workflow |
| Test | https://grif-test.grifball.xyz | Every build of `master` |
| PR previews | [grif-preview1](https://grif-preview1.grifball.xyz), [2](https://grif-preview2.grifball.xyz), [3](https://grif-preview3.grifball.xyz), [4](https://grif-preview4.grifball.xyz), [5](https://grif-preview5.grifball.xyz), [6](https://grif-preview6.grifball.xyz) | PRs with the `preview` label, one slot each (slots 1-3 with Discord); the PR's comment says which |
| Halo gateway | https://grunt.grifball.xyz/admin | Holds the signed-in Halo account every environment's stats go through |

All of them run on Kubernetes from the [Helm chart](charts/grifballwebapp/README.md)
(`oci://ghcr.io/playgrifball/charts/grifballwebapp`).

## Tech

- **Backend:** .NET 10, ASP.NET Core, SignalR, Entity Framework Core on SQL Server, PostgreSQL or SQLite, Quartz,
  OpenTelemetry, and [Grunt](https://github.com/PlayGrifball/grunt) for Halo Infinite. Domain-driven design inside
  Clean Architecture layers: see [docs/architecture.md](docs/architecture.md).
- **Frontend:** Angular with Angular Material, Tailwind CSS and Luxon, organised by feature: see
  [docs/frontend.md](docs/frontend.md).

## Docs

- [Developer setup](docs/development-setup.md): tools, configuration, running the app and the tests
- [Architecture](docs/architecture.md) and [database](docs/database.md)
- [Everything else](docs/README.md): jobs, Discord commands, the MCP server, telemetry, historical import, CI
- Working on this repo with an AI agent: [AGENTS.md](AGENTS.md)
