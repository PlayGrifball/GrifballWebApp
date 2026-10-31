import fs from 'node:fs/promises';
import path from 'node:path';

const BASE_DIR = path.resolve('data/historical-archive');
const RAW_DIR = path.join(BASE_DIR, 'raw');
const JSON_DIR = path.join(BASE_DIR, 'json');
const COMPILED_DIR = path.join(BASE_DIR, 'compiled');

function toNum(val) {
  if (val === null || val === undefined || val === '') return null;
  const cleaned = String(val).replace(/,/g, '').trim();
  // Whitespace-only cells are empty, not zero (Number(' ') === 0).
  if (cleaned === '') return null;
  // Integers beyond 2^53 (e.g. 17–19 digit Discord snowflake IDs) would be silently
  // rounded by Number(); keep them as exact strings instead.
  if (/^-?\d+$/.test(cleaned) && !Number.isSafeInteger(Number(cleaned))) return cleaned;
  const n = Number(cleaned);
  return Number.isFinite(n) ? n : val;
}

async function buildAglaChampions() {
  const raw = JSON.parse(await fs.readFile(path.join(JSON_DIR, 'History/history-agla-champions.json'), 'utf8'));
  const sheet = raw.fullSheets.find(s => s.name === 'AGLA Champions');
  const rows = sheet.rows;

  const champions = [];
  const leaderboard = [];

  for (let i = 2; i < rows.length; i++) {
    const r = rows[i] || [];
    const season = (r[0] || '').trim().replace(/:$/, '');
    const game = (r[1] || '').trim();
    const league = (r[2] || '').trim();
    const team = (r[3] || '').trim();
    const roster = r.slice(4, 12).map(x => (x || '').trim()).filter(Boolean);

    if (season && team) {
      champions.push({
        season,
        game,
        league: league === '-' ? 'Open' : league,
        team,
        captain: roster[0] || null,
        roster,
      });
    }

    const lbPlayer = (r[13] || '').trim();
    if (lbPlayer) {
      leaderboard.push({
        player: lbPlayer,
        total: toNum(r[14]) || 0,
        halo3: toNum(r[15]) || 0,
        haloReach: toNum(r[16]) || 0,
        halo4: toNum(r[17]) || 0,
        halo5: toNum(r[18]) || 0,
        infinite: toNum(r[19]) || 0,
        split: toNum(r[20]) || 0,
      });
    }
  }

  const out = {
    sourceId: 'history-agla-champions',
    sourceUrl: raw.url,
    totalChampionshipsRecorded: champions.length,
    totalPlayersWithRings: leaderboard.length,
    champions,
    leaderboard,
  };

  await fs.writeFile(path.join(COMPILED_DIR, 'agla-champions.json'), JSON.stringify(out, null, 2));
  return out;
}

async function buildAllTimeInfiniteStats() {
  const raw = JSON.parse(await fs.readFile(path.join(JSON_DIR, 'History/history-all-time-infinite-stats-luxe.json'), 'utf8'));
  const allTimeSheet = raw.fullSheets.find(s => s.name === 'All Time');
  const rows = allTimeSheet.rows;
  const headers = rows[0].map(h => (h || '').trim());

  const players = [];
  for (let i = 1; i < rows.length; i++) {
    const r = rows[i] || [];
    const gamertag = (r[0] || '').trim();
    if (!gamertag) continue;
    players.push({
      gamertag,
      kdRatio: toNum(r[1]),
      kdSpread: toNum(r[2]),
      kills: toNum(r[3]),
      deaths: toNum(r[4]),
      winLossPercent: toNum(r[5]),
      wins: toNum(r[6]),
      losses: toNum(r[7]),
      goals: toNum(r[8]),
      betrayals: toNum(r[9]),
      playerGrade: (r[10] || '').trim() || null,
      gradeRank: toNum(r[11]),
      playoffWinLossPercent: toNum(r[12]),
      playoffWins: toNum(r[13]),
      playoffLosses: toNum(r[14]),
      multikills: toNum(r[15]),
      gamesPlayed: toNum(r[16]),
      goalsPerGame: toNum(r[17]),
      killsPerGame: toNum(r[18]),
      sprees: toNum(r[19]),
      exterminations: toNum(r[20]),
      mvps: toNum(r[21]),
      fmvps: toNum(r[22]),
      bestTank: toNum(r[23]),
      bestRunner: toNum(r[24]),
      bestHybrid: toNum(r[25]),
      goalsPerMinute: toNum(r[26]),
      killsPerMinute: toNum(r[27]),
      spreesPerMinute: toNum(r[28]),
      multikillsPerMinute: toNum(r[29]),
      betrayalsPerMinute: toNum(r[30]),
      secondsPlayed: toNum(r[31]),
    });
  }

  const seasonTabs = ['S1', 'S1 Playoffs', 'S2', 'S3', 'S4', 'S4 Playoffs', 'S5', 'S6', 'S7'];
  const seasons = {};
  for (const tabName of seasonTabs) {
    const s = raw.fullSheets.find(sh => sh.name === tabName);
    if (!s || s.rows.length < 2) continue;
    const h = s.rows[0].map((col, idx) => (col || '').trim() || `col_${idx}`);
    const list = [];
    for (let i = 1; i < s.rows.length; i++) {
      const r = s.rows[i];
      if (!r || !r[0] || !String(r[0]).trim()) continue;
      const obj = {};
      for (let c = 0; c < h.length; c++) {
        if (r[c] !== undefined && r[c] !== '') {
          obj[h[c]] = toNum(r[c]);
        }
      }
      list.push(obj);
    }
    seasons[tabName] = list;
  }

  const out = {
    sourceId: 'history-all-time-infinite-stats-luxe',
    sourceUrl: raw.url,
    totalPlayers: players.length,
    headers,
    players,
    seasons,
  };

  await fs.writeFile(path.join(COMPILED_DIR, 'all-time-infinite-stats.json'), JSON.stringify(out, null, 2));
  return out;
}

async function buildLateLeagueMaster() {
  const raw = JSON.parse(await fs.readFile(path.join(JSON_DIR, 'Late-League/late-league-aspence-main.json'), 'utf8'));

  const parseTable = (sheetName) => {
    const s = raw.fullSheets.find(sh => sh.name === sheetName);
    if (!s || s.rows.length < 2) return [];
    const headers = s.rows[0].map((h, idx) => (h || '').trim() || `col_${idx}`);
    const items = [];
    for (let i = 1; i < s.rows.length; i++) {
      const r = s.rows[i];
      if (!r || r.every(c => !String(c || '').trim())) continue;
      const obj = {};
      for (let c = 0; c < headers.length; c++) {
        if (r[c] !== undefined && r[c] !== '') {
          obj[headers[c]] = toNum(r[c]);
        }
      }
      items.push(obj);
    }
    return items;
  };

  const out = {
    sourceId: 'late-league-aspence-main',
    sourceUrl: raw.url,
    totalSheetsInWorkbook: raw.sheets.length,
    playerMasterlist: parseTable('Player Masterlist'),
    playerRankMasterlist: parseTable('Player Rank Masterlist'),
    dataCenterPings: parseTable('Data Center Pings'),
    grifbuxMaster: parseTable('Grifbux Master'),
    season8: {
      halo3: parseTable('S8 Halo 3'),
      haloReach: parseTable('S8 Halo Reach'),
      halo4: parseTable('S8 Halo 4'),
    },
    season7: {
      halo3: parseTable('S7 Halo 3'),
      haloReach: parseTable('S7 Halo Reach'),
      halo4: parseTable('S7 Halo 4'),
    },
    season6: {
      halo3: parseTable('S6 Halo 3'),
      haloReach: parseTable('S6 Halo Reach'),
      halo4: parseTable('S6 Halo 4'),
      halo5: parseTable('S6 Halo 5'),
    },
  };

  await fs.writeFile(path.join(COMPILED_DIR, 'late-league-master.json'), JSON.stringify(out, null, 2));
  return out;
}

async function buildYouTubeVods() {
  const htmlPath = path.join(RAW_DIR, 'youtube-grifball-channel.html');
  let videos = [];
  let channelInfo = {};
  try {
    const html = await fs.readFile(htmlPath, 'utf8');
    const titleMatch = html.match(/<meta property="og:title" content="([^"]+)"/);
    const descMatch = html.match(/<meta property="og:description" content="([^"]+)"/);
    const imgMatch = html.match(/<meta property="og:image" content="([^"]+)"/);
    channelInfo = {
      title: titleMatch ? titleMatch[1] : 'Grifball',
      description: descMatch ? descMatch[1] : '',
      avatar: imgMatch ? imgMatch[1] : '',
      channelId: 'UCTVJapRl-GGwLCfioTb9N8w',
      url: 'https://www.youtube.com/channel/UCTVJapRl-GGwLCfioTb9N8w',
    };

    const videoIdRegex = /"videoId":"([a-zA-Z0-9_-]{11})"/g;
    const seen = new Set();
    let m;
    while ((m = videoIdRegex.exec(html)) !== null) {
      if (!seen.has(m[1])) {
        seen.add(m[1]);
        videos.push({
          videoId: m[1],
          url: `https://www.youtube.com/watch?v=${m[1]}`,
        });
      }
    }
  } catch {
    // ignore
  }
  const out = { channelInfo, videosFoundOnLandingPage: videos.length, videos };
  await fs.writeFile(path.join(COMPILED_DIR, 'youtube-vods.json'), JSON.stringify(out, null, 2));
  return out;
}

async function buildReadme(catalog, champions, infiniteStats, lateLeague) {
  const lines = [];
  lines.push('# Grifball Historical Data Archive (2008–2026)');
  lines.push('');
  lines.push(`- **Generated At:** ${catalog.generatedAt}`);
  lines.push(`- **Total Links Processed:** ${catalog.totalSources}`);
  lines.push(`- **Workbooks & Snapshots Recovered:** ${catalog.recoveredWorkbooks} spreadsheet workbooks + 3 web/Wayback snapshots`);
  lines.push(`- **Total Worksheet Tabs Extracted:** ${catalog.totalSheets.toLocaleString()}`);
  lines.push(`- **Total Non-Empty Data Rows Extracted:** ${catalog.totalRows.toLocaleString()}`);
  lines.push('');
  lines.push('## Directory Structure');
  lines.push('');
  lines.push('- `raw/` — Original `.xlsx` workbooks, `.htmlview.html` snapshots, `.wayback.html` Internet Archive captures, and `.csv` exports (52+ MB).');
  lines.push('- `csv/<Category>/<source-id>/<sheetIndex>_<sheetName>.csv` — Every single worksheet tab from all 51 workbooks exported as clean UTF-8 CSV (792 CSV files).');
  lines.push('- `json/<Category>/<source-id>.json` — Full multi-tab structured JSON representation of each workbook.');
  lines.push('- `compiled/` — High-level normalized datasets:');
  lines.push('  - `catalog.json` — Master index of all 65 sources, 792 sheets, row/column counts, visibility state, and sample headers.');
  lines.push(`  - \`agla-champions.json\` — All ${champions.totalChampionshipsRecorded} championship teams/rosters (2008–2026) and ${champions.totalPlayersWithRings} ring-holders leaderboard.`);
  lines.push(`  - \`all-time-infinite-stats.json\` — All-time Halo Infinite career stats for ${infiniteStats.totalPlayers} players + S1–S7 seasonal & playoff breakdowns.`);
  lines.push(`  - \`late-league-master.json\` — Late League master player registry (${lateLeague.playerMasterlist.length} players with Gamertags, Discord IDs, locations, systems), Data Center Pings (${lateLeague.dataCenterPings.length} players), Grifbux MMR (${lateLeague.grifbuxMaster.length} entries), and S6–S8 leaderboards.`);
  lines.push('  - `youtube-vods.json` — Channel metadata & video IDs extracted from the Grifball YouTube channel.');
  lines.push('');
  lines.push('---');
  lines.push('');
  // Tab / row figures below are read from the catalog so these notes cannot drift from the data.
  const src = (id) => catalog.sources.find(s => s.id === id) || { sheets: [] };
  const tabCount = (id) => (src(id).sheets || []).length;
  const sheetOf = (id, name) => (src(id).sheets || []).find(sh => sh.name === name);
  const rows = (id, name) => (sheetOf(id, name)?.rowCount ?? 0).toLocaleString();
  const csvName = (id, name) => `\`${(sheetOf(id, name)?.csvFile || '').split('/').pop() || name}\``;
  const mb = (id) => `${(src(id).bytes / 1e6).toFixed(2)} MB`;

  lines.push('## Recovery Notes on Dead / Restricted Links');
  lines.push('');
  lines.push('1. **2018 SL18 Pro/Legacy/Ammy Schedules (`DEAD`) & `grifballstats.com/AGLASML18Stats.php`:**');
  lines.push(`   - While the 3 Google Sheets return HTTP \`410 Gone\` and \`grifballstats.com\` is offline, **\`2018-sml18-fantasy-rey\` (\`SML18 Fantasy by Rey\`)** contains **${tabCount('2018-sml18-fantasy-rey')} tabs**, including ${csvName('2018-sml18-fantasy-rey', 'Fixtures & Results')} (\`AGLA SL18 Fixtures & Results\`, ${rows('2018-sml18-fantasy-rey', 'Fixtures & Results')} rows), ${csvName('2018-sml18-fantasy-rey', 'Total Stats')} (${rows('2018-sml18-fantasy-rey', 'Total Stats')} rows), and weekly \`W1\`–\`W4\` Stats / Points / Team Points tabs.`);
  lines.push('   - **Caveat:** the `W3` and `W4` tabs are byte-identical copies of `W2` (and `W1 Points` is identical to `Points System`), so only `W1`–`W2` appear to contain distinct weekly data. There are no `W5`–`W8` tabs.');
  lines.push('2. **2018 FL18 Schedule (`DEAD`):**');
  lines.push('   - Permanently deleted (`410 Gone`), but all team rosters, captains, and bids are preserved in **`2018-fl18-bid-league` (`FL18 Bid League`)**, and the champion (`One Hit Wonders`) is recorded in `agla-champions.json`.');
  lines.push('3. **2019 WL19 Playoff Bracket (`challonge.com/h3wl19`):**');
  lines.push(`   - Challonge blocks automated requests via Cloudflare (\`403\`), but **\`2019-wl19-schedule-stats\` (\`WL19 Schedule & Stats\`)** preserves the playoff stats (${csvName('2019-wl19-schedule-stats', 'Playoff Stats')}, ${rows('2019-wl19-schedule-stats', 'Playoff Stats')} rows), regular season schedule (${csvName('2019-wl19-schedule-stats', 'Schedule')}, ${rows('2019-wl19-schedule-stats', 'Schedule')} rows), and per-game results (${csvName('2019-wl19-schedule-stats', 'Game Results')}, ${rows('2019-wl19-schedule-stats', 'Game Results')} rows). The bracket itself is not recovered.`);
  lines.push('4. **2021 Fall League (`FL21`) Bonus Discovery:**');
  lines.push(`   - Although not listed as a separate link, **\`2021-sml21-pro-stats\`** and **\`2021-sml21-ammy-stats\`** contain \`FL21_Stats\`, \`FL21_PlayoffStats\`, and \`FL21_MatchHistory\` tabs (${rows('2021-sml21-pro-stats', 'FL21_MatchHistory')} and ${rows('2021-sml21-ammy-stats', 'FL21_MatchHistory')} match rows).`);
  lines.push('5. **2022 ASpence Halo 4 Bid Season Draft (`DEAD`):**');
  lines.push(`   - The draft sheet is \`410 Gone\`, but **\`2022-h4-bid-sheet\`** (${tabCount('2022-h4-bid-sheet')} tabs) and **\`2022-h4-bid-stats\`** (${tabCount('2022-h4-bid-stats')} tabs) preserve all 4 bidding rounds (\`Round 1\`–\`Round 4\`), \`Bid Values\`, \`Signed\`, \`AGLA Pro/Ammy Season Raw Data\`, team rosters (\`Teams\`), and match history (\`Match_History\`, ${rows('2022-h4-bid-stats', 'Match_History')} rows).`);
  lines.push('6. **2024 IGL Infektions Halo Infinite FL24 Stats (`1drv.ms`):**');
  lines.push(`   - OneDrive personal link requires Microsoft authentication, but **\`2024-igl-evolved-fl24-stats\`** (${mb('2024-igl-evolved-fl24-stats')} \`.xlsx\`) preserves the IGL Fall 2024 stats: \`IGL Fall Season 2024 Stats Shee\` (${rows('2024-igl-evolved-fl24-stats', 'IGL Fall Season 2024 Stats Shee')} rows), \`IGL Fall Season 2024 Sheet hist\` (${rows('2024-igl-evolved-fl24-stats', 'IGL Fall Season 2024 Sheet hist')} rows), \`PlayerGrades+\`, \`TeamInfo\`, and \`Review HI_MatchH\` (${rows('2024-igl-evolved-fl24-stats', 'Review HI_MatchH')} rows).`);
  lines.push('   - **Note:** the same-named `IGL Fall Season 2024 Stats Shee` tabs in the 2025 Octane/Classic stats workbooks contain only a header row.');
  lines.push('7. **PlayGrifball.com Website (`http://playgrifball.com/`):**');
  lines.push('   - Live domain is parked/404, so our downloader queried the **Internet Archive Wayback Machine CDX API** and saved an archived snapshot (`raw/website-playgrifball.wayback.html`).');
  lines.push('8. **Late League Website (`lateleague.org`):**');
  lines.push('   - `raw/late-league-website.html` is only the empty single-page-app shell (no rendered content); no data was recovered from it.');
  lines.push('');
  lines.push('---');
  lines.push('');
  lines.push('## Complete Source Inventory');
  lines.push('');
  lines.push('| # | Category | ID | Title | Status | Format | Tabs | Non-Empty Rows |');
  lines.push('|---|---|---|---|---|---|---:|---:|');

  catalog.sources.forEach((s, idx) => {
    const rowSum = (s.sheets || []).reduce((acc, sh) => acc + (sh.rowCount || 0), 0);
    lines.push(`| ${idx + 1} | ${s.category} | \`${s.id}\` | ${s.title} | \`${s.status}\` | \`${s.sourceFormat || '-'}\` | ${(s.sheets || []).length} | ${rowSum.toLocaleString()} |`);
  });

  lines.push('');
  lines.push('---');
  lines.push('');
  lines.push('## Detailed Workbook & Worksheet Breakdown');
  lines.push('');

  for (const s of catalog.sources) {
    if (!s.sheets || s.sheets.length === 0) continue;
    const rowSum = s.sheets.reduce((acc, sh) => acc + (sh.rowCount || 0), 0);
    lines.push(`### ${s.title} (\`${s.id}\`)`);
    lines.push(`- **Category:** ${s.category} | **Format:** \`${s.sourceFormat}\` | **Tabs:** ${s.sheets.length} | **Rows:** ${rowSum.toLocaleString()}`);
    lines.push(`- **JSON:** \`json/${s.category}/${s.id}.json\``);
    lines.push('');
      lines.push(`| Tab # | Sheet Name | Visibility | Rows | Cols | CSV File |`);
      lines.push(`|---:|---|---|---:|---:|---|`);
      for (const sh of s.sheets) {
        const vis = sh.hidden ? 'Hidden' : 'Visible';
        const csvCell = sh.csvFile ? `\`${sh.csvFile}\`` : '— (empty sheet, no CSV)';
        lines.push(`| ${sh.index} | ${sh.name.replace(/\|/g, '\\|')} | ${vis} | ${sh.rowCount} | ${sh.colCount} | ${csvCell} |`);
      }
    lines.push('');
  }

  await fs.writeFile(path.join(BASE_DIR, 'README.md'), lines.join('\n'));
}

function parseCsvText(text) {
  const rows = [];
  let row = [];
  let cur = '';
  let inQuotes = false;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (inQuotes) {
      if (ch === '"') {
        if (text[i + 1] === '"') {
          cur += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        cur += ch;
      }
    } else {
      if (ch === '"') {
        inQuotes = true;
      } else if (ch === ',') {
        row.push(cur);
        cur = '';
      } else if (ch === '\n' || ch === '\r') {
        if (ch === '\r' && text[i + 1] === '\n') i++;
        row.push(cur);
        cur = '';
        if (row.some(c => c.trim() !== '')) rows.push(row);
        row = [];
      } else {
        cur += ch;
      }
    }
  }
  if (cur || row.length > 0) {
    row.push(cur);
    if (row.some(c => c.trim() !== '')) rows.push(row);
  }
  return rows;
}

async function recoverSl26MajorSchedule(catalog) {
  const sheetId = '1DBxPmiTg6yGyL7fjy5NmwSL0sr2ei1thnXD-vP32oNk';
  const tabs = [
    { index: 1, name: 'Teams', gid: '1882778066', file: '01_Teams.csv' },
    { index: 2, name: 'League Schedule', gid: '1860257957', file: '02_League_Schedule.csv' },
    { index: 3, name: 'Team Schedules', gid: '485390000', file: '03_Team_Schedules.csv' },
    { index: 4, name: 'Game History', gid: '1497794987', file: '04_Game_History.csv' },
    { index: 5, name: 'Playoffs', gid: '1299347818', file: '05_Playoffs.csv' },
  ];
  const csvSubDir = path.join(BASE_DIR, 'csv/AGLA-2025/2026-sl26-major-schedule');
  await fs.mkdir(csvSubDir, { recursive: true });

  const sheetsMeta = [];
  const fullSheets = [];
  for (const t of tabs) {
    const localCsvPath = path.join(csvSubDir, t.file);
    let csvText = null;
    try {
      csvText = await fs.readFile(localCsvPath, 'utf8');
    } catch {
      const gvizUrl = `https://docs.google.com/spreadsheets/d/${sheetId}/gviz/tq?tqx=out:csv&gid=${t.gid}`;
      const res = await fetch(gvizUrl);
      if (res.ok) {
        csvText = await res.text();
        await fs.writeFile(localCsvPath, csvText, 'utf8');
      }
    }
    if (csvText) {
      const rows = parseCsvText(csvText);
      const colCount = rows.reduce((m, r) => Math.max(m, r.length), 0);
      sheetsMeta.push({
        index: t.index,
        name: t.name,
        hidden: false,
        rowCount: rows.length,
        colCount,
        sampleHeader: (rows[0] || []).slice(0, 12),
        csvFile: `csv/AGLA-2025/2026-sl26-major-schedule/${t.file}`,
      });
      fullSheets.push({
        name: t.name,
        hidden: false,
        rowCount: rows.length,
        colCount,
        rows,
      });
    }
  }

  if (sheetsMeta.length > 0) {
    const srcEntry = catalog.sources.find(s => s.id === '2026-sl26-major-schedule');
    if (srcEntry) {
      srcEntry.status = 'recovered';
      srcEntry.sourceFormat = 'gviz-csv';
      srcEntry.sheets = sheetsMeta;
      await fs.writeFile(
        path.join(JSON_DIR, 'AGLA-2025/2026-sl26-major-schedule.json'),
        JSON.stringify({ ...srcEntry, fullSheets }, null, 2)
      );
    }
    catalog.recoveredWorkbooks = catalog.sources.filter(s => s.status.startsWith('recovered') && s.sheets && s.sheets.length > 0).length;
    catalog.totalSheets = catalog.sources.reduce((sum, s) => sum + (s.sheets ? s.sheets.length : 0), 0);
    catalog.totalRows = catalog.sources.reduce((sum, s) => sum + (s.sheets ? s.sheets.reduce((a, sh) => a + (sh.rowCount || 0), 0) : 0), 0);
    await fs.writeFile(path.join(COMPILED_DIR, 'catalog.json'), JSON.stringify(catalog, null, 2));
  }
}

async function main() {
  const catalog = JSON.parse(await fs.readFile(path.join(COMPILED_DIR, 'catalog.json'), 'utf8'));
  await recoverSl26MajorSchedule(catalog);
  const champions = await buildAglaChampions();
  const infiniteStats = await buildAllTimeInfiniteStats();
  const lateLeague = await buildLateLeagueMaster();
  const vods = await buildYouTubeVods();
  await buildReadme(catalog, champions, infiniteStats, lateLeague);

  console.log(`Compiled summaries complete:`);
  console.log(`- AGLA Champions: ${champions.totalChampionshipsRecorded} seasons, ${champions.totalPlayersWithRings} ring winners`);
  console.log(`- All-Time Infinite Stats: ${infiniteStats.totalPlayers} players across ${Object.keys(infiniteStats.seasons).length} season tabs`);
  console.log(`- Late League Master: ${lateLeague.playerMasterlist.length} masterlist players, ${lateLeague.grifbuxMaster.length} Grifbux rows, ${lateLeague.dataCenterPings.length} ping profiles`);
  console.log(`- YouTube VODs: ${vods.videosFoundOnLandingPage} landing page videos`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
