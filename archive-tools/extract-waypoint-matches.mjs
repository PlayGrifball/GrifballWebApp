// Extracts Halo Infinite match IDs from the raw workbooks' game history sheets (Waypoint, Halo Data Hive or HaloQuery match
// links) and writes data/historical-archive/compiled/waypoint-matches.json, keyed by each season's HistoricalSourceID
// ("{sourceWorkbook}/{sourceSheet}" of its regular-season entry in unified-historical-seasons.json):
//   { "<HistoricalSourceID>": { "regular": ["<match guid>", ...], "playoffs": [...] } }
// import-archive.mjs uploads it with the import, which pulls those matches and derives the seasons' stats from them.
//
// Run from the repository root: node archive-tools/extract-waypoint-matches.mjs
import fs from 'node:fs';
import path from 'node:path';

const BASE_DIR = path.resolve('data/historical-archive');
const JSON_DIR = path.join(BASE_DIR, 'json');
const COMPILED_DIR = path.join(BASE_DIR, 'compiled');

// Each Halo Infinite season and the sheets listing its games. A sheet's rows are split into weeks by its first column:
// from a row whose first cell says "Playoffs" (or Finals/Semis/Quarters/Bracket) on, games are playoff games.
// Left out on purpose: the hidden "Game History (OLD)" sheets copied into the 2025 workbooks (a template's leftovers,
// not those seasons' games) and per-player sheets (MatchBot, "Sheet hist"), whose games are all in these anyway.
export const SEASONS = [
  { season: { workbook: '2023-da-wl23-stats' }, sheets: [['AGLA-2023/2023-da-wl23-schedule', 'Game History'], ['AGLA-2023/2023-da-wl23-stats', 'Match History']] },
  { season: { workbook: '2024-da-sl24-stats' }, sheets: [['AGLA-2024/2024-da-sl24-schedule', 'Game History']] },
  { season: { workbook: '2024-igl-evolved-fl24-stats' }, sheets: [['IGL-2024/2024-igl-evolved-fl24', 'Game History']] },
  { season: { workbook: '2025-octane-wl25-stats' }, sheets: [['AGLA-2025/2025-octane-wl25-schedule', 'Game History']] },
  { season: { workbook: '2025-octane-sl25-bid-stats' }, sheets: [['AGLA-2025/2025-octane-sl25-bid-schedule', 'Game History']] },
  { season: { workbook: '2025-classic-sml25-pro-stats' }, sheets: [['AGLA-2025/2025-classic-sml25-schedules', 'Pro Game History']] },
  { season: { workbook: '2025-classic-sml25-legacy-stats' }, sheets: [['AGLA-2025/2025-classic-sml25-schedules', 'Legacy Game History']] },
  { season: { workbook: '2026-sl26-minor-stats-schedule' }, sheets: [['AGLA-2025/2026-sl26-minor-stats-schedule', 'Game History']] },
  { season: { workbook: '2026-sl26-major-stats' }, sheets: [['AGLA-2025/2026-sl26-major-schedule', 'Game History']] },
];

const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;
const LINK = /(halowaypoint\.com|halodatahive\.com|haloquery\.com)/i;
const PLAYOFFS = /playoff|final|semi|quarter|bracket/i;

/** The match GUIDs a cell links to (several links in one cell are several games), or null for a link without one. */
export function matchIds(cell) {
  const text = String(cell ?? '');
  if (!LINK.test(text))
    return [];
  const found = [];
  for (const part of text.split(/\s|,|;/).filter(p => LINK.test(p))) {
    const ids = part.match(GUID);
    if (!ids)
      return null;
    // A match link's ID is its last GUID (/players/<gamertag>/matches/<id>, /Infinite/Match/<id>, /matches/<id>)
    found.push(ids[ids.length - 1].toLowerCase());
  }
  return found.length ? found : null;
}

/** The sheet's games: { regular: Set, playoffs: Set, unparsed: [cell text] }. */
export function extractSheet(rows) {
  const result = { regular: new Set(), playoffs: new Set(), unparsed: [] };
  let playoffs = false;
  for (const row of rows) {
    const first = String(row[0] ?? '').trim();
    if (first)
      playoffs = PLAYOFFS.test(first);
    for (const cell of row) {
      const ids = matchIds(cell);
      if (ids === null)
        result.unparsed.push(String(cell).trim());
      else
        ids.forEach(id => (playoffs ? result.playoffs : result.regular).add(id));
    }
  }
  return result;
}

function readSheet(file, name) {
  const workbook = JSON.parse(fs.readFileSync(path.join(JSON_DIR, `${file}.json`), 'utf8'));
  const sheets = Array.isArray(workbook.fullSheets) ? workbook.fullSheets : Object.values(workbook.fullSheets ?? {});
  const sheet = sheets.find(s => s.name === name);
  if (!sheet)
    throw new Error(`${file}.json has no sheet "${name}"`);
  return sheet.rows ?? [];
}

export function extract(unified) {
  const output = {};
  const report = [];
  for (const { season, sheets } of SEASONS) {
    const entries = unified.seasons.filter(s => s.sourceWorkbook === season.workbook);
    const primary = entries.find(e => !e.isPlayoffs) ?? entries[0];
    if (!primary) {
      report.push({ season: season.workbook, error: 'not in unified-historical-seasons.json' });
      continue;
    }
    const key = `${primary.sourceWorkbook.trim()}/${primary.sourceSheet.trim()}`;
    const regular = new Set();
    const playoffs = new Set();
    const unparsed = [];
    for (const [file, name] of sheets) {
      const sheet = extractSheet(readSheet(file, name));
      sheet.regular.forEach(id => regular.add(id));
      sheet.playoffs.forEach(id => playoffs.add(id));
      unparsed.push(...sheet.unparsed.map(text => `${file} / ${name}: ${text}`));
    }
    // A game listed as both (two sheets disagreeing) is a playoff game
    playoffs.forEach(id => regular.delete(id));
    if (regular.size || playoffs.size)
      output[key] = { regular: [...regular].sort(), playoffs: [...playoffs].sort() };
    report.push({ season: primary.seasonName, key, regular: regular.size, playoffs: playoffs.size, unparsed });
  }
  return { output, report };
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const unified = JSON.parse(fs.readFileSync(path.join(COMPILED_DIR, 'unified-historical-seasons.json'), 'utf8'));
  const { output, report } = extract(unified);
  fs.writeFileSync(path.join(COMPILED_DIR, 'waypoint-matches.json'), JSON.stringify(output, null, 2) + '\n');
  for (const r of report) {
    if (r.error) {
      console.log(`${r.season}: ${r.error}`);
      continue;
    }
    console.log(`${r.season} [${r.key}]: ${r.regular} regular, ${r.playoffs} playoff matches`);
    for (const text of r.unparsed)
      console.log(`  unparsed link: ${text}`);
  }
  console.log(`Wrote ${path.join(COMPILED_DIR, 'waypoint-matches.json')}`);
}
