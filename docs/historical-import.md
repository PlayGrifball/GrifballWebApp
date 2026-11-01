# Historical import

The historical Grifball archive (AGLA, Late League, GWL, IGL and side leagues, 2008 onwards) is **not in the repository**.
The app downloads and imports it itself; the Node `archive-tools` scripts that used to compile it are gone.

## How it works

- **Sources** (`Event.HistoricalSources`): every workbook, page and channel of the archive, from the catalog in
  `GrifballWebApp.Server/Historical/Sources/HistoricalSourceCatalog.cs` (written to the table at runtime: a fresh
  environment needs no setup). Each entry says what the import reads from it (season stats tabs, Waypoint game lists,
  per-game carnage tabs, the champions record, the Late League players list). Dead sources are listed, never downloaded.
- **Snapshots** (`Event.HistoricalSourceSnapshots`): a refresh downloads each enabled source (Google Sheets as their
  xlsx export, a tab's CSV when the export is refused; pages at their URL; Wayback captures at their permanent URL)
  and keeps the content only when its SHA-256 changed. The import reads each source's latest snapshot, so a sheet that
  went private or was deleted still imports from its last one. Snapshots are in the database, so they ride its backups.
- **Parsing** (`Historical/Parsing`): the xlsx is read with ExcelDataReader into text cells and parsed into the same
  `HistoricalArchiveInput` the compiled JSON used to be; `HistoricalImportService` stays the only importer.
- **Jobs**: everything runs in the worker role through the historical import job queue (one job at a time):
  refresh, preview (a dry run: each season's transaction rolled back) and import.
- **Schedule** (`HistoricalImport:Schedule:Interval`, daily by default): a scheduled refresh queues a guarded import of
  the seasons the changed sources make up, and nothing when nothing changed. It can be paused on the page.
- **Guardrail** (`HistoricalImport:Guardrail`): a guarded import stops a season that would remove more than 20% of its
  stat rows or teams, or any championship, and lists it for review; a sysadmin applies or skips it.

## Using it

Sysadmins: User management > Historical Import (`/usermanagement/historicalimport`). "Refresh sources" downloads;
"Import" runs a preview first, listing what each season would lose, then "Apply this import" imports exactly what was
previewed. A compiled archive (the old JSON files) can still be uploaded with `POST /HistoricalImport/Import`.
