// Headless walk of the main routes with the API mocked by page.route.
// usage: node walk.js <label>=<distDir> [<label>=<distDir> ...]
// Writes results-<label>.json and screenshots into ./shots/<label>/.
const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright-core');

const DELAY = 400; // ms before each mocked API response, so data lands after first render
const SETTLE = 2500; // ms to wait after navigation before checking the DOM

function serve(dir, port) {
  return new Promise((resolve) => {
    const srv = http.createServer((req, res) => {
      let p = decodeURIComponent(req.url.split('?')[0]);
      let f = path.join(dir, p);
      if (!f.startsWith(dir) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) f = path.join(dir, 'index.html');
      const ext = path.extname(f);
      const types = { '.js': 'text/javascript', '.css': 'text/css', '.html': 'text/html', '.json': 'application/json', '.webp': 'image/webp', '.ico': 'image/x-icon', '.svg': 'image/svg+xml' };
      res.writeHead(200, { 'content-type': types[ext] || 'application/octet-stream' });
      fs.createReadStream(f).pipe(res);
    });
    srv.listen(port, () => resolve(srv));
  });
}

const user = (id, name) => ({ userID: id, userName: name, lockoutEnd: null, lockoutEnabled: false, isDummyUser: false, accessFailedCount: 0, region: 'NA', displayName: name + '-DN', gamertag: name + '-GT', discord: null, externalAuthCount: 1, hasPassword: true, roles: [{ roleName: 'Player', hasRole: true }] });
const letters = { xboxUserID: 1, gamertag: 'GRADE-GAMERTAG', goals: 'A', kdSpread: 'B', punches: 'C', sprees: 'D', doubleKills: 'E', tripleKills: 'F', multiKills: 'S', xFactor: 'A+', kills: 'B-', gradeAvgMath: 3.3, gradeAvg: 'B' };
const pm = { xboxUserID: 1, gamertag: 'PM-GAMERTAG', goalsPM: 1, kdSpreadPM: 1, punchesPM: 1, spreesPM: 1, doubleKillsPM: 1, tripleKillsPM: 1, multiKillsPM: 1, xFactorPM: 1, killsPM: 1 };
const timeslots = [
  { id: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: true, isDisabled: false, isHeader: false },
  { id: 2, dayOfWeek: 'Tuesday', time: '19:30:00', isChecked: false, isDisabled: false, isHeader: false },
];

// [regex on pathname (case-insensitive), body, isText]
const MOCKS = [
  [/^\/api\/identity\/metainfo$/, { isSysAdmin: true, isCommissioner: true, isPlayer: true, userID: 1, displayName: 'MOCK-USER' }],
  [/^\/api\/commithash$/, 'abc1234', true],
  [/^\/api\/commitdate$/, '2026-10-01T12:00:00Z', true],
  [/^\/api\/season\/getcurrentseasonid\/?$/, 1],
  [/^\/api\/season\/getseasonname\/1$/, 'SEASON-NAME-X', true],
  [/^\/api\/home\/currentandfutureevents\/?$/, [{ seasonID: 1, name: 'EVT-CURRENT', start: '2026-10-01', end: '2026-12-01', eventType: 'Season' }]],
  [/^\/api\/home\/pastseasons$/, { results: [{ seasonID: 0, name: 'EVT-PAST', start: '2025-01-01', end: '2025-03-01', eventType: 'Season' }], totalCount: 1, pageNumber: 1, pageSize: 10 }],
  [/^\/api\/matchplanner\/getunscheduledmatches\/1$/, [{ seasonMatchID: 11, homeCaptain: 'CAPT-UNSCHED-H', awayCaptain: 'CAPT-UNSCHED-A', complete: false, time: null }]],
  [/^\/api\/matchplanner\/getscheduledmatches\/1$/, [{ seasonMatchID: 12, homeCaptain: 'CAPT-SCHED-H', awayCaptain: 'CAPT-SCHED-A', complete: false, time: '2026-10-10T20:00:00Z' }]],
  [/^\/api\/teamstandings\/getteamstandings\/1$/, [{ teamID: 5, teamName: 'TEAM-STANDING-X', wins: 3, losses: 1 }]],
  [/^\/api\/brackets\/getviewerdata$/, { participants: [], stages: [], matches: [], matchGames: [] }],
  [/^\/api\/signups\/getsignups\/1$/, [{ seasonID: 1, userID: 2, personName: 'SIGNUP-PERSON', timeStamp: '2026-09-01T00:00:00Z', teamName: 'SIGNUP-TEAMNAME', willCaptain: true, requiresAssistanceDrafting: false, timeslots: [] }]],
  [/^\/api\/signups\/getsignup\/1$/, { seasonID: 1, userID: 1, personName: 'Me', teamName: 'SIGNUPFORM-TEAM', willCaptain: true, requiresAssistanceDrafting: false, timeslots }],
  [/^\/api\/signups\/gettimeslots\/1$/, timeslots],
  [/^\/api\/teams\/getteams\/1$/, [{ teamID: 1, teamName: 'TB-TEAM-ALPHA', captain: { personID: 10, name: 'CAPT-ALPHA', order: 1 }, players: [{ personID: 20, name: 'TB-PLAYER-1', round: 1, pick: 1 }] }]],
  [/^\/api\/teams\/getplayerpool\/1$/, [{ personID: 30, name: 'POOL-PLAYER-A', round: null, pick: null }, { personID: 31, name: 'POOL-PLAYER-B', round: null, pick: null }]],
  [/^\/api\/teams\/arecaptainslocked\/1$/, true],
  [/^\/api\/team\/team\/5$/, { teamName: 'TEAMDTO-NAME', wins: 3, losses: 1, tbd: 2, players: [{ teamPlayerID: 1, userID: 2, gamertag: 'TEAM-PLAYER-GT', draftCaptainOrder: 1, draftRound: null }] }],
  [/^\/api\/team\/matches\/5$/, [{ seasonMatchID: 7, scheduledTime: '2026-10-10T20:00:00Z', bestOf: 3, score: 2, result: 'Won', otherTeamID: 6, otherTeamName: 'OTHER-TEAM-NAME', otherScore: 1, otherResult: 'Loss' }]],
  [/^\/api\/grades\/getgrades\/1$/, { totals: [], perMinutes: [pm], letters: [letters] }],
  [/^\/api\/seasonmatch\/getseasonmatchpage\/7$/, { seasonID: 1, seasonName: 'SM-SEASON', isPlayoff: false, homeTeamName: 'SM-HOME-TEAM', homeTeamID: 5, homeTeamScore: 2, homeTeamResult: 'Won', awayTeamName: 'SM-AWAY-TEAM', awayTeamID: 6, awayTeamScore: 1, awayTeamResult: 'Loss', scheduledTime: '2026-10-10T20:00:00Z', bestOf: 3, reportedGames: [], bracketInfo: null, activeRescheduleRequestId: null }],
  [/^\/api\/seasonmatch\/getpossiblematches\/7$/, []],
  [/^\/api\/stats\/topkills$/, [{ rank: 1, gamertag: 'TOPKILLS-GT', kills: 99 }]],
  [/^\/api\/eventorganizer\/getseasons$/, [{ seasonID: 1, seasonName: 'SEASON-MGR-NAME', signupsOpen: '2026-01-01', signupsClose: '2026-01-02', draftStart: '2026-01-03', seasonStart: '2026-01-04', seasonEnd: '2026-02-01', signupsCount: 4 }]],
  [/^\/api\/eventorganizer\/getseason\/1$/, { seasonID: 1, seasonName: 'EDIT-SEASON-NAME', signupsOpen: '2026-01-01T00:00:00Z', signupsClose: '2026-01-02T00:00:00Z', draftStart: '2026-01-03T00:00:00Z', seasonStart: '2026-01-04T00:00:00Z', seasonEnd: '2026-02-01T00:00:00Z', signupsCount: 4, copyFrom: null, copyAvailability: false, copySignups: false, copyTeams: false }],
  [/^\/api\/availability\/getseasonavailability$/, [{ dayOfWeek: 'Wednesday', time: '2026-10-08T19:00:00.000-04:00' }, { dayOfWeek: 'Thursday', time: '2026-10-08T19:30:00.000-04:00' }, { dayOfWeek: 'Friday', time: '2026-10-08T20:00:00.000-04:00' }]],
  [/^\/api\/admin\/checkstatus$/, 'STATUS-OK-FROM-API', true],
  [/^\/api\/usermanagement\/getusers$/, { results: [user(1, 'USER-ONE'), user(2, 'USER-TWO')], totalCount: 2, pageNumber: 1, pageSize: 10 }],
  [/^\/api\/usermanagement\/getuser\/3$/, user(3, 'EDIT-USER-NAME')],
  [/^\/api\/usermanagement\/getuser\/4$/, user(4, 'MERGE-TO-USER')],
  [/^\/api\/usermanagement\/getuser\/0$/, user(0, 'ZERO')],
  [/^\/api\/profile\/getgamertag\/2$/, { gamertag: 'PROFILE-GT' }],
  [/^\/api\/excel\/defaultsheetinfo$/, [{ name: 'SHEET-NAME-X', spreadsheetID: 'sid', sheetName: 'sname' }]],
  [/^\/api\/commissionerdashboard\/getdashboarddata$/, { pendingReschedules: [{ matchRescheduleID: 1, seasonMatchID: 7, homeCaptain: 'DASH-CAPT-H', awayCaptain: 'DASH-CAPT-A', reason: 'r', requestedByGamertag: 'g', requestedAt: '2026-10-01T00:00:00Z', status: 0 }], overdueMatches: [], summary: { pendingRescheduleCount: 1, overdueMatchCount: 0, criticalOverdueCount: 0 } }],
  [/^\/api\/identity\/login$/, { tokenType: 'Bearer', accessToken: 'tok2', expiresIn: 3600, refreshToken: 'r2' }],
];

// Each scenario: path; texts that must appear; inputs whose value must equal; optional interact().
const SCENARIOS = [
  { name: 'home', path: '/', texts: ['EVT-CURRENT', 'EVT-PAST', 'abc1234', 'Hello MOCK-USER'] },
  { name: 'season', path: '/season/1', texts: ['SEASON-NAME-X', 'CAPT-UNSCHED-H', 'CAPT-SCHED-H', 'TEAM-STANDING-X'] },
  { name: 'signups', path: '/season/1/signups', texts: ['SIGNUP-PERSON'] },
  { name: 'signupForm', path: '/season/1/signupForm', inputs: { 'input[name="teamName"]': 'SIGNUPFORM-TEAM' }, texts: ['Monday'] },
  { name: 'teamBuilder', path: '/season/1/teams', texts: ['TB-TEAM-ALPHA', 'POOL-PLAYER-A', 'Unlock Captains'], signalr: true },
  { name: 'team', path: '/season/1/team/5', texts: ['TEAMDTO-NAME', 'TEAM-PLAYER-GT', 'OTHER-TEAM-NAME'] },
  { name: 'playerGrades', path: '/season/1/playergrades', texts: ['GRADE-GAMERTAG'] },
  { name: 'seasonMatch', path: '/seasonmatch/7', texts: ['SM-HOME-TEAM', 'SM-AWAY-TEAM'] },
  { name: 'topStats', path: '/topstats', texts: ['TOPKILLS-GT'] },
  { name: 'seasonManager', path: '/seasonManager', texts: ['SEASON-MGR-NAME'] },
  { name: 'seasonEdit', path: '/seasonEdit/1', inputs: { 'input[name="seasonName"]': 'EDIT-SEASON-NAME' } },
  { name: 'seasonAvailability', path: '/seasonAvailability/1', count: ['mat-select', 3] },
  { name: 'infiniteClient', path: '/infiniteclient', texts: ['STATUS-OK-FROM-API'] },
  { name: 'userManagement', path: '/usermanagement', texts: ['USER-ONE', 'USER-TWO'] },
  { name: 'editUser', path: '/usermanagement/edituser/3', inputs: { 'input[name="userName"]': 'EDIT-USER-NAME' } },
  { name: 'mergeUser', path: '/usermanagement/mergeuser/3/4', texts: ['EDIT-USER-NAME', 'MERGE-TO-USER'] },
  { name: 'profile', path: '/profile/2', texts: ['Gamertag: PROFILE-GT'] },
  { name: 'excel', path: '/excel', texts: ['SHEET-NAME-X'] },
  { name: 'commissionerDashboard', path: '/commissioner-dashboard', texts: ['DASH-CAPT-H'] },
  { name: 'register-validation', path: '/register', interact: async (page) => {
      await page.fill('input[name="password"]', 'short');
      await page.locator('input[name="password"]').blur();
      await page.waitForTimeout(500);
    }, texts: ['Minimum 12 characters'] },
  { name: 'login-flow', path: '/login', loggedOut: true, interact: async (page) => {
      await page.fill('input[name="username"]', 'u');
      await page.fill('input[name="password"]', 'p');
      await page.locator('form button[mat-flat-button], form button[mat-raised-button]').first().click();
      await page.waitForTimeout(1500);
    }, texts: ['Hello MOCK-USER'] },
];

async function signalrScenario(page, log) {
  // Run after the page has loaded data: push hub messages and check the view.
  const results = {};
  const sock = page.__hub;
  if (!sock) { results.error = 'no websocket connected'; return results; }
  // Nudge first so the initial HTTP data is on screen even in the zoneless build; this isolates the SignalR path.
  await page.locator('mat-toolbar button[aria-label="menu icon"]').click().catch(() => {});
  await page.waitForTimeout(600); await page.keyboard.press('Escape'); await page.waitForTimeout(600);
  results.dataVisibleBeforePush = (await page.locator('body').innerText()).includes('TB-TEAM-ALPHA');
  const send = (target, args) => sock.send(JSON.stringify({ type: 1, target, arguments: args }) + '\x1e');
  send('UnlockCaptains', [1]);
  await page.waitForTimeout(1500);
  results.unlockShowsLockButton = (await page.locator('body').innerText()).includes('Lock Captains') && !(await page.locator('body').innerText()).includes('Unlock Captains');
  send('AddCaptain', [{ seasonID: 1, personID: 31, teamName: 'SIGNALR-NEW-TEAM', captainName: 'POOL-PLAYER-B', orderNumber: 2 }]);
  await page.waitForTimeout(1500);
  const body = await page.locator('body').innerText();
  results.addCaptainShowsTeam = body.includes('SIGNALR-NEW-TEAM');
  const pool = await page.locator('.player-pool-item').allInnerTexts();
  results.addCaptainRemovedFromPool = !pool.some((t) => t.includes('POOL-PLAYER-B'));
  return results;
}

async function runBuild(browser, label, dir, port) {
  const srv = await serve(path.resolve(dir), port);
  const out = [];
  fs.mkdirSync(`shots/${label}`, { recursive: true });
  for (const sc of SCENARIOS) {
    const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const page = await ctx.newPage();
    const consoleErrors = [];
    page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 300)); });
    page.on('pageerror', (e) => consoleErrors.push('pageerror: ' + String(e.message).slice(0, 300)));
    if (!sc.loggedOut) {
      await ctx.addInitScript(() => {
        localStorage.setItem('access_token', JSON.stringify({ tokenType: 'Bearer', accessToken: 'tok', expiresIn: 3600, refreshToken: 'r' }));
        localStorage.setItem('metaInfo', JSON.stringify({ isSysAdmin: true, isCommissioner: true, isPlayer: true, userID: 1, displayName: 'MOCK-USER' }));
      });
    }
    // brackets-viewer is loaded from a CDN; stub it.
    await page.route('https://cdn.jsdelivr.net/**', (r) => r.fulfill({ contentType: 'text/javascript', body: 'window.bracketsViewer={addLocale(){},render(){},onMatchClicked:null};' }));
    await page.route('**/api/**', async (route) => {
      const u = new URL(route.request().url());
      const p = u.pathname.toLowerCase();
      const m = MOCKS.find(([re]) => re.test(p));
      await new Promise((r) => setTimeout(r, DELAY));
      if (!m) return route.fulfill({ status: 404, body: 'no mock for ' + p });
      const [, body, isText] = m;
      return route.fulfill({ status: 200, contentType: isText ? 'text/plain' : 'application/json', body: isText ? body : JSON.stringify(body) });
    });
    // SignalR: negotiate + websocket
    await page.route('**/hub/TeamsHub/negotiate**', (r) => r.fulfill({ contentType: 'application/json', body: JSON.stringify({ negotiateVersion: 1, connectionId: 'c1', connectionToken: 't1', availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text', 'Binary'] }] }) }));
    await page.routeWebSocket(/\/hub\/TeamsHub/, (ws) => {
      ws.onMessage((msg) => {
        if (String(msg).includes('"protocol"')) { ws.send('{}\x1e'); page.__hub = ws; }
      });
    });

    const t0 = Date.now();
    await page.goto(`http://localhost:${port}${sc.path}`, { waitUntil: 'load' });
    await page.waitForTimeout(SETTLE);
    if (sc.interact) { try { await sc.interact(page); } catch (e) { consoleErrors.push('interact: ' + e.message.slice(0, 200)); } }
    const body = await page.locator('body').innerText();
    const res = { name: sc.name, path: sc.path, checks: {} };
    for (const t of sc.texts || []) res.checks['text:' + t] = body.includes(t);
    for (const [sel, val] of Object.entries(sc.inputs || {})) {
      const v = await page.locator(sel).first().inputValue({ timeout: 1000 }).catch(() => '<missing>');
      res.checks[`input:${sel}=${val}`] = v === val;
      if (v !== val) res.checks[`input:${sel}=${val}`] = false, res['actual:' + sel] = v;
    }
    if (sc.count) { const n = await page.locator(sc.count[0]).count(); res.checks[`count:${sc.count[0]}>=${sc.count[1]}`] = n >= sc.count[1]; res['actual:count'] = n; }
    if (sc.signalr) Object.assign(res.checks, await signalrScenario(page));
    // "Nudge": does an unrelated user event (a click on the toolbar palette button? no -- a keypress on body) make a stale view catch up?
    const failing = Object.entries(res.checks).filter(([, ok]) => !ok).map(([k]) => k);
    if (failing.length && !sc.signalr) {
      await page.locator('mat-toolbar button[aria-label="menu icon"]').click().catch(() => {});
      await page.waitForTimeout(800);
      await page.keyboard.press('Escape');
      await page.waitForTimeout(500);
      const body2 = await page.locator('body').innerText();
      res.afterNudge = {};
      for (const k of failing) {
        if (k.startsWith('text:')) res.afterNudge[k] = body2.includes(k.slice(5));
        else if (k.startsWith('input:')) { const kv = k.slice(6); const i = kv.lastIndexOf('='); const sel = kv.slice(0, i), val = kv.slice(i + 1); res.afterNudge[k] = (await page.locator(sel).first().inputValue({ timeout: 1000 }).catch(() => '')) === val; }
        else if (k.startsWith('count:')) res.afterNudge[k] = (await page.locator(sc.count[0]).count()) >= sc.count[1];
      }
    }
    res.ms = Date.now() - t0;
    res.ng0100 = consoleErrors.filter((e) => e.includes('NG0100')).length;
    res.errors = [...new Set(consoleErrors)].slice(0, 6);
    await page.screenshot({ path: `shots/${label}/${sc.name}.png`, fullPage: false });
    out.push(res);
    const ok = Object.values(res.checks).every(Boolean);
    console.log(`[${label}] ${ok ? 'OK  ' : 'FAIL'} ${sc.name} ${JSON.stringify(res.checks)}${res.afterNudge ? ' nudge=' + JSON.stringify(res.afterNudge) : ''} ng0100=${res.ng0100}`);
    await ctx.close();
  }
  srv.close();
  fs.writeFileSync(`results-${label}.json`, JSON.stringify(out, null, 2));
}

(async () => {
  const browser = await chromium.launch({ executablePath: '/usr/bin/chromium', args: ['--no-sandbox'] });
  let port = 8100;
  for (const arg of process.argv.slice(2)) {
    const [label, dir] = arg.split('=');
    await runBuild(browser, label, dir, port++);
  }
  await browser.close();
})().catch((e) => { console.error(e); process.exit(1); });
