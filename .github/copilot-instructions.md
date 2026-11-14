# Copilot instructions for GrifballWebApp

The full agent guide is [AGENTS.md](../AGENTS.md): the repository's layout, the backend's architecture, the commands
and the rules. Read it first. What follows is the part every change and every review hinges on, and what Copilot's own
environment has.

## Copilot's environment

[copilot-setup-steps.yml](workflows/copilot-setup-steps.yml) runs before the agent starts: the .NET 10 SDK with
`dotnet-ef` and the repository's local tools, the NuGet packages restored, Node.js with the client's packages
(`npm ci`), and the client's own Angular CLI on the `PATH` as `ng`. The runner has Docker, so the integration tests'
Testcontainers database servers work.

## The rules reviews check

- **Clean Architecture.** Dependencies point inwards: Domain ← Application ← Infrastructure ← Server. The Domain uses
  no other project of the solution, the Application only the Domain; Infrastructure implements the Application's ports;
  controllers stay thin and call Application services. `GrifballWebApp.Architecture.Test` enforces it.
- **Domain-driven design.** The Domain is split into bounded contexts (`League`, `LateLeague`, `Archive`, `Players`,
  `Halo`, `Shared`); a new crossing between them goes on `ContextCrossings.Allowed`, as a review decision. Models are
  aggregates: private constructors with static factories, private setters, read-only collections, and methods on the
  root that own the rules. Rules go on the aggregate, not in services. See
  [docs/architecture.md](../docs/architecture.md).
- **Migrations.** A model change needs a migration in each of the three migrations projects. A migration already on
  master is never edited, deleted, renamed or regenerated.
- **Tests.** Every PR has them. NUnit 4 and NSubstitute (never Moq). A test that touches a real database server, the
  hosted app, Discord or the network goes in `GrifballWebApp.Integration.Test`; every other test goes in its layer's
  unit test project. No new SonarCloud issues.
- **Generated code.** `grifballwebapp.client/src/generated` is regenerated (`npm run generate:api` from `openapi.json`;
  the SignalR client from the hubs), never edited.
- **Frontend.** camelCase folders and files; feature folders with the import rules in
  [docs/frontend.md](../docs/frontend.md); standalone, OnPush, zoneless components with signals, `input()`/`output()`,
  `inject()` and native control flow (`@if`, `@for`).
- **Docs** go in `docs/`, never the repository root.
