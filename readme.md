[![codecov](https://codecov.io/gh/PlayGrifball/GrifballWebApp/branch/master/graph/badge.svg)](https://codecov.io/gh/PlayGrifball/GrifballWebApp)
[![.NET Tests](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/dotnet-tests.yml/badge.svg)](https://github.com/PlayGrifball/GrifballWebApp/actions/workflows/dotnet-tests.yml)
# Grifball Web App
Full stack web application for planning and organizing Grifball tournaments, pulling match stats, and providing data insights.

## Objectives
- Users of this application are primarily expected to be players, event organizers, and/or viewers.
- Event organizers should have the ability to create and plan Seasons and/or short-term tournaments.
- Players should be able to define their standard availability.
- Players should be able to sign up for events.
- Players should be able to adjust availability on a per event basis and otherwise track any deviations to standard availability
- Event organizers should be able to use availability data to plan matches
- Captains should be able to submit the MatchID (found in Waypoint link) after match for data to be pulled
- There must be a draft page
- Captains must be able to use the draft page to pick players
- Viewers should be able to view seasons, teams, and players.
- Player profile page should show all teams and seasons they are participated in along with grand total stats.
- There must be a Schedule list page with dates and times
- There must be a Team Standings page with Wins and Losses
- There must be a Game History page to display the final score of each match. Each should link to a more detailed match page.
- There must be a Playoffs Bracket page


## Missing Requirements
- Need to know how to determine whether a team makes playoffs
- Need to know how teams are seeded in playoffs


Up until now stats have been compiled into excel sheets, see Winter League 2023 Stats:
https://docs.google.com/spreadsheets/d/14tRPXLkjauRV-xfQiegUD6VMlqso7_EYe68E9HY45_c
https://docs.google.com/spreadsheets/d/1xpraseVR_rQu7LCuUNWuFWf4qSHcLyJlxDN2RtMF240

## Deploying
To Kubernetes with the Helm chart in [charts/grifballwebapp](charts/grifballwebapp/README.md), published to
`oci://ghcr.io/playgrifball/charts/grifballwebapp`: frontend, backend, optionally SQL Server or PostgreSQL, and a
migration Job that backs the database up before applying new migrations.

## Backend Technologies
- [.NET 10](https://learn.microsoft.com/en-us/dotnet/) - [source](https://github.com/dotnet)
- [ASP.NET Core Web API](https://learn.microsoft.com/en-us/aspnet/core) - [source](https://github.com/dotnet/aspnetcore)
- [SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/) - [source](https://github.com/dotnet/aspnetcore/tree/main/src/SignalR)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) - [source](https://github.com/dotnet/efcore)
- [Grunt API](https://github.com/NoahSurprenant/grunt) for pulling Halo Infinite stats

## Frontend Technologies
- [Angular 20](https://angular.io/) - [source](https://github.com/angular/angular)
- [Angular Material](https://material.angular.io/) - [source](https://github.com/angular/components)
- [ngx-drag-drop](https://reppners.github.io/ngx-drag-drop/) - [source](https://github.com/reppners/ngx-drag-drop) because [the standard angular dnd does not know how to flex](https://github.com/angular/components/issues/13372)
- [ng-matero extensions](https://ng-matero.github.io/extensions/) - [source](https://github.com/ng-matero/extensions) for its DateTimepicker
- [Luxon](https://moment.github.io/luxon) - [source](https://github.com/moment/luxon/) because Date + Javascript + Coordinated Lunar Time = :(

## Developer Setup

### Installation

To get started with Grifball Web App development, install the following tools:

- **[Visual Studio 2026](https://visualstudio.microsoft.com/downloads/)**  
  - Community Edition is sufficient.
  - During installation, select the "ASP.NET and web development" workload.
  - If already installed, you can modify the installation via the Visual Studio Installer. 
  - It is great for backend development.
  - You can also use Visual Studio for frontend work if you prefer.

- **[Visual Studio Code](https://code.visualstudio.com/download)** (Optional)  
  - Recommended for frontend development.
  - When you open the project, VS Code will prompt you to install recommended extensions (see [extensions.json](grifballwebapp.client/.vscode/extensions.json)).
  - You can use VS Code for backend development as well, but some additional configuration may be required (e.g., setting up launch configurations in [launch.json](grifballwebapp.client/.vscode/launch.json)).
	- In theory this would involve installing the .NET SDK manually (Assuming you did not install it with VS 2026)
	- Manually using the terminal to run and debug the backend application with commands like `dotnet watch run` or setting up launch.json to do it for you.

- **[SQL Server 2022 Developer](https://go.microsoft.com/fwlink/p/?linkid=2215158&clcid=0x809&culture=en-gb&country=gb)**  
  - Required for local database development.

- **[SQL Server Management Studio (SSMS)](https://aka.ms/ssms/21/release/vs_SSMS.exe)**  
  - Useful for managing and inspecting your local database.
  - If the link breaks, refer to the [Install Guide](https://learn.microsoft.com/en-gb/ssms/install/install).

- **[Node.js](https://nodejs.org/)**  
  - Recommend using [nvm-windows](https://github.com/coreybutler/nvm-windows/releases) to manage Node.js versions rather than installing Node.js directly.
  - The project currently uses Node.js v22.12.0, but any version [compatible with Angular](https://angular.dev/reference/versions) will work.
  - Use nvm to install node: `nvm install 22.12.0`
  - Use nvm to switch node versions: `nvm use 22.12.0`

- **Angular CLI**  
  - Install globally after setting up Node.js: `npm install -g @angular/cli`
  - See [Angular CLI documentation](https://angular.dev/installation) for more details.
  - Inside the `grifballwebapp.client` folder, run `npm install --force` to install all required packages.

- **Entity Framework Core Tools**  
  - Used for managing database migrations: `dotnet tool install --global dotnet-ef --version 10.*`
  - See [EF Core CLI documentation](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) for more information.

### Configuration
This application uses the IConfiguration interface to access settings that are effectively a set of key-value pairs.  
The source of these settings comes from multiple sources, including: appsettings.json, environment variables, user secrets just to name the most important ones.  
Some settings cannot be committed to source control either because they are sensitive, or they are specific to your needs. For this reason it is recommended that you setup your own user secrets by right-clicking GrifballWebApp.Server project in Solution Explorer and selecting user secrets.  
Here is an example of what the user secrets file might look like:
```json
{
  "Kestrel:Certificates:Development:Password": "", // Generated by Kestrel, developer specific.
  "JwtSecret": "", // A base64 encoded string used to sign JWT tokens, should be kept secret.
  // The GoogleSheets:Sheets array is used by the export to Google Sheets page for stat tracking
  "GoogleSheets": {
    "Sheets": [
      {
        "Name": "Pro",
        "SpreadsheetID": "1Pzw2oh54gHF4dCC-RIwBoBKolH9qsPrYGXLwqYqWBQI",
        "SheetName": "Season Match History"
      },
      {
        "Name": "Ammy",
        "SpreadsheetID": "1v6tCrEpFPUGx9HhHw42y8wEeu4BJeyfL7six0PkgbUs",
        "SheetName": "Season Match History"
      }
    ]
  },
  "GoogleSheets:Key": "C:/Users/username/grifball-449020-2e7985f8e46b.json", // Path to the Google Sheets API key file, should be kept secret.
  "GoogleSheets:CopySpreadsheetID": "1-zTTfLK4RgxlTJfzWEGhI-vaecS6UOSLhZq3ikTyzA4", // I am not sure if this is needed anymore
  "GoogleSheets:CopySheetNameRange": "Game History!F:F", // I am not sure if this is needed anymore
  "Discord:Token": "", // Token for the Discord bot, should be kept secret
  "Discord:QueueChannel": "1368687948010356797", // Channel where the bot post the queue message.
  "Discord:MatchPlayers": "2", // The number of players needed to start a LL match. Notice I set it to 2 for testing purposes.
  "Discord:LogChannel": "1368717929499197550", // Channel where the bot will log messages like match created.
  "Discord:EventsChannel": "1387623047124619459", // Channel where the bot will post events like season signups.
  "Discord:DraftChannel": "886634393333022754", // Channel where the bot will post draft messages.
  "Discord:DisableGlobally": "False", // The intention of this is to be a feature flag for discord functionality but I do not believe it is fully honored and may only disable the LL queue.
  "Discord:ClientSecret": "", // Discord client secret
  "Discord:ClientId": "", // Discord client ID
  "BaseUrl": "https://localhost:4200",
  "ClientConfiguration:RedirectUrl": "https://localhost:4200/infiniteclient",
  "ClientConfiguration:ClientSecret": "", // Microsoft client secret for the Halo Infinite API
  "ClientConfiguration:ClientId": "", // Microsoft client Id for the Halo Infinite API
  "ApplyMigrations": "True" // Whether to apply migrations on startup, set to false if you want to manage migrations manually.
}
```

### Database
The app runs on SQL Server (the default) or PostgreSQL, picked by the `Database:Provider` setting (`SqlServer` or
`Postgres`); `ConnectionStrings:GrifballWebApp` is in that database's own format. For Postgres, in user secrets:
```json
{
  "Database:Provider": "Postgres",
  "ConnectionStrings:GrifballWebApp": "Host=localhost;Port=5432;Database=GrifballWebApp;Username=postgres;Password=..."
}
```

Each database has its own migrations project, both generated from the one model in `GrifballWebApp.Database`:
`GrifballWebApp.Migrations.SqlServer` and `GrifballWebApp.Migrations.Postgres`. A model change needs a migration in
both (a test fails for whichever is missing). Each project is its own startup project and reads the connection string
from `GrifballWebApp.Database`'s user secrets or `ConnectionStrings__GrifballWebApp`; `migrations add` needs none
that works, so any placeholder will do there:
```sh
dotnet ef migrations add <Name> --project GrifballWebApp.Migrations.SqlServer --startup-project GrifballWebApp.Migrations.SqlServer
dotnet ef migrations add <Name> --project GrifballWebApp.Migrations.Postgres --startup-project GrifballWebApp.Migrations.Postgres
```

The tests run on SQL Server unless `GRIF_TEST_DATABASE=Postgres`; CI runs them on both.

#### Row history
SQL Server keeps a history of every row change in its temporal tables. Postgres has none, so the app keeps the same
history itself, for the same tables (`RowHistory`, `RowHistoryInterceptor`):
- Each of those tables has a `PeriodStart` column, when its row's current version began, and a history table beside
  it, `<Table>History` in the same schema (as SQL Server names them): the table's columns, nullable but for the key,
  and `Valid`, the `tsrange` (UTC, like every time here) in which a version was current.
- On every `SaveChanges` that updates or deletes such a row, the interceptor adds its old version to the history,
  `Valid` from its `PeriodStart` to now, in the same transaction: the change and its history are saved together or
  not at all. For an entity attached rather than loaded (`Update`, `Remove` of an entity from elsewhere, as
  Identity's `UserStore.UpdateAsync` does), the old version is read from the database. A delete records what the
  database's `ON DELETE CASCADE` deletes with it: the interceptor reads those rows first.
- Each history table has `UNIQUE (<key>, "Valid" WITHOUT OVERLAPS)` (PostgreSQL 18): no two versions of a row
  current at once. It also rejects the second of two saves of the same version (both loaded it, both changed it),
  which on SQL Server would silently overwrite the first; the second save fails, with no change.

The tables hold only current rows, so they're queried as before, with no filter; history is asked for:
`context.AsOf<Team>(utcTime)` (either database) reads a table as it was, `context.HistoryOf<Team>()` (Postgres) its
history rows. In SQL, as of a time:
```sql
SELECT "TeamID", "TeamName", ... FROM "Event"."Teams" WHERE "PeriodStart" <= @asOf
UNION ALL
SELECT "TeamID", "TeamName", ... FROM "Event"."TeamsHistory" WHERE "Valid" @> @asOf;
```

Not recorded, as it bypasses `SaveChanges`: `ExecuteUpdate`, `ExecuteDelete` and raw SQL. To delete in bulk with
history, use `ExecuteDeleteWithHistoryAsync` (`ExecuteDeleteAsync` on SQL Server). There's no history from before
it began (the `AddRowHistory` migration): the rows already there have that as their `PeriodStart`. An `ON DELETE SET
NULL` would change rows unseen, so there are none (a test fails on one).

History tables follow the model: a column or table added to a table with history is added to its history by
`migrations add`, the constraint too (`PostgresMigrationsSqlGenerator` creates the history table's GiST index as it).
The constraint needs PostgreSQL 18 and the `btree_gist` extension (contrib, in the official images), which the
migration creates; it's a trusted extension, so the migrations' user needs only `CREATE` on the database, as owner
or granted. A managed PostgreSQL has to offer it.

### Running the Application

0. Optionally, open the `grifballwebapp.client` folder in Visual Studio Code to work on the frontend. In the terminal, run `npm start` to start the Angular development server. The Angular frontend will be available at [https://localhost:4200](https://localhost:4200).
1. Open the `GrifballWebApp.sln` file in Visual Studio.
2. The startup project should be set to `GrifballWebApp.Server`, the project in bold in the Solution Explorer is the startup project, additionally it shows up as the startup project to the right of the cogwheel near the green debug arrow. Startup project can be changed either via cogwheel dropdown or by right-clicking the project and selecting "Set as Startup Project" in solution explorer.
3. Recommended startup profile is `https`, or `IIS Express`. This is set via the dropdown near green debug arrow.
4. Press `F5` or click the green debug arrow to start the backend server. The API swagger page will be available at [https://localhost:7210/swagger](https://localhost:7210/swagger). The backend will attempt to run `npm start` for the frontend, if it does not detect it already running as specified in step 0. It is recommended to run the frontend separately in step 0 to avoid issues with the backend trying to start the frontend.
