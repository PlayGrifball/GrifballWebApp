#!/usr/bin/env node
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT = path.resolve(__dirname, '..');
const COMPILED_DIR = path.join(ROOT, 'data/historical-archive/compiled');

function parseArgs() {
  const args = process.argv.slice(2);
  const options = {
    baseUrl: process.env.API_BASE_URL || 'https://localhost:7210',
    username: process.env.SYSADMIN_USER || 'smoke-test',
    password: process.env.SYSADMIN_PASSWORD || 'Pass@word1!',
    token: process.env.AUTH_TOKEN || null,
    insecure: true,
  };

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === '--url' || arg === '-u') options.baseUrl = args[++i];
    else if (arg === '--user') options.username = args[++i];
    else if (arg === '--password' || arg === '-p') options.password = args[++i];
    else if (arg === '--token' || arg === '-t') options.token = args[++i];
    else if (arg === '--no-insecure') options.insecure = false;
  }
  return options;
}

async function main() {
  const options = parseArgs();
  if (options.insecure) {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
  }

  console.log(`\n======================================================`);
  console.log(` Grifball Historical Archive Ingestion Runner`);
  console.log(` Target Server: ${options.baseUrl}`);
  console.log(`======================================================\n`);

  const seasonsPath = path.join(COMPILED_DIR, 'unified-historical-seasons.json');
  const championsPath = path.join(COMPILED_DIR, 'agla-champions.json');

  console.log(`[1/3] Reading compiled archive files...`);
  const seasonsBuf = await fs.readFile(seasonsPath);
  const championsBuf = await fs.readFile(championsPath);
  console.log(`  - Seasons archive: ${(seasonsBuf.length / (1024 * 1024)).toFixed(2)} MB`);
  console.log(`  - Champions archive: ${(championsBuf.length / 1024).toFixed(2)} KB`);

  let token = options.token;
  if (!token) {
    console.log(`\n[2/3] Authenticating as '${options.username}'...`);
    try {
      const loginRes = await fetch(`${options.baseUrl}/Identity/Login?useCookies=false`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userName: options.username,
          password: options.password,
        }),
      });

      if (!loginRes.ok) {
        const errText = await loginRes.text();
        console.warn(`  Warning: Direct bearer login returned HTTP ${loginRes.status}: ${errText}`);
        console.log(`  Attempting cookie-based login...`);
      } else {
        const loginData = await loginRes.json();
        token = loginData.accessToken;
        console.log(`  Authenticated successfully. Bearer token acquired.`);
      }
    } catch (err) {
      console.warn(`  Warning: Could not connect or authenticate to server: ${err.message}`);
      console.log(`  Ensure the backend is running at ${options.baseUrl}.`);
      console.log(`  (You can start it with .agents/skills/start-env/scripts/start.ps1)`);
      return;
    }
  }

  console.log(`\n[3/3] Uploading and importing historical archive...`);
  const form = new FormData();
  form.append('seasons', new Blob([seasonsBuf], { type: 'application/json' }), 'unified-historical-seasons.json');
  form.append('championships', new Blob([championsBuf], { type: 'application/json' }), 'agla-champions.json');
  // Each workbook's link, stored on its seasons (HistoricalSourceUrl)
  form.append('catalog', new Blob([await fs.readFile(path.join(COMPILED_DIR, 'catalog.json'))], { type: 'application/json' }), 'catalog.json');
  // Halo Infinite seasons' match IDs (archive-tools/extract-waypoint-matches.mjs): the import pulls those matches and derives
  // the seasons' stats from them
  form.append('waypointMatches', new Blob([await fs.readFile(path.join(COMPILED_DIR, 'waypoint-matches.json'))], { type: 'application/json' }), 'waypoint-matches.json');

  const headers = {};
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  try {
    const res = await fetch(`${options.baseUrl}/HistoricalImport/Import`, {
      method: 'POST',
      headers,
      body: form,
    });

    if (!res.ok) {
      const body = await res.text();
      throw new Error(`Server returned HTTP ${res.status}: ${body}`);
    }

    // The import runs in the background: 202 with the job, whose progress and report the job endpoint gives
    const { jobID } = await res.json();
    console.log(`  Queued as job ${jobID}`);
    let job;
    let lastStage = "";
    for (;;) {
      await new Promise(resolve => setTimeout(resolve, 5000));
      const status = await fetch(`${options.baseUrl}/HistoricalImport/Job/${jobID}`, { headers });
      if (!status.ok)
        throw new Error(`Job status returned HTTP ${status.status}: ${await status.text()}`);
      job = await status.json();
      const stage = `${job.status}: ${job.stage ?? ""} (${job.seasonsDone}/${job.seasonsTotal} seasons)`;
      if (stage !== lastStage)
        console.log(`  ${stage}`);
      lastStage = stage;
      if (job.status === "Succeeded" || job.status === "Failed")
        break;
    }
    if (job.status === "Failed")
      throw new Error(`Job ${jobID} failed: ${job.error}`);
    const report = job.report;
    console.log(`\n=== Import Report ===`);
    console.log(`  Seasons Created:         ${report.seasonsCreated}`);
    console.log(`  Seasons Updated:         ${report.seasonsUpdated}`);
    console.log(`  Player Stats Inserted:   ${report.statsInserted}`);
    console.log(`  Player Stats Updated:    ${report.statsUpdated}`);
    console.log(`  Championships Inserted:  ${report.championshipsInserted}`);
    console.log(`  Championships Updated:   ${report.championshipsUpdated}`);
    console.log(`  Dummy Users Created:     ${report.dummyUsersCreated}`);
    console.log(`  Manual Replaced By API:  ${report.manualReplacedByWaypoint}`);
    console.log(`  Waypoint Matches Pulled: ${report.waypointMatchesPulled}`);
    console.log(`  Roster Suggestions:      ${report.suggestionsCreated} (approve or reject at /HistoricalImport/Claims)`);
    for (const [label, list] of [["Missing Waypoint matches", report.missingMatches], ["Warnings", report.warnings]]) {
      if (list?.length) {
        console.log(`\n  ${label} (${list.length}):`);
        list.slice(0, 10).forEach(s => console.log(`    - ${s}`));
        if (list.length > 10) console.log(`    ... and ${list.length - 10} more`);
      }
    }
    if (report.skipped?.length) {
      console.log(`\n  Skipped Items (${report.skipped.length}):`);
      report.skipped.slice(0, 10).forEach(s => console.log(`    - ${s}`));
      if (report.skipped.length > 10) console.log(`    ... and ${report.skipped.length - 10} more`);
    }
    if (report.errors?.length) {
      console.error(`\n  Errors (${report.errors.length}):`);
      report.errors.forEach(e => console.error(`    ! ${e}`));
    }
    console.log(`\nHistorical ingestion completed successfully.\n`);
  } catch (err) {
    console.error(`\nImport failed: ${err.message}`);
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
