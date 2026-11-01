# archive-tools

Scripts that downloaded the historical Grifball spreadsheets and compiled them into the JSON the historical import
reads (`unified-historical-seasons.json`, `agla-champions.json`, `catalog.json`, `waypoint-matches.json`).

The downloaded archive (`data/historical-archive/`: the raw workbooks, Wayback and htmlview captures, CSV/JSON exports and
the compiled files) is **not in the repository**. It is kept outside git as an offline backup; nothing in the app or the
tests depends on it. The tests use a few seasons of the compiled archive in `GrifballWebApp.Test/Fixtures/HistoricalArchive`.

These scripts are being replaced by the import inside the app (sources and snapshots in the database, parsed in .NET).
