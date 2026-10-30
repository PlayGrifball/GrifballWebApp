# Historical Grifball Data Archive — Audit Report & Refinement Plan

- **Audit Date:** 2026-10-30
- **Scope:** 65 historical Grifball links (2008–2026), 51 recovered spreadsheet workbooks, 3 web/Wayback snapshots, 792 worksheet tabs (442 visible + 350 hidden), 85,611 non-empty rows, and 2,900,000+ individual cells.
- **Scripts:**
  - `archive-tools/download-all.mjs` — Multi-strategy downloader (`.xlsx` export, Google Visualization CSV `/gviz/tq?tqx=out:csv`, `/htmlview`, YouTube RSS/landing page, Internet Archive Wayback Machine CDX API).
  - `archive-tools/extract-and-compile.mjs` — Zero-dependency `.xlsx` ZIP/XML + `xl/styles.xml` parser and CSV/JSON compiler.
  - `archive-tools/build-summary.mjs` — Builds normalized master JSON datasets (`catalog.json`, `agla-champions.json`, `all-time-infinite-stats.json`, `late-league-master.json`, `youtube-vods.json`) and `README.md`.
  - `archive-tools/audit-archive.mjs` — Automated cell-by-cell, style-by-style, and formula-by-formula verification suite.

---

## Part 1: Cell-Level & Parser Fixes Already Applied

### 1. Date, Time, and Elapsed Duration Formatting (`xl/styles.xml`) — 6,278 Cells Across 27 Workbooks
Google Sheets `.xlsx` exports store dates, match times, and elapsed durations as raw IEEE-754 serial numbers (days since `1899-12-30` or fractions of a 24-hour day) and rely on `xl/styles.xml` (`<numFmt>` and `<cellXfs>`) for display formatting.

We audited all **29 custom `formatCode` definitions** and all built-in Excel `numFmtId` codes across all 50 `.xlsx` workbooks and updated `extract-and-compile.mjs` to format every styled numeric cell identically to Google Sheets:
- **Elapsed Durations (`[h]:mm:ss`, `numFmtId=46`, `h":"mm":"ss`, `hh":"mm":"ss`, `hh:mm:ss`, `mm:ss`, `mm":"ss`):**
  - Affected workbooks: `2020-fl20-pc-schedule-stats`, `2021-wl21-legacy-stats`, `2021-wl21-rookie-stats`, `2021-sml21-pro-stats`, `2021-sml21-ammy-stats`, `2019-sl19-schedule-stats`, `2019-wl19-schedule-stats`, `side-reach-pc-stats`.
  - Example: `Time` (`J2` = `0.3377546296`) is now recorded as `8:06:22` instead of a raw decimal.
- **Average Life in Seconds (`":"ss`):**
  - Affected workbooks: `2020-fl20-pc-schedule-stats`, `2021-wl21-legacy-stats`, `2021-wl21-rookie-stats`, `2021-sml21-pro-stats`, `2021-sml21-ammy-stats`.
  - Example: `AVG Life` (`T2` = `0.00030370698`) is now recorded as `:26` instead of `0.00030370698`.
- **Scheduled Match Dates & Times (`m/d h:mm am/pm`, `m/d/yy h:mm am/pm`, `h:mm am/pm`, `h:mm:ss am/pm`, `m/d/yyyy h:mm:ss`):**
  - Affected workbooks: `2022-h3-action-schedule`, `2022-h3-action-stats`, `2022-h4-bid-stats`, `2025-octane-wl25-schedule`, `2025-octane-sl25-bid-schedule`.
  - Example: `44769.895833333336` -> `7/27 9:30 PM`; `44754.791666666664` -> `7/12/22 7:00 PM`; `0.875` -> `9:00 PM`.
- **Calendar Dates (`m/d/yyyy`, `M/d/yyyy`, `m-d-yyyy`, `m/d`, `m-d`, `d-m`, `mmmm d`, `dddd mmm d`):**
  - Affected workbooks: 27 workbooks across 2018–2026 schedules, bid sheets, and patch logs.
  - Example: `43604.0` -> `5/19/2019`; `44750.0` (`m-d`) -> `7-8`.
- **Unstyled Serial Timestamps in Late League Match History (`*_MatchH` tabs in `late-league-aspence-main`):**
  - Column A (`Timestamp`) in `H3S5_MatchH`, `H3S6_MatchH`, `H3S7_MatchH`, `H3S8_MatchH`, `HRS7_MatchH`, `H4S7_MatchH`, etc. stored unstyled serial floats like `44173.7307813`, which are now formatted as `12/8/2020 17:32:19`.

### 2. Integer Trailing `.0` Normalization — 1,886,547 Cells
Google Sheets `.xlsx` exports write integer values inside `<v>` tags with `.0` (e.g., `<v>18.0</v>` for Games Played, `<v>0.0</v>` for Betrayals).
- `extract-and-compile.mjs` now strips trailing `.0` from all integer values (unless the cell's style explicitly formats 1 decimal place via `formatCode="0.0"`), fixing **1,886,547 cells** across all 792 CSVs and 51 JSON workbooks.

### 3. Visibility Metadata (`hidden: true | false`) Across All 792 Tabs
- Of the 792 extracted worksheet tabs, **442 are visible** and **350 are hidden** (`state="hidden"` in `xl/workbook.xml`).
- All 792 tabs are extracted, and `hidden: true | false` is now recorded on every sheet in `catalog.json`, `README.md`, and each per-workbook JSON file.

### 4. Verification of Google Sheets `QUERY()` & `IMPORTRANGE()` (`__xludf.DUMMYFUNCTION`) Tabs
- When Google Sheets exports a sheet containing `=QUERY(...)` or `=IMPORTRANGE(...)` to `.xlsx`, it writes the evaluated cached value `<v>...</v>` into every cell and wraps the formula in `IFERROR(__xludf.DUMMYFUNCTION("..."), cachedError)`.
- We audited all **53 tabs** containing `__xludf.DUMMYFUNCTION` and cross-checked them against Google's live `/gviz/tq?tqx=out:csv` endpoint.
- **Finding:** 100% of evaluated `QUERY()` results were already preserved in the `.xlsx` `<v>` tags. Any cell containing `#REF!`, `#VALUE!`, or `#NUM!` is genuinely broken in the live Google Sheet itself (see Part 2, Section C below).

---

## Part 2: Data Anomalies & Refinement Questions for Review

### A. `AGLA Champions` (`history-agla-champions` / `compiled/agla-champions.json`)
The `AGLA Champions` sheet records **76 championship teams** with **434 total player appearances** in the left table (Columns A–L), and **434 total rings** summed on the right-hand Championship Leaderboard (Columns N–U) across **264 rows**.

Our cross-check between the left-side team rosters and the right-side leaderboard uncovered **5 specific anomalies**:

#### 1. Wrong Game Column for `Winter League 2021` (`MCC Halo 3`, Amateur — `8th Place`, Row 66)
- **Issue:** On the left table (Row 66), **`8th Place`** (`Fox Fahrenheit`, `Sammisaibot`, `MuNgO PuNgO`, `Chibifoxkit1`, `VinnyHaw`) won **Winter League 2021 (`MCC Halo 3`, Amateur)**.
- However, on the right-hand Leaderboard, all 5 players had that championship recorded under the **`Infinite`** column (Col T) instead of the **`Halo 3`** column (Col P):
  - `Fox Fahrenheit` (Row 55): `Total: 2` (`Reach: 1, Infinite: 1`) -> should be `Reach: 1, Halo 3: 1`
  - `Sammisaibot` (Row 57): `Total: 2` (`Reach: 1, Infinite: 1`) -> should be `Reach: 1, Halo 3: 1`
  - `MuNgO PuNgO` (Row 235): `Total: 1` (`Infinite: 1`) -> should be `Halo 3: 1`
  - `Chibifoxkit1` (Row 237): `Total: 1` (`Infinite: 1`) -> should be `Halo 3: 1`
  - `VinnyHaw` (Row 238): `Total: 1` (`Infinite: 1`) -> should be `Halo 3: 1`
- **Refinement Question #1:** Should we move those 5 rings from `infinite` to `halo3` in `compiled/agla-champions.json`?

#### 2. Duplicate / Split Players on the Championship Leaderboard
- **`Chibifoxkit1`** appears **twice** on the leaderboard with identical spelling:
  - Row 236: `Chibifoxkit1` — `1` ring (`Halo Reach`, Spring League 2020 `Free Ship`)
  - Row 237: `Chibifoxkit1` — `1` ring (`Infinite` [actually WL21 Halo 3], `8th Place`)
- **`Rekamcipdiputs` (Row 126, 1 H4 ring) & `rekamcipdiputs` (Row 127, 1 H4 ring):**
  - Split across two consecutive rows due to capitalization (`Summer League 2014 H4 Ammy — Alpha status` and `Fall League 2014 H4 Ammy — Aftershock`).
- **`Alwaystrollin` (Row 253, 1 H4 ring) & `AlwaysTrollin` (Row 254, 1 H5 ring):**
  - Split across two consecutive rows due to capitalization (`Spring League 2014 H4 Ammy — Strictly Business` and `Spring League 2017 H5 Ammy — B E N C H W A R M E R S`).
- **`Trespa5s` (Row 26, 3 rings: 1 H3, 1 H5, 1 Split) & `Tre5pass` (Row 93, 1 Infinite ring in Fall League 2024 `Asking Alexandra`):**
  - Split into two separate entries instead of a single 4-ring entry.
- **Refinement Question #2:** Should we merge `Chibifoxkit1` (2 rings), `Rekamcipdiputs` (2 rings), `AlwaysTrollin` (2 rings), and `Trespa5s` / `Tre5pass` (4 rings) on the compiled leaderboard?

#### 3. Same Player Listed Twice on the *Same* Championship Team Roster (and Counted Twice on the Leaderboard)
- **`Fall League 2014, Halo 4, Amateur — Aftershock` (Row 40):**
  - Lists **`About73Puppies`** as Captain (Col E) AND **`AB0UT73PUPPIES`** (with a zero `0`) as Player 4 (Col H) on the *exact same team*, and gives them two separate 1-ring rows on the leaderboard (Rows 258 & 259).
- **`Summer League 2010, Halo 3, Amateur — Relentless` (Row 21):**
  - Lists **`V IE R I T A S`** as Player 2 (Col F) AND **`Veritas`** as Player 6 (Col J) on the *exact same team*. On the leaderboard, `Veritas` has 2 H3 rings (Row 44, from Spring 2010 `Defiance` + Summer 2010 `Relentless`) while `V IE R I T A S` also has 1 H3 ring (Row 87, from Summer 2010 `Relentless`).
- **Refinement Question #3:** Were `About73Puppies` / `AB0UT73PUPPIES` (on `Aftershock`) and `V IE R I T A S` / `Veritas` (on `Relentless`) accidentally listed twice on those rosters, or were they separate players/accounts?

#### 4. Silent Alias Consolidations on the Leaderboard (20 Players Across 31 Alternate Gamertag Appearances)
The sheet author consolidated the following gamertag variations on the leaderboard:
| Leaderboard Name | Total Rings | Roster Appearances Consolidated |
|---|---:|---|
| `Major Silva` | 10 | `Major Silva` (3), `Silva` (4), `I MajorSilva I` (1), `I Major Silva I` (1), `The Silvaa` (1) |
| `ACE B S1lentND1` | 9 | `ACE B S1lentND1` (8), `AceBS1lentND1` (1) |
| `ixGingy` | 6 | `ixGingy` (5), `Gingy` (1) |
| `DlGITAL PAIN` | 6 | `DlGITAL PAIN` (4), `DIGITAL PAIN` (2) |
| `Regresser` | 5 | `Regresser` (4: 2 H3, 2 Reach) + **`Oh My Godlxss`** (1 H4: Summer 2014 Pro `Next Dimenstion`) |
| `Scet Shin` | 4 | `Scet Shin` (2), `oScet` (1), `Scet` (1) |
| `OVERweightGOAT` | 4 | `OVERweightGOAT` (1), `overweightgoat` (1), `Overweighttank` (1), `OverweightTank` (1) |
| `TommyWstSide793` | 3 | `TommyWstSide793` (1), `TommyWstSide` (2) |
| `The Piggies` | 3 | `The Piggies` (1), `x352x piggies` (2) |
| `PriestXYZ` | 3 | `PriestXYZ` (1), `Priest XYZ` (2) |
| `ii flowers ii` | 3 | `ii flowers ii` (1), `l Flowers l` (1), `I Flowers I` (1) |
| `Crusader0102` | 3 | `Crusader0102` (2), `OHW Crusader` (1) |
| `cxrnchip` | 2 | `cxrnchip` (1 H3) + **`DI Cell4225`** (1 Reach: Summer 2020 Reach PC `FTN`) |
| `SilentShadow459` | 2 | `SilentShadow459` (1), `SilentShadow451` (1) |
| `KING RICKXL` | 2 | `KING RICKXL` (1), `KING RICK XL` (1) |
| `ID I2 IA G O N` | 2 | `ID I2 IA G O N` (1), `THEXILEDRAGON` (1) |
| `I DIM3 I` | 2 | `I DIM3 I` (1), `oDime` (1) |
| `L1ght5aber` | 2 | `L1ght5aber` (1), `Paragon Saber` (1) |
| `AJDaJuiceMayne` | 2 | `AJDaJuiceMayne` (1), `AJ DaJuiceMayne` (1) |
| `a man canon` | 2 | `a man canon` (1), `the man canon` (1) |

- **Refinement Question #4:** Can you confirm that `Oh My Godlxss` = `Regresser` and `DI Cell4225` = `cxrnchip`? Should we add an explicit `aliases` array and `championshipsWon` list to every leaderboard entry in `compiled/agla-champions.json`?

---

### B. `All-time Infinite Stats by Luxe` (`history-all-time-infinite-stats-luxe` / `compiled/all-time-infinite-stats.json`)

1. **Season Coverage (`S1`–`S7` vs. `SL26 Major` / `S8`):**
   - Luxe's workbook has tabs `S1`, `S1 Playoffs`, `S2`, `S3`, `S4`, `S4 Playoffs`, `S5`, `S6`, and `S7` (`SL26 Minor Season`), while **`SL26 Major Season` (`S8`)** is in its own separate workbook (`2026-sl26-major-stats`).
   - **Refinement Question #5:** Should `compiled/all-time-infinite-stats.json` remain a strict 1:1 mirror of Luxe's sheet (`S1`–`S7`), or should we also include `S8` (`SL26 Major`) from `2026-sl26-major-stats`?
2. **Header Normalization Across Seasonal Tabs:**
   - In `All Time`, Column L (between `Player Grade` and `Playoff W/L`) has a blank header in the sheet and stores the numeric grade rank (`1` = `S+` down to `21` = `F-`), which we mapped to `gradeRank` in `all-time-infinite-stats.json`.
   - Row 109 of `All Time` had no gamertag and a single stray `"s"` in Column AD (`MultiKills PM`), which we excluded from the compiled player array.
   - In `S2` and `S3`, cell `A1` is `"NO PLAYOFFS - except W/L"`, and in `S4` and `S4 Playoffs` cell `A1` is blank `""` instead of `"Gamertag"`.
   - **Refinement Question #6:** Should we normalize the first column key across all seasonal tabs (`S1`–`S7`) in `all-time-infinite-stats.json` to `"gamertag"`?

---

### C. Broken Formulas in Source Sheets & Unlisted Links Discovered

1. **`Team Report Cards` Tabs with Unmatched Selector Lookups:**
   - In `2024-da-sl24-stats`, `2025-octane-wl25-stats`, `2025-classic-sml25-legacy-stats`, `2026-sl26-minor-stats-schedule`, and `2026-sl26-major-stats`, the `Team Report Cards` tab uses `=QUERY(TeamInfo!A:J, "Select D WHERE C='"&B2&"'", 0)` which evaluates to `#VALUE!` in the live Google Sheets when `B2` doesn't match `TeamInfo!C:C`. All underlying team rosters and player stats are 100% preserved in the `TeamInfo`, `Teams`, `Halo Infinite Stats`, and `Season+Playoffs Stats` tabs of those workbooks.
2. **`Playoff Stats Sheet stats` Tabs in 2026 Minor/Major:**
   - In `2026-sl26-minor-stats-schedule` and `2026-sl26-major-stats`, the hidden tab `Playoff Stats Sheet stats` has `=QUERY(Playoff!A:GT, ...)` returning `#VALUE!` because the `Playoff` tab is empty, whereas the actual playoff stats are preserved in `Playoff Stats` and `Playoff Review HI_MatchH`.
3. **Unlisted Google Sheets & Challonge Brackets Discovered in Formulas/Hyperlinks:**
   - Two unlisted Google Sheets referenced inside `IMPORTRANGE` formulas were tested and confirmed deleted (`410 Gone`):
     - `1d-JA1rtr6SsQ3KDwXKl9d69yCGp7i6YiLm1GlgrUU-M` (referenced in `Sheet5` of 2024/2025 schedule/draft workbooks)
     - `1n7CiDdFBDtXZZYs2U-QOvuRqzhUZIZYAOBRmC3_LlzI` (referenced in `2024-da-sl24-stats` -> `11_PlayerExtra.csv` cell `J5`)
   - Three additional Challonge bracket URLs were discovered in `gwl-ts1-halo4` and `gwl-ts2-reach`:
     - `https://challonge.com/Halo4FallSeriesEventTwo`
     - `https://challonge.com/halo4grifball`
     - `https://challonge.com/haloreachgrifball`
