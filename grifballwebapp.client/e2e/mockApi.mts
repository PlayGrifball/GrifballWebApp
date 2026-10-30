// A fake backend with realistic data, shaped like the API's DTOs. Used by the layout test
// (layout.spec.ts, through page.route) and by e2e/serve.mjs as the backend of the app's server
// (pages rendered on the server, Lighthouse CI), so both run without the .NET server. Plain TypeScript Node can run as is (no enums etc.).

export type Role = 'anon' | 'player' | 'admin';

/** A JSON body, or { text } for the endpoints that answer text/plain. null answers 204. */
export type MockBody = unknown;

/** The fixed "now" the data is written around (a Thursday in week 3 of the Autumn Brawl season). */
export const MOCK_NOW = '2026-10-29T18:00:00Z';

/**
 * The session cookie (BrowserAuth.CookieName on the API, SESSION_COOKIE in src/app/pageCache.ts).
 * Its value here is the role it signs in as, for the mock backend of e2e/serve.mjs.
 */
export const SESSION_COOKIE = '__Host-Grifball';

/** The role a Cookie header signs in as: its session cookie's value, or a visitor. */
export function roleOf(cookieHeader: string | undefined): Role {
  const value = (cookieHeader ?? '').split(';').map(c => c.trim()).find(c => c.startsWith(SESSION_COOKIE + '='))?.slice(SESSION_COOKIE.length + 1);
  return value === 'player' || value === 'admin' ? value : 'anon';
}

/**
 * Signs a browser context in as `role`, as the API's login would: the session cookie, for both of
 * the hosts the tests open (localhost renders on the server, which sends it to the mock backend;
 * 127.0.0.1 renders in the browser, where the tests answer the API themselves).
 */
export async function signIn(context: { addCookies(cookies: object[]): Promise<void> }, role: Role): Promise<void> {
  if (role === 'anon')
    return;
  await context.addCookies(['localhost', '127.0.0.1'].map(domain =>
    ({ name: SESSION_COOKIE, value: role, domain, path: '/', httpOnly: true, secure: true, sameSite: 'Lax' })));
}

export const META = {
  player: { isSysAdmin: false, isCommissioner: false, isPlayer: true, displayName: 'MasterChief117', userID: 117 },
  admin: { isSysAdmin: true, isCommissioner: true, isPlayer: true, displayName: 'Commander Grif', userID: 999 },
} as const;

const teams: [number, string, string][] = [
  [1, 'Red Rampage', 'Sarge Prime'], [2, 'Blue Bombers', 'Tucker Tactics'], [3, 'Hammer Bros', 'Grifmeister'],
  [4, 'Sword Swallowers', 'Caboose42'], [5, 'Gravity Wells', 'Donut Delta'], [6, 'Sticky Situation', 'Lopez Unit'],
];
const teamName = (id: number) => teams[id - 1][1];

function roster(teamID: number) {
  const [, name, captain] = teams[teamID - 1];
  const base = ['Plasma Pete', 'Banshee Bri', 'Warthog Wes', 'Needler Nan', 'Spartan Sam', 'Elite Eli', 'Grunt Gus', 'Jackal Jo'];
  return {
    teamName: name, wins: 7 - teamID, losses: teamID - 1, tbd: 2,
    players: [
      { teamPlayerID: teamID * 10, userID: teamID * 10, gamertag: captain, draftCaptainOrder: teamID, draftRound: null },
      ...[0, 1, 2].map(i => ({ teamPlayerID: teamID * 10 + i + 1, userID: teamID * 10 + i + 1, gamertag: base[(teamID + i) % 8], draftCaptainOrder: null, draftRound: i + 1 })),
    ],
  };
}

const scheduled = ([
  [31, 1, 2, '2026-10-30T23:00:00Z', false], [32, 3, 4, '2026-10-29T01:00:00Z', false], [33, 5, 6, '2026-10-27T00:30:00Z', true],
  [21, 1, 3, '2026-10-21T23:00:00Z', true], [22, 2, 5, '2026-10-22T23:00:00Z', true], [23, 4, 6, '2026-10-23T23:00:00Z', true],
  [41, 1, 4, '2026-11-05T00:00:00Z', false], [42, 2, 6, '2026-11-06T00:00:00Z', false], [43, 3, 5, '2026-11-06T01:00:00Z', false],
] as [number, number, number, string, boolean][]).map(([id, h, a, time, complete]) =>
  ({ seasonMatchID: id, homeCaptain: teamName(h), awayCaptain: teamName(a), complete, time }));

// brackets-viewer data (brackets-model): an 8-team single elimination with one BYE and one forfeit.
const side = (id: number, position: number, extra: object = {}) => ({ id, position, ...extra });
const participant = (id: number, name: string) => ({ id, tournament_id: 12, name });
const bracket = {
  stages: [{ id: 1, tournament_id: 12, name: 'Autumn Brawl Playoffs', type: 'single_elimination', number: 1,
    settings: { size: 8, seedOrdering: ['inner_outer'], grandFinal: 'none', consolationFinal: false } }],
  matches: [
    { id: 101, stage_id: 1, group_id: 1, round_id: 1, number: 1, child_count: 0, status: 4, opponent1: side(1, 1, { result: 'win' }), opponent2: null },
    { id: 102, stage_id: 1, group_id: 1, round_id: 1, number: 2, child_count: 0, status: 4, opponent1: side(4, 4, { score: 1, result: 'loss' }), opponent2: side(5, 5, { score: 2, result: 'win' }) },
    { id: 103, stage_id: 1, group_id: 1, round_id: 1, number: 3, child_count: 0, status: 4, opponent1: side(2, 2, { score: 2, result: 'win' }), opponent2: side(7, 7, { score: 0, result: 'loss' }) },
    { id: 104, stage_id: 1, group_id: 1, round_id: 1, number: 4, child_count: 0, status: 4, opponent1: side(3, 3, { score: 0, result: 'loss', forfeit: true }), opponent2: side(6, 6, { result: 'win' }) },
    { id: 105, stage_id: 1, group_id: 1, round_id: 2, number: 1, child_count: 0, status: 3, opponent1: { id: 1, score: 1 }, opponent2: { id: 5, score: 1 } },
    { id: 106, stage_id: 1, group_id: 1, round_id: 2, number: 2, child_count: 0, status: 2, opponent1: { id: 2 }, opponent2: { id: 6 } },
    { id: 107, stage_id: 1, group_id: 1, round_id: 3, number: 1, child_count: 0, status: 0, opponent1: { id: null }, opponent2: { id: null } },
  ],
  matchGames: [],
  participants: [...teams.map(([id, name]) => participant(id, name)), participant(7, 'Late Joiners')],
};

const days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
const times = ['18:00:00', '19:00:00', '20:00:00', '21:00:00', '22:00:00'];
const timeslots = days.flatMap((dayOfWeek, d) => times.map((time, t) =>
  ({ optionID: d * times.length + t + 1, dayOfWeek, time, isChecked: (d * times.length + t + 1) % 3 === 0 })));

const players = ['MasterChief117', 'Plasma Pete', 'Banshee Bri', 'Warthog Wes', 'Needler Nan', 'Caboose42'];
const letters = ['S', 'A+', 'A', 'B+', 'B', 'C', 'D'];
const grades = (i: number, gamertag: string) => {
  const L = (k: number) => letters[(i + k) % letters.length];
  return { xboxUserID: i, gamertag, goals: L(0), kdSpread: L(1), punches: L(2), sprees: L(3), doubleKills: L(1), tripleKills: L(4),
    multiKills: L(5), xFactor: L(2), kills: L(0), gradeAvgMath: 3.2 - i * 0.2, gradeAvg: L(1) };
};

const possibleTeam = (teamID: number, score: number, outcome: string, offset: number) => ({
  teamID, score, outcome,
  players: roster(teamID).players.map((p, i) => ({ xboxUserID: offset + i, gamertag: p.gamertag, score: 900 - offset * 20 - i * 80, kills: 14 - i, deaths: 6 + i, isOnTeam: true })),
});

const users = [['admin', 'Commander Grif', 'CommanderGrif', 'grif#0001'], ['chief', 'MasterChief117', 'MasterChief117', 'chief'], ['tucker', 'Tucker Tactics', null, 'tucker.t']]
  .map(([userName, displayName, gamertag, discord], i) => ({ userID: i + 1, userName, lockoutEnd: null, lockoutEnabled: true, isDummyUser: false,
    accessFailedCount: 0, region: 'NA', displayName, gamertag, discord, externalAuthCount: 1, hasPassword: true, roles: [] }));

// The team builder's board for the Winter Campaign draft (/season/13/teams): two captains with
// their first picks, the rest of the signups in the pool.
const draftPlayer = (personID: number, name: string, round: number | null = null) => ({ name, personID, pick: round, round });
const draftTeams = [
  { teamID: 1, teamName: "Sarge Prime's Crew", captain: { name: 'Sarge Prime', personID: 201, order: 1 }, players: [draftPlayer(301, 'Plasma Pete', 1)] },
  { teamID: 2, teamName: "Caboose42's Crew", captain: { name: 'Caboose42', personID: 202, order: 2 }, players: [draftPlayer(302, 'Banshee Bri', 1)] },
];
const draftPool = ['Warthog Wes', 'Needler Nan', 'Spartan Sam', 'Elite Eli', 'Grunt Gus', 'Jackal Jo'].map((name, i) => draftPlayer(401 + i, name));

// The commissioner dashboard: a reschedule waiting for a decision and a match past its time.
const commissionerDashboard = {
  pendingReschedules: [{ matchRescheduleID: 7, seasonMatchID: 32, homeCaptain: 'Hammer Bros', awayCaptain: 'Sword Swallowers',
    originalScheduledTime: '2026-10-29T01:00:00Z', newScheduledTime: '2026-10-31T01:00:00Z', reason: 'Half the team is at a LAN that night',
    requestedByGamertag: 'Grifmeister', requestedAt: '2026-10-27T15:20:00Z', status: 'Pending', discordThreadID: null }],
  overdueMatches: [{ seasonMatchID: 33, homeCaptain: 'Gravity Wells', awayCaptain: 'Sticky Situation', scheduledTime: '2026-10-27T00:30:00Z', hoursOverdue: 65 }],
  summary: { pendingRescheduleCount: 1, overdueMatchCount: 1, criticalOverdueCount: 1 },
};

// The Winter Campaign's signups close on 10 November, or (the server that renders the pages goes by
// the real clock, not MOCK_NOW) a month from now if that is later: open signups, with their Sign up
// buttons, whenever the tests run.
const signupsClose = new Date(Math.max(Date.parse('2026-11-10T23:59:00Z'), Date.now() + 30 * 24 * 3600 * 1000)).toISOString();

const seasonNames: Record<number, string> = { 11: 'Summer Smash', 12: 'Autumn Brawl', 13: 'Winter Campaign' };

// The season editor's form (/seasonEdit/12), in the shape GetSeasons lists them
const season = (seasonID: number) => ({ seasonID, seasonName: seasonNames[seasonID], signupsOpen: '2026-08-01T00:00:00Z',
  signupsClose: '2026-08-31T00:00:00Z', draftStart: '2026-09-02T00:00:00Z', seasonStart: '2026-09-07T00:00:00Z', seasonEnd: '2026-11-30T00:00:00Z', signupsCount: 24 });

// A user as User Management edits and merges them (/usermanagement/edituser/2, /usermanagement/mergeuser/2/3)
const roleNames = ['Sysadmin', 'Commissioner', 'Player'];
const userDetail = (user: typeof users[number]) => ({ ...user, roles: roleNames.map((roleName, i) => ({ roleName, hasRole: user.userID === 1 || i === 2 })) });

// The top kills of all time (/topstats)
const topKills = ['Grifmeister', 'Sarge Prime', 'MasterChief117', 'Caboose42', 'Plasma Pete', 'Banshee Bri', 'Tucker Tactics', 'Donut Delta']
  .map((gamertag, i) => ({ rank: i + 1, gamertag, kills: 1840 - i * 137 }));

/** The mocked endpoints, by path (without the query string). */
export function mockRoutes(role: Role): Record<string, MockBody> {
  return {
    // Who is signed in: a visitor gets 204 No Content
    '/api/Identity/Me': role === 'anon' ? null : META[role],
    '/api/Identity/Antiforgery': { token: `e2e-xsrf-${role}` },
    '/api/CommitHash': { text: '9f3c2ab' },
    '/api/CommitDate': { text: '2026-10-28T21:14:00Z' },
    '/api/Season/getCurrentSeasonID/': 12,
    '/api/Home/CurrentAndFutureEvents/': [
      { seasonID: 13, name: 'Winter Campaign', start: '2026-10-20T00:00:00Z', end: signupsClose, eventType: 'Signup' },
      { seasonID: 13, name: 'Winter Campaign', start: '2026-11-14T19:00:00Z', end: '2026-11-14T23:00:00Z', eventType: 'Draft' },
      { seasonID: 12, name: 'Autumn Brawl', start: '2026-09-07T00:00:00Z', end: '2026-11-30T00:00:00Z', eventType: 'Season' },
      { seasonID: 13, name: 'Winter Campaign', start: '2026-11-20T00:00:00Z', end: '2027-02-15T00:00:00Z', eventType: 'Season' },
    ],
    '/api/Home/PastSeasons': {
      totalCount: 11,
      results: ([['Summer Smash', '2026-06-01', '2026-08-20', 11], ['Spring Showdown', '2026-03-02', '2026-05-18', 10],
        ['Frostbite Cup', '2025-12-01', '2026-02-15', 9], ['Harvest Hammer', '2025-09-01', '2025-11-20', 8], ['Season 7', '2025-06-02', '2025-08-18', 7]] as [string, string, string, number][])
        .map(([name, start, end, seasonID]) => ({ seasonID, name, start: start + 'T00:00:00Z', end: end + 'T00:00:00Z', eventType: 'Season' })),
    },
    ...Object.fromEntries(Object.entries(seasonNames).map(([id, name]) => [`/api/Season/getSeasonName/${id}`, { text: name }])),
    '/api/TeamStandings/GetTeamStandings/12': teams.map(([teamID, name], i) => ({ teamID, teamName: name, wins: 7 - i - (i > 2 ? 1 : 0), losses: i + 1 })),
    '/api/MatchPlanner/GetScheduledMatches/12': scheduled,
    '/api/MatchPlanner/GetUnscheduledMatches/12': [{ seasonMatchID: 51, homeCaptain: 'Gravity Wells', awayCaptain: 'Red Rampage', complete: false }],
    '/api/Brackets/GetViewerData': bracket,
    '/api/SeasonMatch/GetSeasonMatchPage/31': {
      seasonID: 12, seasonName: 'Autumn Brawl', isPlayoff: false,
      homeTeamName: 'Red Rampage', homeTeamID: 1, homeTeamScore: 1, homeTeamResult: 'TBD',
      awayTeamName: 'Blue Bombers', awayTeamID: 2, awayTeamScore: 0, awayTeamResult: 'TBD',
      scheduledTime: '2026-10-30T23:00:00Z', bestOf: 3,
      reportedGames: [{ matchNumber: 1, matchID: '0f8fad5b-d9cb-469f-a165-70867728950e' }],
      bracketInfo: null, activeRescheduleRequestId: null,
    },
    '/api/SeasonMatch/GetPossibleMatches/31': [{ matchID: '7c9e6679-7425-40de-944b-e07fc1f90ae7', homeTeam: possibleTeam(1, 5, 'Won', 0), awayTeam: possibleTeam(2, 3, 'Lost', 10) }],
    ...Object.fromEntries(teams.map(([id]) => [`/api/Team/Team/${id}`, roster(id)])),
    '/api/Team/Matches/1': [
      { seasonMatchID: 21, scheduledTime: '2026-10-21T23:00:00Z', bestOf: 3, score: 2, result: 'Won', otherTeamID: 3, otherTeamName: 'Hammer Bros', otherScore: 1, otherResult: 'Loss' },
      { seasonMatchID: 31, scheduledTime: '2026-10-30T23:00:00Z', bestOf: 3, score: null, result: null, otherTeamID: 2, otherTeamName: 'Blue Bombers', otherScore: null, otherResult: null },
    ],
    '/api/profile/getgamertag/117': { gamertag: 'MasterChief117' },
    '/api/profile/getgamertag/999': { gamertag: 'CommanderGrif' },
    '/api/ExternalLogins/Mine': { hasPassword: true, logins: [{ loginProvider: 'Discord', providerKey: '81234567890123456', providerDisplayName: 'Discord' }], providers: [{ name: 'Discord', displayName: 'Discord' }] },
    '/api/Signups/getSignup/13': null,
    '/api/Signups/getTimeslots/13': timeslots,
    '/api/Signups/getSignups/12': players.map((personName, i) => ({ seasonID: 12, userID: i, personName, timestamp: `2026-10-2${i}T18:0${i}:00Z`,
      teamName: i % 3 === 0 ? `${personName}'s Crew` : null, willCaptain: i % 3 === 0, requiresAssistanceDrafting: i === 3, timeslots: [] })),
    '/api/Grades/GetGrades/12': { totals: [], perMinutes: [], letters: players.slice(0, 5).map((p, i) => grades(i, p)) },
    '/api/EventOrganizer/GetSeasons': [12, 13, 11].map(season),
    '/api/EventOrganizer/GetSeason/12': { ...season(12), copyFrom: null, copyAvailability: false, copySignups: false, copyTeams: false },
    // The times a season offers for signups (?seasonID=12): weeknights at 7 and 8
    '/api/Availability/GetSeasonAvailability': ['Monday', 'Wednesday', 'Thursday'].flatMap(dayOfWeek => ['19:00:00', '20:00:00'].map(time => ({ dayOfWeek, time }))),
    '/api/stats/TopKills': topKills,
    '/api/Admin/CheckStatus': { text: 'Signed in to Halo Infinite (token valid for 41 minutes)' },
    '/api/Excel/DefaultSheetInfo': [{ name: 'Autumn Brawl results', spreadsheetID: '1xGrifballAutumnBrawl2026', sheetName: 'Week 3' }],
    '/api/UserManagement/GetUsers': { totalCount: users.length, results: users },
    ...Object.fromEntries(users.map(user => ['/api/usermanagement/getuser/' + user.userID, userDetail(user)])),
    // The accounts linked to a user, on their edit page
    ...Object.fromEntries(users.map(user => ['/api/ExternalLogins/UserLogins/' + user.userID, { hasPassword: user.hasPassword,
      logins: [{ loginProvider: 'Discord', providerKey: '8123456789012345' + user.userID, providerDisplayName: 'Discord' }], providers: [{ name: 'Discord', displayName: 'Discord' }] }])),
    '/api/Teams/GetTeams/13': draftTeams,
    '/api/Teams/GetPlayerPool/13': draftPool,
    '/api/Teams/areCaptainsLocked/13': true,
    '/api/CommissionerDashboard/GetDashboardData': commissionerDashboard,
  };
}

/** How to answer one request: status, content type and body; undefined for an unknown path (404). */
export function mockResponse(role: Role, path: string): { status: number, contentType: string, body: string } | undefined {
  const routes = mockRoutes(role);
  if (!(path in routes))
    return undefined;
  const body = routes[path];
  if (body === null)
    return { status: 204, contentType: 'application/json', body: '' };
  if (typeof body === 'object' && body !== null && 'text' in body && Object.keys(body).length === 1)
    return { status: 200, contentType: 'text/plain', body: String((body as { text: string }).text) };
  return { status: 200, contentType: 'application/json', body: JSON.stringify(body) };
}
