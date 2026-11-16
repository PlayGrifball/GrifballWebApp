# Backend architecture

The .NET backend follows **Clean Architecture** for its layers and **domain-driven design** inside the Domain. Both
are enforced: `GrifballWebApp.Architecture.Test` reads the compiled assemblies (Mono.Cecil) and the project files, and
fails a PR that breaks a rule, naming the type that crosses and what it reaches.

## Layers

Dependencies point inwards only: **Domain ← Application ← Infrastructure ← Historical ← Server**.

| Layer | Project | What's in it | May use |
| --- | --- | --- | --- |
| Domain | `GrifballWebApp.Domain` | The model, by bounded context: aggregates, their rules, and their EF configurations | none of the solution's projects (EF Core, a package, is allowed) |
| Application | `GrifballWebApp.Application` | The use cases, by feature (`Seasons`, `Brackets`, `Matchmaking`, ...): services, DTOs, queries, and the ports Infrastructure implements (`IApplicationDbContext`, `IOutbox`, `IDatabaseLocks`, ...) | Domain |
| Infrastructure | `GrifballWebApp.Infrastructure` | `GrifballContext` and the database plumbing (providers, row history, temporal queries, conversion, backups), and the adapters: Halo Infinite, Redis, outbox, Discord, Excel, jobs | Application, Domain, DiscordInterfaces |
| Historical | `GrifballWebApp.Historical` | The historical archive's import, whole: its sources (catalog, downloads, snapshots), the parsers of their sheets, the importer, and the worker and schedule that run it. Above Infrastructure: it reads the snapshots and sheets through `GrifballContext` and the workbook reader | Infrastructure, Application, Domain |
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

## SignalR hubs and sessions

The hubs (`TeamsHub`, `QueueHub`, `ChatHub`) never trust a connection's claims beyond the moment they're checked. A connection
keeps the `ClaimsPrincipal` it was made with, so the server checks it against the database (`GrifballWebApp.Server/SignalR/HubSessions.cs`):

- **On every call and on connecting** (`HubSessionFilter`, registered first for every hub), `HubSessionValidator` asks
  Identity whether the user is still signed in and allowed:
  - the claims' security stamp is still the user's;
  - they aren't locked out;
  - their roles now.

  A call is refused (`HubException`) and its connection closed when:
  - the session has ended;
  - the roles changed since the connection was made: its groups, such as TeamsHub's staff group, were the old roles';
  - the method's and the hub's `[Authorize]` (roles, policies, `[AllowAnonymous]`) refuse the user as they are now. ASP.NET Core's own
    `IAuthorizationService` decides this, on the connection's claims with the roles the database has now.

  The user found is the call's `HubUser` (scoped). The hubs read roles there, never `Context.User`: ChatHub's tools and TeamsHub's
  staff group follow the roles now.
- **Proactively**, when a save changes a user's sign-in or roles, their connections are closed on every pod.
  - `UserSessionsInterceptor` (Infrastructure, on every context) spots these saves: a new security stamp (a sign-out, a new
    password), a lockout, a deleted user, a role added or removed. It compares each property's original and current values.
    Identity's `UpdateAsync` marks every property modified, so a failed sign-in, which only counts the failure, ends nothing.
  - Once the save is committed, it calls `IUserSessions.EndAsync`.
  - `HubUserSessions` closes this process's connections at once. When scaled out, it then publishes the users on Redis
    (`user-sessions-ended`, under the SignalR prefix) in the background, logging a failure; the save never waits on Redis.
  - `HubUserSessionsListener` closes them on the other pods.
  - A closed connection connects again as the user is now. A connection that only listens stops getting pushes meant for the
    old roles.
- **As a backstop**, `HubSessionSweep` checks this process's signed-in connections every 30 seconds and closes those whose user
  changed. It checks every user in two queries, however many connections. This catches a change no save announced: SQL run by
  hand, or a lost Redis message. The sweep and the Redis listener run only where the hubs are (the API role).
- **At the cookie's expiry**, every hub is mapped with `CloseOnAuthenticationExpiration`, so the connection closes.
- **Message size**: SignalR's `MaximumReceiveMessageSize` is per hub, not per connection. ChatHub's (about 660 KB, for a whole
  conversation at its limits) applies to signed-out connections too. A message is read whole before the hub sees it, and nothing
  in SignalR checks who sent it first.
