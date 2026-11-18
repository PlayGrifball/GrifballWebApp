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

## Domain events and live updates

An aggregate's methods raise domain events as they make their changes (`Raise(new TeamRosterChanged(this, [userID]))`;
the events are each context's `*Events.cs`, records in the past tense carrying the aggregate). A change the aggregate
refuses raises nothing. An aggregate about to be removed says so first (`Team.Disband`, `SeasonMatch.Cancel`,
`SeasonSignup.Withdraw`, `QueuedPlayer.Leave`): the service calls it, then removes it.

- **Sent once committed.** `DomainEventsInterceptor` (Infrastructure, on every context) collects the saved aggregates'
  events and hands them to `IDomainEventDispatcher` after the save, or after the transaction commits; a rolled back one,
  or a failed save, sends nothing. Their handlers (`IDomainEventHandler<TEvent>`, or of `IDomainEvent` for every event)
  run in a scope of their own, and a handler's failure is logged, never the save's.
- **Bulk statements raise nothing** (`ExecuteUpdate`, `ExecuteDelete`): they bypass the aggregates. Use them where no
  page needs to hear of it at once (the import jobs' progress, which the page polls).
- **Live updates** are one handler, `LiveUpdatesHandler` (Application/Live): `LiveChanges.Of` maps each event to the
  topics it changes (`season:12`, `draft:12`, `seasonmatch:55`, `player:7`, `queue`, `staff:users`, …: `LiveTopics`),
  and `ILiveUpdates` (the Server's `LiveUpdates`) gathers them for a quarter of a second, once a topic however many
  events changed it, and hands them to the API pods (through Redis when scaled out), where `LiveDelivery` tells the
  connections watching each topic there (`LiveWatchers`). A topic nobody on the pod watches costs nothing.
- **Pushed or a notice.** The topics many watch at once (a season's page, its draft, the home page, the queue) come with
  their data (`Snapshot(topic, json)`, `LiveSnapshots`: the same DTOs the API gives those pages, as JSON), built once
  a change on each pod for all its watchers (once a viewer for the home page, whose events differ for staff): a draft
  pick is one set of reads, not one per viewer. Every other topic is a notice (`Changed(topic)`), and its pages read
  again, each after a random delay of up to two seconds so they don't all ask at once. A change's data, or a page's
  read, is what the API gives that viewer.
- **Who may watch what** is `LiveTopicAccess`: a season's topics and its matches' only for a season the viewer may see
  (one not announced yet is staff's alone), the `staff:` topics only for staff. The viewer's own queue place
  (`queue:me`) goes to their connections alone.
- **Pages** watch through `liveReload(topics, reload)` (client `core/live`), in their store's constructor: the store
  loads again, in place, once a burst of changes settles, and when the connection comes back after a drop.

### Who is on the site

Each tab tells `LiveHub` which page it shows (`Viewing`); `PresenceTracker` keeps the API pod's tabs, with every other
pod's through Redis when scaled out (`RedisPresenceStore`: a pod that stops answering drops out), and sends the count
and the pages to every tab when they change (the footer's "N online"). A member is shown by the name the site shows
for them (`User.Named`, never their login name); a staff page, or the page of a season not announced yet or of its
match, is `/staff` for everyone (`PresencePages`).

## The frontend

The Angular client has its own boundaries (feature folders, `core`, `shared`), enforced by lint: see
[frontend.md](frontend.md).

## SignalR hubs and sessions

The hubs (`LiveHub`, `ChatHub`) never trust a connection's claims beyond the moment they're checked. A connection
keeps the `ClaimsPrincipal` it was made with, so the server checks it against the database (`GrifballWebApp.Server/SignalR/HubSessions.cs`):

- **On every call and on connecting** (`HubSessionFilter`, registered first for every hub), `HubSessionValidator` asks
  Identity whether the user is still signed in and allowed:
  - the claims' security stamp is still the user's;
  - they aren't locked out;
  - their roles now.

  A call is refused (`HubException`) and its connection closed when:
  - the session has ended;
  - the roles changed since the connection was made: its groups, such as the topics LiveHub let it watch, were the old roles';
  - the method's and the hub's `[Authorize]` (roles, policies, `[AllowAnonymous]`) refuse the user as they are now. ASP.NET Core's own
    `IAuthorizationService` decides this, on the connection's claims with the roles the database has now.

  The user found is the call's `HubUser` (scoped). The hubs read roles there, never `Context.User`: ChatHub's tools and LiveHub's
  topics follow the roles now.
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
