# Historical import

The historical Grifball archive (AGLA, Late League, GWL, IGL and side leagues, 2008 onwards) is **not in the repository**.
The app downloads and imports it itself; the Node `archive-tools` scripts that used to compile it are gone.

## How it works

- **Sources** (`Event.HistoricalSources`): every workbook, page and channel of the archive, from the catalog in
  `GrifballWebApp.Historical/Sources/HistoricalSourceCatalog.cs` (written to the table at runtime: a fresh
  environment needs no setup). Each entry says what the import reads from it (season stats tabs, database tables of stats
  with a season column (`StatsTable`: the grifball.info backup, split by season, division and phase, its season IDs named
  in its entry since the workbook has no names), Waypoint game lists, per-game carnage tabs, the champions record, the
  Late League players list, the team standings). Dead sources are listed, never downloaded.
- **Snapshots** (`Event.HistoricalSourceSnapshots`): a refresh downloads each enabled source (Google Sheets as their
  xlsx export, a tab's CSV when the export is refused; pages at their URL; Wayback captures at their permanent URL)
  and keeps the content only when its SHA-256 changed. The import reads each source's latest snapshot, so a sheet that
  went private or was deleted still imports from its last one. Snapshots are in the database, so they ride its backups.
- **Parsing** (`GrifballWebApp.Historical/Parsing`): the xlsx is read with ExcelDataReader into text cells and parsed into the same
  `HistoricalArchiveInput` the compiled JSON used to be; `HistoricalImportService` stays the only importer.
- **Stat columns** (`SeasonStatsParser.Columns`): a stats tab's columns are found by their headers. The Infinite stats
  tool's tabs (2025 on) count rounds, not games (an Infinite Grifball game is several rounds): their Rounds Won, Lost
  and Tied (and FL24's Goal Assists) are kept in the line's extended metrics, and the games played, wins and losses come
  from the season's per-game tab (a tab's `GamesSheet`: the Waypoint match export, a row per player per game, its
  Outcome 2 a win and 3 a loss). A per-game average (SL24's AVG Score, AVG Damage Dealt, AVG Damage Taken) is kept as
  its total, the average times the games.
- **Standings** (`StandingsParser`, a source's `Standings` tabs): a season's team standings in any of its sheets' layouts (a team per
  row under headers, a team per column beside labels, or a list of "Team (captain) (W-L)" cells), its columns found by their
  headers: each team's record, its rank (seed: Rank, Seed, Standing, or the table's order) and its final place (Place
  Finished, Playoffs, Playoff Ending). A standings name a few letters off the stats' team takes the stats' name. A team
  keeps them as `Team.ArchivedRank` and `ArchivedFinish`; the season page orders its standings by the sheet's ranks (one
  table's) and shows each team's finish.
- **Ladders** (`LadderParser`, a source's `Ladders` tabs and its `FinishesSheet`): a Late League season's ranked ladder, a row
  per player under headers (Division, League, Rank, LP, Streak, MMR, P/ Win, P/ Loss), in the tab's order. The past finishes
  tab ("Past Seasons": League, Rank and Pos under each season's heading) gives a player's place on its ladder. A ladder goes
  on its season (by name), or on a season of its own when no stats tab has one (the early seasons). Each row is a
  `MatchmakingLadderRank`; the Late League season page shows them under Ladder.
- **Brackets** (`BracketParser`, a source's `Brackets`): a season's playoff bracket as its sheets draw it, either cell by
  cell (each series' two team cells, their seeds, its score and the cell its winner went on to: the layouts are in
  `HistoricalBracketLayouts`) or from a tab per round of game rows (Respawn). The import makes each series a slot of the
  app's bracket (winners', losers', grand final), each side from the slot its team last played or by its seed (the
  sheet's, else its standings rank), its result the sheet's; the season's playoff games go in the slot of their teams.
  A season without a bracket sheet still has its bracket rebuilt from the order its playoff games were played in.
- **Drafts** (`DraftParser`, a source's `Drafts` tabs): a season's draft board (a team per column: Team Name, Captain, and a
  row per round, a row of numbers above a round numbering its picks) or a TeamInfo table ("Draft Order", "Seed #: Draft
  Spot 3", or "Draft Pick" codes: C3 for the 3rd captain, R2P3 for a 2nd-round pick). A board's team is one the season
  has (by name, or its captain's team); its captain and picks are the season's players, a few letters off at most (a
  nickname is left out). The team's captain, its place in the draft order and each pick's round (and number, when the
  board gives one or its `Order` is `Linear`) are kept on its roster (`TeamPlayer.DraftCaptainOrder`, `DraftRound`,
  `DraftPick`), as the app's own draft keeps them. A board whose columns are no draft order (`Order` `None`: a bid draft's teams)
  gives captains and rounds but no order; bid prices wait for the bid draft (#19).
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
  its games finished `MatchedMatch`es of recorded games, its sheet totals `MatchmakingPlayerStat` rows and its ladder
  `MatchmakingLadderRank` rows. No teams,
  series, playoffs or titles. `MatchmakingStatsService` reads its stats (games first, as for seasons), for
  `GET /api/late-league/queue` (the Discord queue's games: Late League as it is played now), `GET /api/late-league/seasons`,
  `GET /api/late-league/seasons/{matchmakingSeasonID}`, profiles and the career leaderboards.
- **Records and titles**: a team's record is `TeamRecord.Of` (its matches, or its sheet's record when its matches are
  not in the app); titles are written by `ChampionshipService`, from the bracket for the app's seasons and from the
  champions record for imported ones (their bracket, the sheet's or one rebuilt from game order, decides nothing).
- **Editing**: an imported season can't be edited in the season editor: the next import would revert the edit. Correct
  it in its spreadsheet.

## Testing against the real archive

`GrifballWebApp.HistoricalArchive.Test` runs the import on the whole archive, not on workbooks built in a test: each
source's sheet is parsed with the catalog's current config and imported into a fresh SQLite database, and its tests
say what every season should have (teams, stats, captains, a champion, a bracket when it had playoffs), each naming the
seasons that fall short. CI builds it but never runs it. Run it while working on the import:

```sh
# Fill the copy (.historical-archive/ in the repository, ignored by git) from the test environment's snapshots and
# Google, then run. The fill only reads the database (ApplicationIntent=ReadOnly is a hint, not a guard). Keep the
# database's password in the environment (DB_PASSWORD), never on a command line that is logged.
kubectl -n grif-test port-forward svc/sqlserver 14330:1433 &
GRIF_HISTORICAL_FILL=both \
GRIF_HISTORICAL_DATABASE="Server=127.0.0.1,14330;Database=GrifballWebApp;User Id=grif_app;Password=$DB_PASSWORD;Encrypt=True;TrustServerCertificate=True;ApplicationIntent=ReadOnly" \
  dotnet test GrifballWebApp.HistoricalArchive.Test

# Afterwards, from the copy as it is (a config or parser change needs no fetching):
dotnet test GrifballWebApp.HistoricalArchive.Test
```

- `GRIF_HISTORICAL_FILL`: `database` (each source's latest snapshot in `GRIF_HISTORICAL_DATABASE`; it has the sheets
  that have gone private), `download` (as a refresh downloads them), or `both` (the database's, then a download over each
  still public). Unset: the copy as it is. With no copy, every test is ignored.
- `GRIF_HISTORICAL_DATABASE_PROVIDER`: the database's kind, `SqlServer` by default.
- `GRIF_HISTORICAL_ARCHIVE`: the copy's folder, instead of `.historical-archive/`.

A run leaves `import.db` (the imported database, to query) and `summary.md` (each season's teams, captains, stats,
matches, playoff matches and titles, and the build's and import's warnings) in the copy's folder. A fill and run take
about 4 minutes; a run from the copy, under 2.

### In CI: `historical-archive.yml` and `historical-archive-public.yml`

The same tests run in CI: `historical-archive-public.yml` on every PR that changes the import (`GrifballWebApp.Historical`,
the real-archive tests, `Application/Historical`, `Domain/Archive`, the workbook reader), and either by hand (Actions >
Run workflow, on any branch).
Both fill a copy once, run this commit's import and its base's on it, and report each season whose counts changed: in
the run's summary, and on a PR from this repository a comment each, kept up to date. The import's known gaps don't fail
them. The summaries and imported databases are each run's artifact. They share `historical-archive-run.yml`; they
differ in where they run, so in what their copy can have:

- **`historical-archive.yml`**: the org's in-cluster runners (`grif-k8s`). By hand only: every source the import reads is
  public, so the PRs' runs are GitHub's, and the cluster's memory stays the cluster's (its PR trigger is in the
  workflow, commented out, for a sheet the import reads going private). The test environment's snapshots, through
  the chart's read-only `database.logins.archiveReader`, then a download: every source, a sheet that went private
  included. The test environment scales to zero when idle: the run first wakes it through Sablier
  (`.github/scripts/sablier-wake.sh`), up to 3 minutes for it to be ready and 3 more for its SQL Server's port, and keeps
  it up 30 minutes. Never a fork's PR: a self-hosted runner doesn't run outside code.
- **`historical-archive-public.yml`**: GitHub's runners, downloaded only, a fork's PR included. A sheet that went
  private or was deleted, which only the database still has, isn't in its copy: `Copy_HasEverySourceTheImportReads`
  warns of it instead of failing, the report lists it, and its seasons aren't imported on either side.
