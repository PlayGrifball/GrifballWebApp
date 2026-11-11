# Historical import

The historical Grifball archive (AGLA, Late League, GWL, IGL and side leagues, 2008 onwards) is **not in the repository**.
The app downloads and imports it itself; the Node `archive-tools` scripts that used to compile it are gone.

## How it works

- **Sources** (`Event.HistoricalSources`): every workbook, page and channel of the archive, from the catalog in
  `GrifballWebApp.Application/Historical/Sources/HistoricalSourceCatalog.cs` (written to the table at runtime: a fresh
  environment needs no setup). Each entry says what the import reads from it (season stats tabs, database tables of stats
  with a season column (`StatsTable`: the grifball.info backup, split by season, division and phase, its season IDs named
  in its entry since the workbook has no names), Waypoint game lists, per-game carnage tabs, the champions record, the
  Late League players list). Dead sources are listed, never downloaded.
- **Snapshots** (`Event.HistoricalSourceSnapshots`): a refresh downloads each enabled source (Google Sheets as their
  xlsx export, a tab's CSV when the export is refused; pages at their URL; Wayback captures at their permanent URL)
  and keeps the content only when its SHA-256 changed. The import reads each source's latest snapshot, so a sheet that
  went private or was deleted still imports from its last one. Snapshots are in the database, so they ride its backups.
- **Parsing** (`Historical/Parsing`): the xlsx is read with ExcelDataReader into text cells and parsed into the same
  `HistoricalArchiveInput` the compiled JSON used to be; `HistoricalImportService` stays the only importer.
- **Jobs**: everything runs in the worker role through the historical import job queue (one job at a time):
  refresh, preview (a dry run: each season's transaction rolled back) and import.
- **Schedule** (`HistoricalRefreshJob`, a Quartz job, daily by default): a scheduled refresh queues a guarded import of
  the seasons the changed sources make up, and nothing when nothing changed. A run that falls due while a job is queued
  or running tries again 5 minutes later. It can be paused on the page (or on the Jobs page, which also changes its
  interval); the schedule and the pause are its trigger in Quartz's tables, so they outlive restarts.
- **Guardrail** (`HistoricalImport:Guardrail`): a guarded import stops a season that would remove more than 20% of its
  stat rows or teams, or any championship, and lists it for review; a sysadmin applies or skips it.
- **VODs** (`Event.Vods`, `Event.VodTeams`; `Historical/Vods`): each refresh job also finds the YouTube channel
  sources' videos, one row per video ID: the channel page's latest snapshot, the archive's capture (`VodArchive`), the
  uploads feed (`/feeds/videos.xml`, the latest ~15, no key) and, when `YouTube:ApiKey` is set, the Data API v3 (every
  upload, durations, exact dates). Shorts and clips under 3 minutes are left out. Titles ("SL24 Week 3 - A vs B",
  "Late League S7 Grand Final") are matched to a season (named, or running when published), its teams and their
  match; a confident match shows on the match, season, team and player pages, an unsure one waits under "VODs to
  review" on the page. A sysadmin's link or dismissal is never changed by a later refresh. Each import matches the
  undecided ones again. The player is youtube-nocookie.com, loaded only on Play.

## Using it

Sysadmins: User management > Historical Import (`/usermanagement/historicalimport`). "Refresh sources" downloads;
"Import" runs a preview first, listing what each season would lose, then "Apply this import" imports exactly what was
previewed. A compiled archive (the old JSON files) can still be uploaded with `POST /api/historical-imports`.

## Reading it

An imported season is a season like any other, read through the same endpoints and pages as one run in the app or by
the Discord bot; there is no separate archive API. What makes it imported is `Season.HistoricalSourceID` being set.

- **Seasons**: `GET /api/seasons` (the seasons page and the home page's past seasons) and
  `GET /api/seasons/{seasonID}/summary` (the season page). Every DTO of or about a season carries `SeasonInfoDto`, projected in
  one place (`Seasons/SeasonInfoDto.cs`).
- **Stats**: `SeasonStatsService` is the only reader of season player stats, for the season page, team page, profiles and
  leaderboards. A player's phase of a season (regular season or playoffs) is totalled from its games where the app has
  them (`PlayerGameStatsQuery`: Waypoint matches and recorded MCC games), otherwise from the sheet's stored totals
  (`PlayerSeasonStat`), never both. Career totals are `CareerTotals`, over seasons and Late League alike.
- **Late League** is matchmaking (random, skill-matched sides, like the queue), not a season: the import makes each
  archive Late League season (league or format Late League, or a name starting "Late League") a `MatchmakingSeason`,
  its games finished `MatchedMatch`es of recorded games, and its sheet totals `MatchmakingPlayerStat` rows. No teams,
  series, playoffs or titles. `MatchmakingStatsService` reads its stats (games first, as for seasons), for
  `GET /api/late-league/queue` (the Discord queue's games: Late League as it is played now), `GET /api/late-league/seasons`,
  `GET /api/late-league/seasons/{matchmakingSeasonID}`, profiles and the career leaderboards.
- **Records and titles**: a team's record is `TeamRecord.Of` (its matches, or its sheet's record when its matches are
  not in the app); titles are written by `ChampionshipService`, from the bracket for the app's seasons and from the
  champions record for imported ones (their bracket is rebuilt from game order and decides nothing).
- **Editing**: an imported season can't be edited in the season editor: the next import would revert the edit. Correct
  it in its spreadsheet.
