# Background jobs

The app's scheduled background work runs as [Quartz.NET](https://www.quartz-scheduler.net/) jobs
(`GrifballWebApp.Infrastructure/Jobs`), with Quartz's job store in the app's own database: the `QRTZ_` tables (`qrtz_` on
PostgreSQL) the `AddQuartz` migration makes on each provider, from Quartz's own scripts. The jobs, their schedules,
pauses and run history are shared by every pod and outlive restarts and deploys.

| Job | Default schedule | Does |
|---|---|---|
| `historical-refresh` | Daily, first 10 minutes after a worker first starts with it | Refreshes the historical sources; an import is queued when one changed ([Historical Import](historical-import.md)). Due while a job is queued or running, it tries again 5 minutes later. |
| `xbox-placeholders` | Hourly | Gives a placeholder user to each Xbox account no user holds that played in a league game (a game reported for a season match or a Late League match: a sub who never signed up). Reporting a game also makes its players' placeholders at once. Someone only in pulled pickup games is no league player and gets none: their account and games stay, on its Xbox page (`/xbox/{xuid}`). |
| `active-season-pulls` | Every 30 minutes, first 5 minutes after a worker first starts with it | Pulls the latest custom games (one page of 25) of every player on a team in a season under way from Halo Infinite, so their season matches' possible matches are there when the page opens |
| `match-variant-backfill` | Hourly, first 10 minutes after a worker first starts with it | Fetches the stats of matches saved before their game variant was kept, 200 a run (`MatchVariantBackfill:BatchSize`), in a random order, and sets only their variant (`UgcGameVariantAssetID`/`VersionID`), never the stats saved before. One that Halo doesn't give is tried again on a later run. Then decodes the custom scores (#217) of the matches not decoded yet and of every match of the variants it filled in (`CustomScoreLayouts`). Once every match has its variant and is decoded, a run is two queries. |
| `password-reset-link-cleanup` | Daily, first 15 minutes after a worker first starts with it | Deletes expired and used password reset links (the sysadmin's cleanup button still works) |
| `matchmaking-queue` | Every 10 seconds | Makes matches from the matchmaking queue and announces them (Discord only) |
| `season-events` | Every 10 seconds | Updates the season announcements in the Discord events channel (Discord only) |

Each runs one at a time, on every pod together. A tick missed while no worker was up is skipped for the 10-second
jobs, and run once when a worker is back for the others.

## Manual-only jobs

A recurring job with `ManualOnly` has no trigger. The worker never makes one, so it never runs on a schedule. The Jobs page
shows it as "Run by hand", with no interval, Pause or schedule, and a sysadmin starts it with Run now.

| Job | Does |
|---|---|
| `xbox-placeholder-cleanup` | Removes the placeholder users made before only league players got one. A placeholder goes only when it is untouched since it was made (a dummy `historical:` user holding an Xbox account, shown by its gamertag, with no Discord link, email, password, region or lockout, never renamed or edited), its account played in no league game (a season match's or a Late League match's reported game), and nothing names it: no foreign key to it (stats, teams, signups, claims, championships, roles) and no key-less user column (merges, duplicate dismissals, audit columns). It checks and deletes 200 at a time, each batch in a serializable transaction, so nothing written meanwhile slips between the check and the delete. Any failure stops the run and fails it on the Jobs page; the batches done stay done, and running it again carries on. The Xbox account and its games stay. It logs each one it removed. |

## On-demand jobs

Some jobs run once per request from a page, with an input of their own (Quartz's one-off jobs, `OnDemandJobs`): the
page queues a run, named after what it is for, and polls its status (queued, running with its progress, or idle with
how the last run went). Asking again while one is queued or running for the same thing adds nothing. They show on the
Jobs page as **On demand**, with their runs, but aren't started from it: from their page, or (`season-match-pull`) by the agents' `pull_season_match_games` tool too, through the MCP server or the Discord bot's chat.

| Job | Started from | Does |
|---|---|---|
| `season-match-pull` (`SeasonMatchPullJob`) | Season match page, "Pull recent games" (players, commissioners and sysadmins) | Pulls both teams' players' recent custom games (11 pages of 25) from Halo Infinite; or, from "Pull back to the season's start", the games they played during the match's season (a day before it to a week after it), however far back: each history is searched for the season's pages (pages 1, 2, 4, 8…, then halving the gap) and only those are read, up to 100 Halo calls a player (one that needs more is reported). The page's possible matches only read the database: the newest 200 of the season's games (a day before it to a week after it) with players of both teams. |
| `xbox-account-pull` (`XboxAccountPullJob`) | Xbox account page (`/xbox/{xuid}`), "Pull recent games" (players, commissioners and sysadmins) | Pulls the account's recent custom games (11 pages of 25) from Halo Infinite, as the season match page's pull does for its players. One run an account at a time, and one Halo pull at a time across them all. |
| `schedule-recommendation` (`ScheduleRecommendationJob`) | Season schedule, editing: "Recommend times" (commissioners) | Runs the genetic algorithm for the season's regular matches (`ScheduleService.Recommend`): up to 2000 generations or 2 minutes (`ScheduleRecommendations:MaxGenerations`, `:TimeBudget`), sooner at a perfect schedule, reporting its generation and best fitness. Keeps its best schedule (`ScheduleRecommendations` table), which the schedule shows with each match's conflicts and a commissioner applies: every match without a result gets its time. |
| `sheet-export` (`SheetExportJob`) | Excel page, "Export all matches" (commissioners) | Writes every match in the copy sheet to the sheet (`ExcelService.ExportAll`), downloading the ones the database doesn't have. One export at a time. |
| `database-backup` (`DatabaseBackupJob`) | Database Backup page, a download (sysadmins) | Makes the database as a file (SQLite, a script for PostgreSQL or SQL Server, or a native `.bak`/`.dump`, on the scratch server when it's the other kind) and keeps it on the pod that ran it for an hour, or until the next one of its format: one backup a format (`BackupFiles`, each format its own run, `database-backup-<format>`). The page downloads it from there: on one pod, from its disk; on an API pod with workers, streamed from the worker that made it (its pod address, `DatabaseBackup:NodeUrl`, recorded with the run), at `/internal/backups/{id}`, with a five-minute token only the app's pods can make (`BackupDownloads`, Data Protection). |
| `database-export` (`DatabaseExportJob`) | Database Backup page, an export (sysadmins) | Copies the database into a new one on another server. Both report their progress table by table ("Copying Matches (26 of 64)"), which the page shows as a bar. The target's connection string reaches the job store protected (Data Protection, readable for an hour), never in the clear. |

The Halo pulls (`active-season-pulls`, `SeasonMatchPullJob`, `match-variant-backfill`) share the `halo-match-pulls` execution group, limited to
one at a time across every pod sharing the store (a cluster-scoped limit), so two never ask for and save the same games
at once.
Their Halo calls are paced to the gateway key's quota (`HaloGateway:PermitsPerMinute`, 90% of it; unset: no pacing). The
quota is the key's, so scaled out the turns are shared through Redis (`ConnectionStrings:Redis`, key
`<SignalR:RedisChannelPrefix>:halo-call-pace`): every pod's pulls (the workers' jobs and historical imports, an API pod's,
the Discord commands') and grif-discord's XUID lookups queue for the same turns, so two workers pulling at once still keep
to it. The turns share the SignalR backplane's Redis connection, one per process; a connect that failed is tried again.
While Redis can't be reached a pod paces its own calls at a tenth of the rate (the other pods may still be taking every
turn, and its calls come on top), and tries Redis again after 30 seconds. Without Redis (one pod) it paces at the full rate. A
call the gateway still turns away is tried again after its Retry-After, up to 8 times (`HaloGateway:Retry:MaxAttempts`).
A run that couldn't fetch everything it found (a game, or a page of a player's history) is recorded as Failed, its summary
saying how many: what it did fetch is saved, and the next pull tries the rest again.

A run someone started runs as them (`JobUser`, Quartz's job scope hook, before the job is built): an on-demand job's
input carries `RequestedByUserID` (`IRequestedByUser`, set by the server when it's queued), and "run now" on the Jobs
page puts the user who pressed it in its trigger. So what the run saves has their `CreatedByID`/`ModifiedByID`, as when
the work ran in their request. A scheduled run names nobody.
The database backup and export share `database-backups`, one at a time across the pods (each copy holds a table's rows in memory); the
page answers 409 while either is queued or running.

## Where they run

`App:Role` decides it (see `Hosting/AppRole.cs`):

- **all** (one pod) and **worker**: the scheduler starts and fires the jobs. As it starts, each recurring job
  without a trigger gets its default one (`RecurringJobSeeder`); a trigger that is there is left alone, so a pause or a
  schedule changed on the Jobs page stays.
- **api** and **discord**: the scheduler is built in standby. It reads and changes the store (the Jobs page, "run
  now") and fires nothing; a worker picks up what it stores within about 5 seconds.

On SQL Server and PostgreSQL the scheduler is clustered, so any number of workers can run (the chart's
`worker.replicas`): each firing runs on one of them, a job never runs on two at once (`DisallowConcurrentExecution`
holds across them), an execution group's limit counts every worker's runs, a rolling deploy's old and new workers
never run the same firing. Triggers are acquired under the store's lock (`AcquireTriggersWithinLock`), so a
cluster-wide limit is exact: two workers acquiring at once can't both take a group's last slot. A worker that dies has
its runs cleared once the others see it gone (its check-ins stop: within about half a minute), freeing its execution-group slots,
but no job asks to be recovered, so its runs aren't run again: a recurring job runs at its next firing, and an on-demand
run (a backup, a sheet export) is started again from its page. Workers starting at once agree on each recurring job's
trigger (the seeder's `TriggerConflict.Keep` is decided under the store's lock).

A worker starting with an on-demand backup scratch server queues `scratch-stop` (`ScratchStopJob`), which stops it in
case a worker that died, or that a rollout stopped mid-backup, left it running. It is in the `database-backups`
execution group, so it waits while a backup or export is queued or running on any worker (that run stops the server
itself), and runs once a dead worker's backup has been cleared. SQLite can't be clustered (Quartz refuses it), and is one pod anyway
([SQLite](sqlite.md)).

`matchmaking-queue` and the queue buttons (on grif-discord) both make matches (`QueueService.Go`): each run holds the
`matchmaking-queue` lock in the database (`DatabaseLocks`: an application lock on SQL Server, an advisory lock on
PostgreSQL), so whichever pod starts one, two never read the same queue and put its players in two matches.

The Discord jobs are off (no trigger) while Discord is, as in a preview without a bot; turned back on, they get their
default trigger again.

## The Jobs page

Sysadmins: User management > Jobs (`/usermanagement/jobs`). For each job: how often it runs, its state, its next and
last run, and its run counts; run it now, pause or resume it, change its interval (5 seconds to 365 days) or put it
back on its default, and stop a running one. Its recent runs (a week of them, at most 200 per job, failures always
kept) come from Quartz's execution history, with the pod that ran each. The two 10-second Discord jobs record a
failure at most every 10 minutes across the workers (`RepeatedFailures`, which reads the last one from the run history); the failures in between are logged and shown as skipped runs
with their error, so a Discord outage can't push the other jobs' runs out of the history (5000 rows in all).

## Adding a job

Write an `IJob` with a static `RecurringJob` definition (its key in `RecurringJobs.Group`, a description and its default
schedule) and register it in `Program.cs`'s `AddJobs` call. An on-demand job is an `IJob<TInput>`, registered with
`AddOnDemand` and queued with `OnDemandJobs.Enqueue`. It gets a DI scope per run, so it can take a
`GrifballContext`. A later Quartz version's schema changes come from its `database/migrations` scripts, in a migration
of their own on each provider.

## Traces and metrics

Each job run is a trace of its own in Tempo (`Quartz.Job.Execute`, with the job's HTTP and database calls under it and
its log lines carrying its trace ID), and Quartz's meter (`quartz.job.execution.*`, misfires, store round trips) goes
with the app's metrics. Left out: the scheduler loop's own store calls, every few seconds (`SchedulerLoopSampler`), and
the two 10-second Discord jobs' runs (`JobTracing.DontRecordThisRun`).

The database converter leaves the Quartz tables out: a converted database starts with none, and its worker makes the
recurring jobs' triggers from their defaults.
