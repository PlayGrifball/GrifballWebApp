# SQLite

The app runs on SQL Server (the default), PostgreSQL, or SQLite. SQLite is a single file the backend opens
itself, with no database server: the simplest way to run one instance of the app for a small league, a demo
or development. It is not for running more than one instance.

## Setting it up

```json
{
  "Database:Provider": "Sqlite",
  "ConnectionStrings:GrifballWebApp": "Data Source=/data/grif.db",
  "ApplyMigrations": true,
  "CreateDatabase": true
}
```

With `ApplyMigrations` and `CreateDatabase`, the backend creates the file and applies its migrations as it
starts. The folder must exist. Alternatively, apply them with the image's bundle,
`/app/sqlite/efbundle` (with `ConnectionStrings__GrifballWebApp` set), or with
`dotnet ef database update --project GrifballWebApp.Migrations.Sqlite --startup-project GrifballWebApp.Migrations.Sqlite`.

On Kubernetes, the Helm chart does this for you with `database.provider: sqlite`: see the chart's README
(`charts/grifballwebapp`, Database, SQLite).

## Limits

- **One instance.** SQLite locks the whole file for each write, and only processes on the same machine can
  share it safely. Run one backend, without a separate worker (the chart refuses anything else). For more
  replicas, use PostgreSQL or SQL Server.
- **Local storage only.** Keep the file on a local disk or a block volume. File locking over network storage
  (NFS, an RWX volume) isn't reliable, and SQLite's WAL mode doesn't work there at all: the file can be
  corrupted.
- **Case-insensitive text is ASCII only.** Text compares with SQLite's `NOCASE`: "Noah" = "noah", but "É" and "é"
  differ, where SQL Server and PostgreSQL match them. Searches (`string.Contains`) and comparisons of a function's
  result (`Replace(...) == name`, which has no collation on SQLite) are made case-insensitive the same way when
  queries are translated.
- **History is stamped per statement.** SQLite has no transaction clock, so each change is stamped when its
  statement runs; SQL Server and Postgres stamp all of a transaction's changes with its start. A version that a
  transaction made and then replaced shows up in `TemporalAll` and the like on SQLite (unless it lasted under a
  millisecond), where the others leave it out. History times are to the millisecond.
- **Text lengths aren't enforced.** SQLite ignores a column's maximum length: text longer than the app's limit
  (which the app's own validation and the other databases refuse) is stored whole.
- **Decimals are doubles.** The few decimal columns (players' season stats) are stored as doubles on SQLite, so
  they can be sorted and compared in queries, and durations (`TimeSpan`) as their ticks.

## How it works

- **Writes.** Every connection opens the file in WAL mode, so reads go on while a write runs. Writes take turns:
  EF's transactions start `IMMEDIATE`, taking the write lock up front, and a write that finds the file locked
  waits for it (up to the command timeout, 30 seconds) rather than failing.
- **Locks.** SQL Server and PostgreSQL lock rows in two places: the Discord outbox, so two dispatchers never take
  the same message, and a season's draft, so its changes run one at a time. SQLite has no row locks and needs none:
  the outbox takes its messages in one `UPDATE ... RETURNING` statement, and every transaction already holds
  the write lock.
- **Row history.** The same tables keep history as on the other databases, in the same place: the versions a
  row had before each update or delete are in `TableHistory`, each with `PeriodStart` and `PeriodEnd`.
  Two triggers on each table write them (`Table_History_Update`, `Table_History_Delete`), and every migration
  that changes a table with history drops and recreates them (`SqliteHistoryMigrationsModelDiffer`). Times are
  UTC, to the millisecond. Rows a migration itself writes get no history. EF's temporal queries
  (`TemporalAsOf`, `TemporalAll`, `TemporalFromTo`, `TemporalBetween`, `TemporalContainedIn`) read it as they do on
  Postgres: the table and its history table together, filtered by the periods (`SqliteTemporalQueries`).
- **Queries that need APPLY.** SQLite has no `APPLY` (a join to a subquery that refers to the outer row). Where EF
  builds one that is a join in all but name (every row of a table per outer row, or a navigation's
  `Select(...).Distinct()` joined on its foreign key), it is turned into a join as the query is translated
  (`SqliteQueryTranslation.cs`); the app's queries are the same on every database. Any other `APPLY` still fails
  with SQLite's own message.
- **No schemas.** SQLite has no schemas, so tables are named without them (`Seasons`, not `Event.Seasons`). No two
  tables share a name (a test checks).

## Migrations

`GrifballWebApp.Migrations.Sqlite` starts from one baseline migration with today's schema. Like the other two
projects, it needs a migration for every model change; a test fails when one is missing:

```sh
ConnectionStrings__GrifballWebApp="Data Source=design.db" dotnet ef migrations add <Name> \
  --project GrifballWebApp.Migrations.Sqlite --startup-project GrifballWebApp.Migrations.Sqlite
```

SQLite can't alter most things about a table in place, so EF rebuilds the table for those changes (a new
table, the rows copied over, the old one dropped). The history triggers are recreated after each rebuild.

## Backups

The chart's backups belong to its migration Job, which SQLite doesn't use, so set your own up. Any of these
gives a consistent copy while the app runs:

- `sqlite3 /data/grif.db ".backup /backups/grif-$(date +%F).db"`
- `VACUUM INTO '/backups/grif.db'` from any SQLite client
- [Litestream](https://litestream.io), which streams the file to object storage continuously

Copying the file alone (`cp`) while the app writes can miss what's still in `grif.db-wal`.

## Moving to PostgreSQL or SQL Server

`GrifballWebApp.Convert` copies a database between any two of the three, row history included, and checks the
copy before it commits. To outgrow SQLite:

```sh
dotnet /app/convert/GrifballWebApp.Convert.dll \
  --Source:Provider=Sqlite --Source:ConnectionString="Data Source=/data/grif.db" \
  --Target:Provider=Postgres --Target:ConnectionString="Host=...;Database=GrifballWebApp;Username=...;Password=..."
```

The source must be migrated to the same build as the converter, and the target must be new (missing, or
empty). Stop the app first, or let it run: SQLite's source is read from one snapshot either way.

## Backups from the site

Sysadmins can download the whole database, whichever server it's on, as a SQLite file from the **Database Backup**
page (`/databasebackup`, `api/Backup/Sqlite`). The converter makes it into the backend's temp folder and checks it
against the database before it's sent; the file is deleted once it's downloaded, and one backup runs at a time.
The file is a working SQLite database, and the page gives the command that loads it into PostgreSQL or SQL Server
with the converter in the site's own image (`sha-<build>`), whose migrations match the backup's.

The same page exports the database straight into a new PostgreSQL, SQL Server or SQLite database (`api/Backup/Export`):
give the server and a connection string to a database that's missing or empty, and the converter runs in the
backend, in the background, the page following it (`api/Backup/ExportStatus`). A database that already has tables is
refused. The connection string is used for that export only, never stored or logged. The backend's NetworkPolicy
lets it reach its own database only: add the target to `networkPolicy.backend.extraEgress` first.
