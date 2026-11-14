# Agent guide

What an AI agent (Claude Code, Copilot, Codex, ...) needs to work on GrifballWebApp. People start at
[readme.md](readme.md) and [docs/development-setup.md](docs/development-setup.md).

## The repository

- `GrifballWebApp.Domain`, `.Application`, `.Infrastructure`, `.Server`: the .NET 10 backend, in layers (below).
- `GrifballWebApp.Migrations.SqlServer`, `.Postgres`, `.Sqlite`: one EF Core migrations project per database.
- `GrifballWebApp.Seeder`, `GrifballWebApp.Convert`: host apps beside the server (seed data; copy a database between
  providers).
- `DiscordInterfaces`, `DiscordInterfaceSourceGen`, `GrifballWebApp.Analyzers`: the Discord bot's interfaces and their
  source generator, and the solution's own Roslyn analyzers.
- `*.Test`: one unit test project per layer and app, plus `GrifballWebApp.Integration.Test` and
  `GrifballWebApp.Architecture.Test`.
- `grifballwebapp.client`: the Angular frontend.
- `charts/grifballwebapp`: the Helm chart every environment runs from.
- `docs/`: all documentation ([index](docs/README.md)). New docs go there, never in the repository root.

## Backend architecture: Clean Architecture + domain-driven design

Read [docs/architecture.md](docs/architecture.md) before changing backend code. In short:

- **Layers depend inwards only: Domain ← Application ← Infrastructure ← Server.** The Domain uses no other project
  of the solution; the Application uses only the Domain; Infrastructure implements the Application's ports (interfaces
  such as `IApplicationDbContext`, `IOutbox`); the Server is the host (controllers, hubs, Discord commands, MCP).
  Controllers stay thin and call Application services.
- **The Domain is split into bounded contexts** (`League`, `LateLeague`, `Archive`, `Players`, `Halo`, and the
  `Shared` kernel). A type may name another context's type only where `ContextCrossings.Allowed` lists it; adding a
  crossing is a review decision, so first look for a way through the type's own context or `Shared`.
- **Models are DDD aggregates** (`[AggregateRoot]`, `[AggregatePart(typeof(Root))]`; `Season` is the reference): a
  private constructor for EF and named static factories, private setters, private collections exposed read-only, and
  methods on the root that own the rules and refuse invalid changes. Parts are reached through their root (no public
  `DbSet`). Business rules go on the aggregate, not in services.
- **Provider gaps** (SQL Server, Postgres and SQLite translating something differently) are fixed in the
  Infrastructure's database layer, never by rewriting the Application's queries.

`GrifballWebApp.Architecture.Test` enforces the layers, the context crossings and the aggregate boundaries; run it
after moving code between projects or namespaces.

## Commands

```sh
dotnet tool restore && dotnet restore         # once
dotnet build GrifballWebApp.slnx
dotnet test GrifballWebApp.Domain.Test        # or any one test project; the integration tests need Docker
GRIF_TEST_DATABASE=Postgres dotnet test GrifballWebApp.Integration.Test   # SqlServer (default), Postgres or Sqlite

cd grifballwebapp.client
npm ci --force
npm run lint                                  # ESLint + feature boundaries (verify-boundaries.mjs)
npm run test:ci                               # Karma/Jasmine, headless, with coverage
npx ng build
```

To run the app without any credentials (Discord, OAuth, Halo), set `Preview:Enabled=true`: see
[docs/development-setup.md](docs/development-setup.md#running-without-credentials). On Windows the
[`start-env` skill](.agents/skills/start-env/SKILL.md) starts everything.

## Rules

### Database and migrations

- A model change needs a migration in **all three** migrations projects ([docs/database.md](docs/database.md#migrations)
  has the commands); a test fails for a missing one.
- **Never edit, delete, rename or regenerate a migration that is already on master.** Add a new one. The snapshot
  files are expected to change. The "Master migrations unchanged" check fails a PR that does. A PR's own new
  migrations can be regenerated freely.

### Tests

- Every PR includes tests. NUnit 4, and **NSubstitute** for test doubles (never Moq).
- **Where a test goes:** a test that touches something real outside the process (a database server via
  Testcontainers, the hosted app via `WebApplicationFactory`, Discord transports, the network) goes in
  `GrifballWebApp.Integration.Test`. Everything else is a unit test in the test project of the layer whose type it
  tests (`GrifballWebApp.Domain.Test`, `.Application.Test`, `.Infrastructure.Test`, `.Server.Test`, ...). An in-memory
  or temp-file SQLite `GrifballContext` is fine in a unit test (`SqliteContextFixture` in
  `GrifballWebApp.Infrastructure.Test`). Details: [docs/ci-cd/dotnet-tests.md](docs/ci-cd/dotnet-tests.md).
- A rule about the code's shape belongs in `GrifballWebApp.Architecture.Test`.
- Test classes: `[TestFixture]`, `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]`; names say what is tested and
  what should happen (`Method_Should_Result_When_Condition` or `Method_Result_ForCase`); `Assert.EnterMultipleScope()`
  for related assertions.
- Coverage goes to Codecov and SonarCloud; a PR adds no new SonarCloud issues.

### Generated code

`grifballwebapp.client/src/generated` is generated and checked in CI: never edit it by hand. After an API change,
update `grifballwebapp.client/openapi.json` and run `npm run generate:api`; after a hub change, regenerate the SignalR
client. Both are described in [docs/development-setup.md](docs/development-setup.md#tests-and-checks).

### Frontend

The client is split into `app/features/<area>`, `app/core` and `app/shared`, with import rules lint enforces
([docs/frontend.md](docs/frontend.md)). Folder and file names are **camelCase**. Write modern Angular:

- Standalone components (don't write `standalone: true`, it's the default), `ChangeDetectionStrategy.OnPush`, and no
  zone.js: the app is zoneless.
- Signals for state, `computed()` for derived state, `set`/`update` (never `mutate`); `input()`/`output()` functions,
  not decorators; `inject()`, not constructor injection.
- Native control flow (`@if`, `@for`, `@switch`); `class`/`style` bindings, not `ngClass`/`ngStyle`; host bindings in
  the decorator's `host` object, not `@HostBinding`/`@HostListener`.
- Reactive forms; lazy-loaded feature routes; `NgOptimizedImage` for static images; no `any` (use `unknown`).
- When Angular has a migration schematic for a change, use it rather than converting by hand.

### Git

- Start from an up-to-date `origin/master` (`git fetch` first; `origin` is
  `https://github.com/PlayGrifball/GrifballWebApp.git`), one PR per change. Phased work is one PR with a commit per
  phase.
- All checks must be green before merge, Codecov patch coverage included.
