# Background jobs

The app's scheduled background work runs as [Quartz.NET](https://www.quartz-scheduler.net/) jobs
(`GrifballWebApp.Server/Jobs`), with Quartz's job store in the app's own database: the `QRTZ_` tables (`qrtz_` on
PostgreSQL) the `AddQuartz` migration makes on each provider, from Quartz's own scripts. The jobs, their schedules,
pauses and run history are shared by every pod and outlive restarts and deploys.

| Job | Default schedule | Does |
|---|---|---|
| `historical-refresh` | Daily, first 10 minutes after a worker first starts with it | Refreshes the historical sources; an import is queued when one changed ([Historical Import](historical-import.md)). Due while a job is queued or running, it tries again 5 minutes later. |
| `xbox-placeholders` | Hourly | Gives Xbox accounts no user holds a placeholder user |
| `active-season-pulls` | Every 30 minutes, first 5 minutes after a worker first starts with it | Pulls the latest custom games (one page of 25) of every player on a team in a season under way from Halo Infinite, so their season matches' possible matches are there when the page opens |
| `matchmaking-queue` | Every 10 seconds | Makes matches from the matchmaking queue and announces them (Discord only) |
| `season-events` | Every 10 seconds | Updates the season announcements in the Discord events channel (Discord only) |

Each runs one at a time, on every pod together. A tick missed while no worker was up is skipped for the 10-second
jobs, and run once when a worker is back for the others.

## On-demand jobs

Some jobs run once per request from a page, with an input of their own (Quartz's one-off jobs, `OnDemandJobs`): the
page queues a run, named after what it is for, and polls its status (queued, running with its progress, or idle with
how the last run went). Asking again while one is queued or running for the same thing adds nothing. They show on the
Jobs page as **On demand**, with their runs, but are started from their page only.

| Job | Started from | Does |
|---|---|---|
| `SeasonMatchPullJob` | Season match page, "Pull recent games" (commissioners) | Pulls both teams' players' recent custom games (11 pages of 25) from Halo Infinite. The page's possible matches only read the database. |
| `ScheduleRecommendationJob` | Season schedule, editing: "Recommend times" (commissioners) | Runs the genetic algorithm for the season's regular matches (`ScheduleService.Recommend`): up to 2000 generations or 2 minutes (`ScheduleRecommendations:MaxGenerations`, `:TimeBudget`), sooner at a perfect schedule, reporting its generation and best fitness. Keeps its best schedule (`ScheduleRecommendations` table), which the schedule shows with each match's conflicts and a commissioner applies: every match without a result gets its time. |

The Halo pulls (`active-season-pulls`, `SeasonMatchPullJob`) share the `halo-match-pulls` execution group, limited to
one at a time, so two never ask for and save the same games at once.

## Where they run

`App:Role` decides it (see `Hosting/AppRole.cs`):

- **all** (one pod) and **worker**: the scheduler starts and fires the jobs. As it starts, each recurring job
  without a trigger gets its default one (`RecurringJobSeeder`); a trigger that is there is left alone, so a pause or a
  schedule changed on the Jobs page stays.
- **api**: the scheduler is built in standby. It reads and changes the store (the Jobs page, "run now") and fires
  nothing; the worker picks up what it stores within about 5 seconds.

On SQL Server and PostgreSQL the scheduler is clustered: during a rolling deploy two workers never run the same firing,
and a worker that dies has its runs recovered. SQLite can't be clustered (Quartz refuses it), and is one pod anyway
([SQLite](sqlite.md)).

The Discord jobs are off (no trigger) while Discord is, as in a preview without a bot; turned back on, they get their
default trigger again.

## The Jobs page

Sysadmins: User management > Jobs (`/usermanagement/jobs`). For each job: how often it runs, its state, its next and
last run, and its run counts; run it now, pause or resume it, change its interval (5 seconds to 365 days) or put it
back on its default, and stop a running one. Its recent runs (a week of them, at most 200 per job, failures always
kept) come from Quartz's execution history, with the pod that ran each. The two 10-second Discord jobs record a
failure at most every 10 minutes (`RepeatedFailures`); the failures in between are logged and shown as skipped runs
with their error, so a Discord outage can't push the other jobs' runs out of the history (5000 rows in all).

## Adding a job

Write an `IJob` with a static `RecurringJob` definition (its key in `RecurringJobs.Group`, a description and its default
schedule) and register it in `Program.cs`'s `AddJobs` call. An on-demand job is an `IJob<TInput>`, registered with
`AddOnDemand` and queued with `OnDemandJobs.Enqueue`. It gets a DI scope per run, so it can take a
`GrifballContext`. A later Quartz version's schema changes come from its `database/migrations` scripts, in a migration
of their own on each provider.

The database converter leaves the Quartz tables out: a converted database starts with none, and its worker makes the
recurring jobs' triggers from their defaults.
