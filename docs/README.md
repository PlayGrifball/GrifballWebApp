# GrifballWebApp documentation

## Getting started

- [Developer setup](development-setup.md) - Tools, configuration, Halo Infinite modes, running the app and the tests
- [Architecture](architecture.md) - The backend's Clean Architecture layers and domain-driven design, and what enforces them
- [Database](database.md) - Providers, migrations, row history and temporal queries, converting between SQL Server and PostgreSQL
- [Frontend structure](frontend.md) - The client's feature folders, core and shared, and the lint check on what may import what
- [Agent guide](../AGENTS.md) - The rules for AI agents (Claude Code reads it through [CLAUDE.md](../CLAUDE.md), Copilot through [copilot-instructions.md](../.github/copilot-instructions.md))

## Testing and CI

- [.NET tests](ci-cd/dotnet-tests.md) - The test projects, where each kind of test goes, and the CI workflow
- [Frontend testing](frontend-testing.md) - Karma and Jasmine, coverage, and writing client tests
- [Code coverage](code-coverage.md) - Running the tests with coverage
- [Reproducible builds](reproducible-builds.md) - Rebuilding the images byte for byte, their attestations, and checking the live site against the build

## Features

- [Background jobs](jobs.md) - The Quartz jobs, where they run, and the Jobs page
- [Discord commands](discord-commands.md) - The bot's slash commands, who may run each, and adding one
- [MCP server](mcp-server.md) - The MCP server for AI agents, its access tokens and its tools
- [Telemetry page](telemetry-page.md) - The sysadmins' view of the app's own logs, metrics and traces, and its settings
- [Historical import](historical-import.md) - How the app downloads, snapshots and imports the historical archive
- [Password reset](PasswordReset.md) - Sysadmin-made, time-limited reset links for users who sign in with a password
- [SQLite](sqlite.md) - Running on a SQLite file instead of SQL Server or PostgreSQL, and its limits

## Deployment

- [Helm chart](../charts/grifballwebapp/README.md) - Every environment's deployment: values, migrations Job, backups, previews
- [Retired workflows](../.github/retired-workflows/README.md) - How the app was deployed from GitHub Actions before the homelab's Argo CD

New docs go in this folder (never the repository root), with a line here.
