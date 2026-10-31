import fs from 'node:fs/promises';
import fssync from 'node:fs';
import path from 'node:path';

const BASE_DIR = path.resolve('data/historical-archive');
const JSON_DIR = path.join(BASE_DIR, 'json');
const COMPILED_DIR = path.join(BASE_DIR, 'compiled');

const SEASONS_CONFIG = JSON.parse(fssync.readFileSync('archive-tools/seasons-config.json', 'utf8'));

// Canonical Core Column Mapping
const CORE_MAPPINGS = {
  // Gamertag / Player
  'player': 'gamertag',
  'gamertag': 'gamertag',
  'players': 'gamertag',
  'name': 'gamertag',

  // Team
  'team': 'teamName',
  'team name': 'teamName',

  // Games & W/L
  'games': 'gamesPlayed',
  'games played': 'gamesPlayed',
  'gamesplayed': 'gamesPlayed',
  'played': 'gamesPlayed',
  'gp': 'gamesPlayed',
  'wins': 'wins',
  'win': 'wins',
  'losses': 'losses',
  'loses': 'losses',
  'loss': 'losses',
  'winpercent': 'winPercent',
  'win %': 'winPercent',
  'win percent': 'winPercent',
  'win/loss': 'winPercent',
  'win percentage': 'winPercent',

  // Combat Core
  'goals': 'goals',
  'kills': 'kills',
  'assists': 'assists',
  'deaths': 'deaths',
  'k/d': 'kdRatio',
  'kd': 'kdRatio',
  'kd ratio': 'kdRatio',
  'k/d ratio': 'kdRatio',
  'kda': 'kdRatio',
  'spread': 'kdSpread',
  'k/d spread': 'kdSpread',
  'kd spread': 'kdSpread',
  'k-d': 'kdSpread',
  'carrier kills': 'carrierKills',
  'carrier kill': 'carrierKills',
  'carrier killers': 'carrierKills',
  'stops': 'carrierKills',
  'bomb stops': 'carrierKills',
  'bombstops': 'carrierKills',
  'plate stops': 'carrierKills',

  // Durations & Life
  'avg life': 'averageLifeSeconds',
  'averagelife': 'averageLifeSeconds',
  'average life': 'averageLifeSeconds',
  'avg life duration': 'averageLifeSeconds',
  'average life duration': 'averageLifeSeconds',
  'time': 'totalSecondsPlayed',
  'minutes': 'totalSecondsPlayed',
  'minutes played': 'totalSecondsPlayed',
  'seconds played': 'totalSecondsPlayed',
  'game time': 'totalSecondsPlayed',
  'gametime': 'totalSecondsPlayed',
  'timealive': 'totalSecondsPlayed',

  // Universal Medals & Accolades
  'sprees': 'sprees',
  'killing spree': 'sprees',
  'killing sprees': 'sprees',
  'killingsprees': 'sprees',
  'multikills': 'multiKills',
  'multikill': 'multiKills',
  'exterminations': 'exterminations',
  'extermination': 'exterminations',
  'exterm': 'exterminations',
  'exterms': 'exterminations',
  'betrayals': 'betrayals',

  // Weapons & Hits
  'hammer kills': 'hammerKills',
  'hammerkills': 'hammerKills',
  'sword kills': 'swordKills',
  'swordkills': 'swordKills',
  'ball kills': 'ballKills',
  'ballkills': 'ballKills',
  'punches': 'punches',
  'ball punches': 'punches',

  // Infinite specifics
  'score': 'score',
  'damage done': 'damageDealt',
  'damage taken': 'damageTaken'
};

function cleanGamertag(rawTag) {
  if (!rawTag) return { gamertag: '', tradeNote: null, roleNote: null };
  let str = String(rawTag).trim();
  
  // Extract trade note: (to TEAM) or (from TEAM)
  let tradeNote = null;
  const tradeMatch = /\((?:to|from)\s+[^)]+\)/i.exec(str);
  if (tradeMatch) {
    tradeNote = tradeMatch[0].replace(/^\(|\)$/g, '').trim();
    str = str.replace(tradeMatch[0], '').trim();
  }

  // Extract role note: (c), (a), (sub), etc.
  let roleNote = null;
  const roleMatch = /\(([casub]+)\)/i.exec(str);
  if (roleMatch) {
    roleNote = roleMatch[1].toLowerCase();
    str = str.replace(roleMatch[0], '').trim();
  }

  // Clean trailing spaces and commas
  str = str.replace(/[\s,]+$/, '').trim();

  return { gamertag: str, tradeNote, roleNote };
}

function parseDurationSeconds(val, rawHeader) {
  if (val === null || val === undefined || val === '') return null;
  const s = String(val).trim();
  if (!s) return null;

  // If header says minutes (e.g. Minutes Played = 154)
  if (/minute/i.test(rawHeader) && !s.includes(':')) {
    const n = parseFloat(s);
    return Number.isFinite(n) ? Math.round(n * 60) : null;
  }

  // HH:MM:SS or MM:SS
  if (s.includes(':')) {
    const parts = s.split(':').map(p => parseFloat(p) || 0);
    if (parts.length === 3) return Math.round(parts[0] * 3600 + parts[1] * 60 + parts[2]);
    if (parts.length === 2) return Math.round(parts[0] * 60 + parts[1]);
  }

  // Raw numeric seconds or fraction
  const n = parseFloat(s);
  return Number.isFinite(n) ? Math.round(n) : null;
}

function parseAverageLifeSeconds(val) {
  if (val === null || val === undefined || val === '') return null;
  const s = String(val).trim();
  if (s.startsWith(':')) {
    const sec = parseFloat(s.slice(1));
    return Number.isFinite(sec) ? sec : null;
  }
  const n = parseFloat(s);
  return Number.isFinite(n) ? n : null;
}

function toNum(val) {
  if (val === null || val === undefined || val === '') return null;
  const s = String(val).replace(/,/g, '').trim();
  if (s === '' || s === '-' || s === '#N/A' || s === '#VALUE!' || s === '#DIV/0!' || s === '#REF!') return null;
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
}

async function main() {
  const unifiedOutput = {
    generatedAt: new Date().toISOString(),
    totalSeasons: 0,
    totalPlayerStints: 0,
    uniqueGamertagsCount: 0,
    seasons: []
  };

  const allGamertags = new Set();
  const inconsistencies = [];

  for (const cfg of SEASONS_CONFIG) {
    let actualCat = null;
    const catList = await fs.readdir(JSON_DIR);
    for (const c of catList) {
      if (fssync.existsSync(path.join(JSON_DIR, c, cfg.workbook + '.json'))) {
        actualCat = c;
        break;
      }
    }
    if (!actualCat) {
      inconsistencies.push({ type: 'missing_workbook', workbook: cfg.workbook });
      continue;
    }

    const wb = JSON.parse(await fs.readFile(path.join(JSON_DIR, actualCat, cfg.workbook + '.json'), 'utf8'));

    for (const t of cfg.tabs) {
      const sheet = wb.fullSheets.find(x => x.name === t.name);
      if (!sheet || !sheet.rows || sheet.rows.length <= t.headerRow + 1) {
        inconsistencies.push({ type: 'missing_sheet_or_empty', workbook: cfg.workbook, sheet: t.name });
        continue;
      }

      const seasonName = t.customSeason || cfg.seasonName;
      const gameVersion = t.game || cfg.game;
      const division = cfg.division || 'Open';
      const league = cfg.league || 'AGLA';

      const rawHeaders = sheet.rows[t.headerRow].map(c => String(c || '').trim());
      const colMap = {};
      for (let c = 0; c < rawHeaders.length; c++) {
        const hLow = rawHeaders[c].toLowerCase();
        if (CORE_MAPPINGS[hLow]) {
          colMap[c] = { key: CORE_MAPPINGS[hLow], original: rawHeaders[c] };
        }
      }

      const playerRows = [];
      const seenInSeason = new Map();

      for (let r = t.headerRow + 1; r < sheet.rows.length; r++) {
        const row = sheet.rows[r];
        if (!row || row.every(cell => !String(cell || '').trim())) continue;

        // Find gamertag
        let rawGamertag = null;
        let teamName = null;
        for (let c = 0; c < row.length; c++) {
          if (colMap[c]?.key === 'gamertag') rawGamertag = row[c];
          if (colMap[c]?.key === 'teamName') teamName = row[c];
        }

        // Special case: 2020-sml20-halo3 row[0]=Team, row[1]=Player (unlabeled)
        if (!rawGamertag && cfg.workbook === '2020-sml20-halo3') {
          teamName = row[0];
          rawGamertag = row[1];
        }

        if (!rawGamertag || !String(rawGamertag).trim() || /total|average|subtotal/i.test(String(rawGamertag))) continue;

        const { gamertag, tradeNote, roleNote } = cleanGamertag(rawGamertag);
        if (!gamertag) continue;

        allGamertags.add(gamertag.toLowerCase());

        // Stint handling
        const prevCount = seenInSeason.get(gamertag.toLowerCase()) || 0;
        seenInSeason.set(gamertag.toLowerCase(), prevCount + 1);
        const stintIndex = prevCount + 1;

        const record = {
          gamertag,
          rawGamertag: String(rawGamertag).trim(),
          teamName: teamName ? String(teamName).trim() : null,
          tradeNote,
          roleNote,
          stintIndex,
          isPlayoffs: Boolean(t.isPlayoffs),
          isCombined: Boolean(t.isCombined),
          gamesPlayed: 0,
          wins: 0,
          losses: 0,
          winPercent: null,
          goals: 0,
          kills: 0,
          deaths: 0,
          assists: 0,
          kdRatio: null,
          kdSpread: null,
          carrierKills: 0,
          averageLifeSeconds: null,
          totalSecondsPlayed: null,
          sprees: 0,
          multiKills: 0,
          exterminations: 0,
          betrayals: 0,
          hammerKills: null,
          swordKills: null,
          ballKills: null,
          punches: null,
          score: null,
          damageDealt: null,
          damageTaken: null,
          extendedMetrics: {}
        };

        for (let c = 0; c < row.length; c++) {
          const mapping = colMap[c];
          const val = row[c];
          if (!mapping || val === undefined || val === '') continue;

          if (mapping.key === 'averageLifeSeconds') {
            record.averageLifeSeconds = parseAverageLifeSeconds(val);
          } else if (mapping.key === 'totalSecondsPlayed') {
            record.totalSecondsPlayed = parseDurationSeconds(val, mapping.original);
          } else if (mapping.key !== 'gamertag' && mapping.key !== 'teamName') {
            const num = toNum(val);
            if (num !== null) {
              if (record[mapping.key] !== undefined) record[mapping.key] = num;
              else record.extendedMetrics[mapping.original] = num;
            }
          }
        }

        // Calculate K/D and Spread if missing or null
        if (record.kills !== null && record.deaths !== null) {
          if (record.kdSpread === null) record.kdSpread = record.kills - record.deaths;
          if (record.kdRatio === null && record.deaths > 0) {
            record.kdRatio = Number((record.kills / record.deaths).toFixed(3));
          }
        }

        playerRows.push(record);
      }

      unifiedOutput.seasons.push({
        seasonName,
        gameVersion,
        league,
        division,
        sourceWorkbook: cfg.workbook,
        sourceSheet: t.name,
        isPlayoffs: Boolean(t.isPlayoffs),
        isCombined: Boolean(t.isCombined),
        playerCount: playerRows.length,
        players: playerRows
      });

      unifiedOutput.totalPlayerStints += playerRows.length;
    }
  }

  unifiedOutput.totalSeasons = unifiedOutput.seasons.length;
  unifiedOutput.uniqueGamertagsCount = allGamertags.size;

  await fs.writeFile(
    path.join(COMPILED_DIR, 'unified-historical-seasons.json'),
    JSON.stringify(unifiedOutput, null, 2),
    'utf8'
  );

  console.log('=== Ingestion Summary ===');
  console.log('Total Seasons Compiled:', unifiedOutput.totalSeasons);
  console.log('Total Player Stints Compiled:', unifiedOutput.totalPlayerStints);
  console.log('Unique Player Gamertags Found:', unifiedOutput.uniqueGamertagsCount);
  console.log('Inconsistencies Encountered:', inconsistencies.length);
  if (inconsistencies.length > 0) {
    console.log(JSON.stringify(inconsistencies, null, 2));
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
