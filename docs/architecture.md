# Backend architecture

The .NET backend follows **Clean Architecture** for its layers and **domain-driven design** inside the Domain. Both
are enforced: `GrifballWebApp.Architecture.Test` reads the compiled assemblies (Mono.Cecil) and the project files, and
fails a PR that breaks a rule, naming the type that crosses and what it reaches.

## Layers

Dependencies point inwards only: **Domain ← Application ← Infrastructure ← Server**.

| Layer | Project | What's in it | May use |
| --- | --- | --- | --- |
| Domain | `GrifballWebApp.Domain` | The model, by bounded context: aggregates, their rules, and their EF configurations | none of the solution's projects (EF Core, a package, is allowed) |
| Application | `GrifballWebApp.Application` | The use cases, by feature (`Seasons`, `Brackets`, `Matchmaking`, ...): services, DTOs, queries, and the ports Infrastructure implements (`IApplicationDbContext`, `IOutbox`, `IDatabaseLocks`, ...) | Domain |
| Infrastructure | `GrifballWebApp.Infrastructure` | `GrifballContext` and the database plumbing (providers, row history, temporal queries, conversion, backups), and the adapters: Halo Infinite, Redis, outbox, Discord, Excel, jobs | Application, Domain, DiscordInterfaces |
| Server | `GrifballWebApp.Server` | The host: controllers and SignalR hubs by feature, Discord commands, the MCP server, telemetry, `Program.cs` | every layer |

`LayerTests` checks both the compiled references and the `.csproj` references, so a new project reference the wrong way
(or to the migrations, the Seeder, DiscordInterfaces from the wrong layer) fails even when it compiles.

The migrations projects (`GrifballWebApp.Migrations.SqlServer`, `.Postgres`, `.Sqlite`) and the host apps beside the
server (`GrifballWebApp.Seeder`, `GrifballWebApp.Convert`) sit outside the layers and use Infrastructure.

## Where new code goes

- **A rule about the data** (what a season may be changed to, who may join a team): a method on the aggregate in the
  Domain, not a check in a service.
- **A use case** (sign a player up, schedule a match): a service in the Application's feature folder, working on
  aggregates through `IApplicationDbContext`. It loads the aggregate, calls its methods, saves.
- **Something outside the process** (a database feature, an HTTP API, Discord, Redis): a port (interface) in the
  Application, implemented in Infrastructure.
- **A database provider gap** (SQL Server, Postgres and SQLite translating something differently): fixed in the
  Infrastructure's database layer, never by rewriting the use case's query.
- **An endpoint**: a controller in the Server's feature folder that calls the Application service. Controllers stay thin.

## Domain-driven design

### Bounded contexts

`GrifballWebApp.Domain` has one namespace per bounded context:

| Context | What it holds |
| --- | --- |
| `League` | Seasons, teams, season matches and series, brackets, signups, availability, reschedules, championships, VODs |
| `LateLeague` | Late League matchmaking: the queue, matched matches and their votes, matchmaking seasons and stats |
| `Archive` | The historical archive: its sources and imports, recorded games, gamertag claims |
| `Players` | Users, roles, Discord and Xbox users, user merges |
| `Halo` | Halo Infinite matches, their participants and teams, medals |
| `Shared` | The shared kernel: auditing, leagues, regions, ranks, game versions, the outbox, the aggregate attributes |

A type may name another context's type only where `ContextCrossings.Allowed` (in `GrifballWebApp.Architecture.Test`)
lists that crossing. A new crossing is a decision made in review: when `BoundedContextTests` fails, first ask whether the
code could go through a type of its own context or the Shared kernel; if the crossing belongs, add it to the list.
Crossings the code no longer makes come off the list (the test says which).

### Aggregates

Each model type is either an aggregate root (`[AggregateRoot]`) or a part of one (`[AggregatePart(typeof(Root))]`).
`Season` (`GrifballWebApp.Domain/League/Season.cs`) is the reference example:

- **No public constructor.** A private one for EF, and named static factories for each way the aggregate begins
  (`Season.Organized(...)`, `Season.Imported(...)`).
- **No public setters.** Properties have private setters, and navigations are private collections exposed read-only.
- **Methods own the rules.** State changes go through methods on the root (`Edit`, `Schedule`, `AnnounceAt`) that
  validate and refuse what the aggregate doesn't allow.
- **Parts are reached through their root.** A part has no public `DbSet`; `IApplicationDbContext` exposes the roots.
- **Internals stay in the aggregate.** A model's internal members may be used only by its own aggregate, by EF, and by
  code that names it with `[AggregateAccess(typeof(Root))]`. `AggregateBoundaryTests` checks this in the compiled code.

Entity configurations (`*Configuration.cs`) live beside their entities in the Domain. The one model is the source of
all three databases' migrations ([database.md](database.md)).

## Tests by layer

Each layer has its own unit test project (`GrifballWebApp.Domain.Test`, `.Application.Test`, `.Infrastructure.Test`,
`.Server.Test`); anything that needs a real database server, the hosted app, Discord transports or the network goes
in `GrifballWebApp.Integration.Test`. See [ci-cd/dotnet-tests.md](ci-cd/dotnet-tests.md) for the full rules.

## The frontend

The Angular client has its own boundaries (feature folders, `core`, `shared`), enforced by lint: see
[frontend.md](frontend.md).
