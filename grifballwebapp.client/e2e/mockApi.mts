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
    teamName: name, season: seasonInfo(12, { hasEnded: false }), wins: 7 - teamID, losses: teamID - 1, tbd: 2,
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
  ({ seasonMatchID: id, homeCaptain: teamName(h), awayCaptain: teamName(a), homeTeam: teamName(h), awayTeam: teamName(a), complete, time }));

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

// The team builder's board for the Winter Campaign draft (/season/13/draft): two captains with
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
/** A season's facts (SeasonInfoDto), as every DTO of or about a season has them: an AGLA Halo Infinite season of 2026 played in the app unless changed. */
const seasonInfo = (seasonID: number, changes: Record<string, unknown> = {}) => ({
  seasonID, seasonName: seasonNames[seasonID] ?? `Season ${seasonID}`, start: '2026-01-01T00:00:00Z', end: '2026-03-01T00:00:00Z', year: 2026,
  hasEnded: true, leagueID: 1, leagueName: 'AGLA', leagueSlug: 'agla', gameVersionID: 6, gameVersionName: 'Halo Infinite', gameVersionSlug: 'infinite',
  division: null, format: 'LeagueSeason', isHistorical: false, historicalSourceUrl: null, ...changes,
});

// The season editor's form (/seasonEdit/12), in the shape GetSeasons lists them
const season = (seasonID: number) => ({ seasonID, seasonName: seasonNames[seasonID], signupsOpen: '2026-08-01T00:00:00Z',
  signupsClose: '2026-08-31T00:00:00Z', draftStart: '2026-09-02T00:00:00Z', seasonStart: '2026-09-07T00:00:00Z', seasonEnd: '2026-11-30T00:00:00Z', signupsCount: 24,
  gameVersionID: 6, leagueID: 1, division: null, format: 'LeagueSeason' });

// A user as User Management edits and merges them (/usermanagement/edituser/2, /usermanagement/mergeuser/2/3)
const roleNames = ['Sysadmin', 'Commissioner', 'Player'];
const userDetail = (user: typeof users[number]) => ({ ...user, roles: roleNames.map((roleName, i) => ({ roleName, hasRole: user.userID === 1 || i === 2 })) });

// A profile's most earned medals (/profile/117, /profile/999)
const commendations = (userID: number) => ({
  userID, totalMedals: 1342, distinctMedals: 9,
  commendations: [
    { medalID: 1, medalName: 'Killing Spree', description: 'Kill 5 enemies without dying.', difficultyName: 'Normal', typeName: 'Spree', count: 412, matches: 120, tier: 4, tierFloor: 250, nextTierAt: 500 },
    { medalID: 3, medalName: 'Double Kill', description: 'Kill 2 enemies in quick succession.', difficultyName: 'Normal', typeName: 'Multikill', count: 64, matches: 41, tier: 2, tierFloor: 50, nextTierAt: 100 },
    { medalID: 5, medalName: 'Overkill', description: 'Kill 4 enemies in quick succession.', difficultyName: 'Mythic', typeName: 'Multikill', count: 3, matches: 3, tier: 0, tierFloor: 0, nextTierAt: 10 },
  ],
});

const profileStats = (userID: number, gamertag: string | null) => ({
  userID,
  gamertag,
  displayName: gamertag ?? `User ${userID}`,
  isDummyUser: false,
  regionID: 2,
  regionName: 'Central North America',
  pastGamertags: [],
  championships: [
    {
      playerChampionshipID: 1,
      season: seasonInfo(12, { leagueName: 'Grifball League', leagueSlug: 'gl', division: 'Pro' }),
      gameVersionID: 1,
      gameVersionName: 'Halo 3',
      gameVersionSlug: 'h3',
      tier: 'Pro',
      teamID: 2,
      teamName: 'Blue Bombers',
      isCaptain: true,
      gamertagUsed: gamertag ?? 'Spartan',
    },
  ],
  careerByGame: [
    {
      gameVersionID: 1,
      gameVersionName: 'Halo 3',
      gameVersionSlug: 'h3',
      seasonsCount: 1,
      gamesPlayed: 10,
      wins: 8,
      losses: 2,
      winPercent: 80,
      goals: 15,
      goalsPerGame: 1.5,
      kills: 120,
      deaths: 60,
      assists: 20,
      kdRatio: 2.0,
      kdSpread: 60,
      carrierKills: 8,
      stopsPerGame: 0.8,
      sprees: 5,
      multiKills: 12,
      exterminations: 1,
      betrayals: 0,
      hammerKills: 80,
      swordKills: 25,
      ballKills: 15,
      punches: 0,
      damageDealt: null,
      damageTaken: null,
      score: null,
    },
  ],
  overallCareer: {
    gameVersionID: null,
    gameVersionName: 'Career',
    gameVersionSlug: 'career',
    seasonsCount: 1,
    gamesPlayed: 10,
    wins: 8,
    losses: 2,
    winPercent: 80,
    // Across games only what every game counts alike: goals, stops and medals are per game
    goals: null,
    goalsPerGame: null,
    kills: 120,
    deaths: 60,
    assists: 20,
    kdRatio: 2.0,
    kdSpread: 60,
    carrierKills: null,
    stopsPerGame: null,
    sprees: null,
    multiKills: null,
    exterminations: null,
    betrayals: null,
    hammerKills: null,
    swordKills: null,
    ballKills: null,
    punches: null,
    damageDealt: null,
    damageTaken: null,
    score: null,
  },
  seasons: [
    {
      playerSeasonStatID: 1,
      season: seasonInfo(12, { leagueName: 'Grifball League', leagueSlug: 'gl', division: 'Pro' }),
      gameVersionID: 1,
      gameVersionName: 'Halo 3',
      gameVersionSlug: 'h3',
      teamID: 2,
      teamName: 'Blue Bombers',
      tradeNote: null,
      roleNote: null,
      stintIndex: 0,
      isPlayoffs: false,
      isCombined: false,
      source: 'Calculated',
      gamesPlayed: 10,
      wins: 8,
      losses: 2,
      winPercent: 80,
      goals: 15,
      kills: 120,
      deaths: 60,
      assists: 20,
      kdRatio: 2.0,
      kdSpread: 60,
      carrierKills: 8,
      sprees: 5,
      multiKills: 12,
      exterminations: 1,
      damageDealt: null,
      damageTaken: null,
    },
  ],
});

/** A season played in the app, as GetSeasonSummary answers. */
const summary = (seasonID: number) => ({
  ...seasonInfo(seasonID, { hasEnded: seasonID === 11 }), teamCount: 6, signupCount: 10, matchCount: 12, bracketMatchCount: 7, playerStatCount: 0, champions: [],
});

// Season 5 was backfilled from the historical archive: teams, rosters, stats and a title, but no signups, schedule or matches
const HISTORICAL_SOURCE = 'https://docs.google.com/spreadsheets/d/e2e-summer-league-2010';
const historicalSeason = seasonInfo(5, { seasonName: 'Summer League 2010', start: '2010-01-01T00:00:00Z', end: '2010-12-31T00:00:00Z', year: 2010,
  gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', division: 'Pro', isHistorical: true, historicalSourceUrl: HISTORICAL_SOURCE });
const historicalTeams: [number, string, number, number][] = [[41, 'Relentless', 9, 1], [42, 'Benchwarmers', 5, 5], [43, 'Truthless and the Truthlessettes', 1, 9]];
const historicalStat = (id: number, gamertag: string, teamID: number, goals: number, isPlayoffs = false) => ({
  playerSeasonStatID: id, userID: 500 + id, gamertag, teamID, teamName: historicalTeams.find(t => t[0] === teamID)![1],
  tradeNote: null, roleNote: null, stintIndex: 1, isPlayoffs, isCombined: false, source: 'Manual', gamesPlayed: 10, wins: 6, losses: 4,
  winPercent: 60, goals, kills: 60 + goals, deaths: 40, assists: 5, kdRatio: 1.5, kdSpread: 20 + goals, carrierKills: 4, sprees: 2,
  multiKills: 3, exterminations: 0,
});
const historicalStats = [
  historicalStat(1, 'Legendary', 41, 14), historicalStat(2, 'Old Timer', 41, 9), historicalStat(3, 'Bench Boss', 42, 7),
  historicalStat(4, 'Truthless', 43, 3), historicalStat(5, 'Legendary', 41, 4, true),
];

/** A season in the season list (GET /api/Season/GetSeasons), played in the app unless changed. */
const seasonListItem = (seasonID: number, seasonName: string, start: string, end: string, changes: Record<string, unknown> = {}) => ({
  ...seasonInfo(seasonID, { seasonName, start: start + 'T00:00:00Z', end: end + 'T00:00:00Z', year: Number(start.slice(0, 4)) }),
  teamCount: 6, playerCount: 30, champions: [], ...changes,
});
/** A title on the leaderboards: its season, tier and winning team. */
const title = (seasonID: number, seasonName: string, tier: string, teamID: number) => ({ seasonID, seasonName, tier, teamID });
const leader = (userID: number, gamertag: string, statValue: number, perGame: number | null) => ({ userID, gamertag, statValue, gamesPlayed: 30, perGame, seasonsCount: 3 });

// Four pending gamertag claims on the backfilled season 5's players: a roster match with a team, a set gamertag
// without, an XUID lookup (who holds the gamertag now) and a user's claim of a past gamertag
const gamertagClaims = [
  { gamertagClaimID: 1, kind: 'RosterMatch', status: 'Pending', xboxUserID: 2535400000000117, xboxGamertag: 'MasterChief117', userID: 117,
    gamertag: 'Legendary', placeholderUserID: 501, seasonID: 5, seasonName: 'Summer League 2010', teamID: 41, teamName: 'Relentless',
    createdAt: '2026-10-27T15:20:00Z', currentGamertag: null, lookedUpAt: null, placeholderSeasons: [] },
  { gamertagClaimID: 2, kind: 'SetGamertag', status: 'Pending', xboxUserID: 2535400000000002, xboxGamertag: 'Old Timer', userID: 2,
    gamertag: 'Old Timer', placeholderUserID: 502, seasonID: 5, seasonName: 'Summer League 2010', teamID: null, teamName: null,
    createdAt: '2026-10-28T09:05:00Z', currentGamertag: null, lookedUpAt: null, placeholderSeasons: [] },
  { gamertagClaimID: 3, kind: 'XuidLookup', status: 'Pending', xboxUserID: 2535400000000503, xboxGamertag: null, userID: null,
    gamertag: 'Old School', placeholderUserID: 503, seasonID: 5, seasonName: 'Summer League 2010', teamID: null, teamName: null,
    createdAt: '2026-10-29T08:00:00Z', currentGamertag: 'ClutchKing22', lookedUpAt: '2026-10-29T09:00:00Z',
    placeholderSeasons: [{ seasonID: 5, seasonName: 'Summer League 2010', year: 2010 }] },
  { gamertagClaimID: 4, kind: 'UserClaim', status: 'Pending', xboxUserID: null, xboxGamertag: null, userID: 2, claimantUserID: 2,
    claimantName: 'Old Timer', firstSeen: '2009-06-01T00:00:00Z', lastSeen: null, rejectionReason: null,
    gamertag: 'Timer Old', placeholderUserID: 504, seasonID: null, seasonName: null, teamID: null, teamName: null,
    createdAt: '2026-10-30T08:00:00Z', currentGamertag: null, lookedUpAt: null, placeholderSeasons: [{ seasonID: 5, seasonName: 'Summer League 2010', year: 2010 }] },
];

// The merges page (/usermanagement/merges): a claim approval, and a manual merge that was undone
const userMerges = [
  { userMergeID: 2, mergedAt: '2026-10-30T10:00:00Z', trigger: 'ClaimApproval', reason: 'claim 1', triggeredByUserID: 999, triggeredByUserName: 'Commander Grif',
    sourceUserID: 501, sourceUserName: 'historical:Legendary', targetUserID: 117, targetUserName: 'MasterChief117', rowsMoved: 4, rowsRemoved: 2, rowsAdded: 1,
    undoneAt: null, undoneByUserID: null, undoNote: null },
  { userMergeID: 1, mergedAt: '2026-10-29T10:00:00Z', trigger: 'Manual', reason: null, triggeredByUserID: 999, triggeredByUserName: 'Commander Grif',
    sourceUserID: 3, sourceUserName: 'grif2', targetUserID: 2, targetUserName: 'grif', rowsMoved: 2, rowsRemoved: 1, rowsAdded: 0,
    undoneAt: '2026-10-29T11:00:00Z', undoneByUserID: 999, undoNote: 'Different players' },
];

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
    // The season list: the home page's past seasons and the seasons page (the backfilled season 5 among them)
    '/api/Season/GetSeasons': {
      totalCount: 7,
      results: [
        seasonListItem(12, 'Autumn Brawl', '2026-09-07', '2026-11-30', { hasEnded: false }),
        ...([['Summer Smash', '2026-06-01', '2026-08-20', 11], ['Spring Showdown', '2026-03-02', '2026-05-18', 10],
          ['Frostbite Cup', '2025-12-01', '2026-02-15', 9], ['Harvest Hammer', '2025-09-01', '2025-11-20', 8], ['Season 7', '2025-06-02', '2025-08-18', 7]] as [string, string, string, number][])
          .map(([name, start, end, seasonID]) => seasonListItem(seasonID, name, start, end)),
        seasonListItem(5, 'Summer League 2010', '2010-01-01', '2010-12-31', {
          gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', division: 'Pro', isHistorical: true, historicalSourceUrl: HISTORICAL_SOURCE,
          teamCount: 3, playerCount: 12, champions: [{ tier: 'Pro', teamID: 41, teamName: 'Relentless', roster: [] }] }),
      ],
    },
    '/api/Leaderboards/GetLeaderboards': {
      ringLeaders: [{ userID: 501, gamertag: 'Legendary', ringsCount: 3, titles: [title(5, 'Summer League 2010', 'Pro', 41), title(11, 'Summer Smash', 'Open', 1), title(9, 'Frostbite Cup', 'Open', 2)] },
        { userID: 117, gamertag: 'MasterChief117', ringsCount: 1, titles: [title(12, 'Autumn Brawl', 'Open', 2)] }],
      dynasties: [{ teamName: 'Relentless', latestTeamID: 41, latestSeasonID: 5, championshipsCount: 2, seasonsActive: 3, titles: [title(5, 'Summer League 2010', 'Pro', 41), title(11, 'Summer Smash', 'Open', 1)] }],
      goalLeaders: [leader(501, 'Legendary', 120, 4), leader(117, 'MasterChief117', 88, 2.93)],
      killLeaders: [leader(117, 'MasterChief117', 640, 21.33)],
      gamesPlayedLeaders: [leader(501, 'Legendary', 30, null)],
      carrierStopLeaders: [leader(501, 'Legendary', 45, 1.5)],
    },
    ...Object.fromEntries(Object.entries(seasonNames).map(([id, name]) => [`/api/Season/getSeasonName/${id}`, { text: name }])),
    ...Object.fromEntries(Object.keys(seasonNames).map(id => [`/api/Season/getSeasonSummary/${id}`, summary(Number(id))])),
    '/api/Season/getSeasonSummary/5': {
      ...historicalSeason, teamCount: 3, signupCount: 0,
      matchCount: 0, bracketMatchCount: 0, playerStatCount: historicalStats.length,
      champions: [{ tier: 'Pro', teamID: 41, teamName: 'Relentless', roster: [
        { userID: 501, gamertag: 'Legendary', isCaptain: true }, { userID: 502, gamertag: 'Old Timer', isCaptain: false }] }],
    },
    // The season's stats, and on the team page (?teamID=41) its team's: matched on the path alone, so the same rows
    '/api/Season/GetSeasonPlayerStats/5': historicalStats,
    // Season 12 has no stored stats yet: its team pages show no stats table
    '/api/Season/GetSeasonPlayerStats/12': [],
    '/api/Season/GetSeasonTeams/5': [
      {
        teamID: 41, teamName: 'Relentless', wins: 9, losses: 1, tbd: 0, recordFromSource: true, titles: ['Pro'],
        players: [
          { teamPlayerID: 4101, userID: 501, gamertag: 'Legendary', isCaptain: true },
          { teamPlayerID: 4102, userID: 502, gamertag: 'Old Timer', isCaptain: false },
        ],
      },
    ],
    '/api/Season/GetSeasonTeams/12': teams.map(([teamID, name], i) => ({
      teamID, teamName: name, wins: 7 - i - (i > 2 ? 1 : 0), losses: i + 1, tbd: 0, recordFromSource: false, titles: [],
      players: [
        { teamPlayerID: teamID * 10 + 1, userID: teamID * 100 + 1, gamertag: `${name} Captain`, isCaptain: true },
      ],
    })),
    '/api/TeamStandings/GetTeamStandings/5': historicalTeams.map(([teamID, teamName, wins, losses]) => ({ teamID, teamName, wins, losses, recordFromSource: true })),
    '/api/MatchPlanner/GetScheduledMatches/5': [],
    '/api/MatchPlanner/GetUnscheduledMatches/5': [],
    '/api/Team/Team/41': {
      teamName: 'Relentless', season: historicalSeason, recordFromSource: true,
      wins: 9, losses: 1, tbd: 0, titles: ['Pro'],
      players: [
        { teamPlayerID: 4101, userID: 501, gamertag: 'Legendary', draftCaptainOrder: null, draftRound: null, isCaptain: true },
        { teamPlayerID: 4102, userID: 502, gamertag: 'Old Timer', draftCaptainOrder: null, draftRound: null, isCaptain: false },
      ],
    },
    '/api/Team/Matches/41': [],
    '/api/TeamStandings/GetTeamStandings/12': teams.map(([teamID, name], i) => ({ teamID, teamName: name, wins: 7 - i - (i > 2 ? 1 : 0), losses: i + 1 })),
    '/api/MatchPlanner/GetScheduledMatches/12': scheduled,
    '/api/MatchPlanner/GetUnscheduledMatches/12': [{ seasonMatchID: 51, homeCaptain: 'Gravity Wells', awayCaptain: 'Red Rampage', homeTeam: 'Gravity Wells', awayTeam: 'Red Rampage', complete: false }],
    '/api/Brackets/GetViewerData': bracket,
    '/api/SeasonMatch/GetSeasonMatchPage/31': {
      seasonID: 12, seasonName: 'Autumn Brawl', isPlayoff: false,
      homeTeamName: 'Red Rampage', homeTeamID: 1, homeTeamScore: 1, homeTeamResult: 'TBD',
      awayTeamName: 'Blue Bombers', awayTeamID: 2, awayTeamScore: 0, awayTeamResult: 'TBD',
      scheduledTime: '2026-10-30T23:00:00Z', bestOf: 3,
      reportedGames: [{
        matchNumber: 1,
        matchID: '0f8fad5b-d9cb-469f-a165-70867728950e',
        homeScore: 5,
        awayScore: 2,
        homePlayers: [
          { userID: 1, gamertag: 'MasterChief117', kills: 24, deaths: 12, assists: 3, bombScores: 3, score: 300 },
          { userID: 2, gamertag: 'Gravity Wells', kills: 18, deaths: 15, assists: 4, bombScores: 2, score: 200 }
        ],
        awayPlayers: [
          { userID: 999, gamertag: 'CommanderGrif', kills: 16, deaths: 22, assists: 1, bombScores: 1, score: 100 },
          { userID: 4, gamertag: 'Blue Bomber', kills: 11, deaths: 20, assists: 2, bombScores: 1, score: 100 }
        ]
      }],
      seriesTotals: {
        homePlayers: [
          { userID: 1, gamertag: 'MasterChief117', kills: 24, deaths: 12, assists: 3, bombScores: 3, score: 300 },
          { userID: 2, gamertag: 'Gravity Wells', kills: 18, deaths: 15, assists: 4, bombScores: 2, score: 200 }
        ],
        awayPlayers: [
          { userID: 999, gamertag: 'CommanderGrif', kills: 16, deaths: 22, assists: 1, bombScores: 1, score: 100 },
          { userID: 4, gamertag: 'Blue Bomber', kills: 11, deaths: 20, assists: 2, bombScores: 1, score: 100 }
        ]
      },
      bracketInfo: null, activeRescheduleRequestId: null,
      // Signed in, the mock users play in (or run) this match: the server would take a reschedule request
      canRequestReschedule: role !== 'anon',
    },
    '/api/SeasonMatch/GetPossibleMatches/31': [{ matchID: '7c9e6679-7425-40de-944b-e07fc1f90ae7', homeTeam: possibleTeam(1, 5, 'Won', 0), awayTeam: possibleTeam(2, 3, 'Lost', 10) }],
    '/api/SeasonMatch/MatchPullStatus/31': { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: { firedAt: '2026-10-30T18:00:00Z', durationMs: 42000, result: 'Succeeded', error: null } },
    ...Object.fromEntries(teams.map(([id]) => [`/api/Team/Team/${id}`, roster(id)])),
    '/api/Team/Matches/1': [
      { seasonMatchID: 21, scheduledTime: '2026-10-21T23:00:00Z', bestOf: 3, score: 2, result: 'Won', otherTeamID: 3, otherTeamName: 'Hammer Bros', otherScore: 1, otherResult: 'Loss' },
      { seasonMatchID: 31, scheduledTime: '2026-10-30T23:00:00Z', bestOf: 3, score: null, result: null, otherTeamID: 2, otherTeamName: 'Blue Bombers', otherScore: null, otherResult: null },
    ],
    '/api/profile/getgamertag/117': { gamertag: 'MasterChief117' },
    '/api/profile/getgamertag/999': { gamertag: 'CommanderGrif' },
    '/api/profile/getprofilestats/117': profileStats(117, 'MasterChief117'),
    '/api/profile/getprofilestats/999': profileStats(999, 'CommanderGrif'),
    '/api/profile/getcommendations/117': commendations(117),
    '/api/profile/getcommendations/999': commendations(999),
    '/api/ExternalLogins/Mine': { hasPassword: true, logins: [{ loginProvider: 'Discord', providerKey: '81234567890123456', providerDisplayName: 'Discord' }], providers: [{ name: 'Discord', displayName: 'Discord' }] },
    '/api/Signups/getSignup/13': null,
    '/api/Signups/getTimeslots/13': timeslots,
    '/api/Signups/getSignups/12': players.map((personName, i) => ({ seasonID: 12, userID: i, personName, timestamp: `2026-10-2${i}T18:0${i}:00Z`,
      teamName: i % 3 === 0 ? `${personName}'s Crew` : null, willCaptain: i % 3 === 0, requiresAssistanceDrafting: i === 3, timeslots: [] })),
    '/api/Grades/GetGrades/12': { totals: [], perMinutes: [], letters: players.slice(0, 5).map((p, i) => grades(i, p)) },
    '/api/EventOrganizer/GetSeasons': [12, 13, 11].map(season),
    '/api/EventOrganizer/GetSeason/12': { ...season(12), copyFromSeasonID: null, copyAvailability: false, copySignups: false, copyTeams: false },
    '/api/EventOrganizer/GetGameVersions': [{ gameVersionID: 1, name: 'Halo 3' }, { gameVersionID: 6, name: 'Halo Infinite' }],
    '/api/EventOrganizer/GetLeagues': [{ leagueID: 1, name: 'AGLA' }, { leagueID: 2, name: 'Late League' }],
    // The times a season offers for signups (?seasonID=12): weeknights at 7 and 8
    '/api/Availability/GetSeasonAvailability': ['Monday', 'Wednesday', 'Thursday'].flatMap(dayOfWeek => ['19:00:00', '20:00:00'].map(time => ({ dayOfWeek, time }))),
    '/api/Admin/CheckStatus': { text: 'Signed in to Halo Infinite (token valid for 41 minutes)' },
    // The Infinite Client page in the default Gateway mode
    '/api/Admin/HaloStatus': { mode: 'Gateway', gatewayUrl: 'http://halo-gateway.halo-gateway.svc:8080/', gatewayAdminUrl: 'https://grunt.grifball.xyz/admin', apiKeyConfigured: true, reachable: true, accountStatus: 'Healthy', apiCode: 200, healthy: true, message: 'Halo gateway account Healthy; test call: Halo Infinite API answered 200' },
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
    // The historical import page (/usermanagement/historicalimport): its sources, jobs, schedule, and a guarded import's job
    '/api/HistoricalImport/Sources': historicalSources,
    '/api/HistoricalImport/Jobs': historicalJobs,
    '/api/HistoricalImport/Schedule': { paused: false, interval: '1.00:00:00', lastRunAt: '2026-10-29T06:00:00Z', nextRunAt: '2026-10-30T06:00:00Z', lastJobID: 40, lastRunNote: null },
    '/api/HistoricalImport/Job/41': historicalJob,
    // The Jobs page (/usermanagement/jobs) and a job's runs (?group=&name=; matched on the path alone)
    '/api/Jobs/List': jobsOverview,
    '/api/Jobs/History': jobHistory,
    // VODs: the match page's, the seasons', the teams' and the profiles' (through their teams), and the review list
    '/api/Vod/ForSeasonMatch/31': [matchVod],
    '/api/Vod/ForSeason/12': [matchVod, castVod],
    '/api/Vod/ForSeason/5': [archiveVod],
    '/api/Vod/ForTeam/1': [matchVod],
    '/api/Vod/ForTeam/41': [archiveVod],
    '/api/Vod/ForPlayer/117': [matchVod, archiveVod],
    '/api/Vod/ForPlayer/999': [],
    '/api/Vod/Review': vodReview,
    // The gamertag claims page (/usermanagement/claims): three pending claims (?status=Pending; matched on the path alone)
    '/api/GamertagClaims/GetClaims': gamertagClaims,
    '/api/UserMerges/List': { totalCount: userMerges.length, results: userMerges },
    // The signed-in admin's own profile (/profile/999): their claims of past gamertags
    '/api/MyProfile/GamertagClaims': [{ ...gamertagClaims[3], claimantUserID: 999, userID: 999, claimantName: 'Commander Grif', status: 'Rejected', rejectionReason: 'That was someone else' }],
    '/api/GamertagClaims/XuidLookupStatus': { placeholders: 12, due: 3, found: 7, notFound: 1, failed: 1, lastLookedUpAt: '2026-10-29T09:00:00Z' },
    // The past gamertags page (/usermanagement/pastgamertags?xboxUserID=...): one manually-added row, one detected from a rename
    '/api/GamertagClaims/PastGamertags': {
      xboxUserID: 2535400000000117, currentGamertag: 'MasterChief117', userID: 117,
      pastGamertags: [
        { xboxGamertagHistoryID: 1, xboxUserID: 2535400000000117, gamertag: 'Legendary', firstSeen: '2010-01-01T00:00:00Z',
          lastSeen: '2012-06-01T00:00:00Z', source: 'Manual', note: 'From the Summer League 2010 roster', canRemove: true },
        { xboxGamertagHistoryID: 2, xboxUserID: 2535400000000117, gamertag: 'MasterChief117', firstSeen: '2012-06-02T00:00:00Z',
          lastSeen: null, source: 'RenameDetected', note: null, canRemove: false },
      ],
    },
  };
}

/** How to answer one request: status, content type and body; undefined for an unknown path (404). */
// The historical import page: a few of the archive's sources (one gone for good), its latest jobs and a guarded import
// The Jobs page (/usermanagement/jobs): the recurring jobs on a worker with Discord on, one paused and one failing
const job = (name: string, description: string, intervalSeconds: number, extra: object = {}) => ({
  group: 'recurring', name, displayName: name, description, recurring: true, enabled: true, state: 'Scheduled', intervalSeconds, defaultIntervalSeconds: intervalSeconds,
  nextRunAt: null, previousRunAt: null, pausedReason: null, pausedBy: null, pausedAt: null, running: [], status: null, ...extra,
});
const jobStatus = (lastRunAt: string, runCount: number, extra: object = {}) => ({
  lastResult: 'Succeeded', lastRunAt, lastDurationMs: 42, lastSucceededAt: lastRunAt, lastFailedAt: null, lastFailureMessage: null,
  consecutiveFailures: 0, runCount, failureCount: 0, ...extra,
});
const jobsOverview = {
  nodes: [{ instanceID: 'grif-worker-7d9c5-1761760800123', lastCheckInAt: '2026-10-29T17:59:56Z', state: 'Alive' }],
  jobs: [
    job('historical-refresh', 'Refreshes the historical sources; an import is queued when one changed', 86400, {
      nextRunAt: '2026-10-30T06:00:00Z', previousRunAt: '2026-10-29T06:00:00Z', status: jobStatus('2026-10-29T06:00:00Z', 6, { lastDurationMs: 180 }),
    }),
    job('xbox-placeholders', 'Gives Xbox accounts no user holds a placeholder user', 3600, {
      state: 'Paused', pausedReason: 'Paused on the Jobs page', pausedBy: 'Commander Grif', pausedAt: '2026-10-29T15:12:00Z',
      previousRunAt: '2026-10-29T15:00:00Z', status: jobStatus('2026-10-29T15:00:00Z', 140, { lastDurationMs: 95 }),
    }),
    job('matchmaking-queue', 'Makes matches from the matchmaking queue and announces them', 10, {
      nextRunAt: '2026-10-29T18:00:04Z', previousRunAt: '2026-10-29T17:59:54Z', running: [{ fireInstanceID: 'f-81', startedAt: '2026-10-29T17:59:54Z', node: 'grif-worker-7d9c5-1761760800123' }],
      status: jobStatus('2026-10-29T17:59:44Z', 8211, { lastDurationMs: 37 }),
    }),
    job('season-events', 'Updates the season announcements in the Discord events channel', 10, {
      nextRunAt: '2026-10-29T18:00:02Z', previousRunAt: '2026-10-29T17:59:52Z',
      status: jobStatus('2026-10-29T17:59:52Z', 8204, { lastResult: 'Failed', lastFailedAt: '2026-10-29T17:59:52Z', lastSucceededAt: '2026-10-29T17:58:12Z',
        lastFailureMessage: 'Discord:EventsChannel is not set', consecutiveFailures: 10, failureCount: 12 }),
    }),
  ],
};
const jobHistory = [
  { firedAt: '2026-10-29T06:00:00Z', durationMs: 180, result: 'Succeeded', error: null, node: 'grif-worker-7d9c5-1761760800123', manual: false, trigger: 'historical-refresh' },
  { firedAt: '2026-10-28T21:14:07Z', durationMs: 166, result: 'Succeeded', error: null, node: 'grif-worker-7d9c5-1761760800123', manual: true, trigger: 'MT_8k2b1' },
  { firedAt: '2026-10-28T06:05:00Z', durationMs: 12, result: 'Succeeded', error: null, node: 'grif-worker-6b1f2-1761674400456', manual: false, trigger: 'historical-refresh-retry' },
  { firedAt: '2026-10-28T06:00:00Z', durationMs: 9, result: 'Succeeded', error: null, node: 'grif-worker-6b1f2-1761674400456', manual: false, trigger: 'historical-refresh' },
];

const historicalSources = [
  { sourceID: '2019-wl19-schedule-stats', category: 'AGLA-2019', year: 2019, title: 'WL19 Schedule & Stats', url: 'https://docs.google.com/spreadsheets/d/wl19/edit', kind: 'GoogleSheet',
    league: 'AGLA', gameVersion: 'halo3', format: 'LeagueSeason', enabled: true, isDead: false, imported: true, lastFetchedAt: '2026-10-29T06:00:10Z',
    lastFetchStatus: 'Unchanged', lastChangedAt: '2026-10-20T06:00:10Z', snapshotCount: 2, latestSnapshotSize: 1318215 },
  { sourceID: 'late-league-aspence-main', category: 'Late-League', year: null, title: 'ASpence Late League Main Sheet', url: 'https://docs.google.com/spreadsheets/d/ll/edit', kind: 'GoogleSheet',
    league: 'Late League', gameVersion: 'multi', format: 'LateLeague', enabled: true, isDead: false, imported: true, lastFetchedAt: '2026-10-29T06:01:00Z',
    lastFetchStatus: 'Changed', lastChangedAt: '2026-10-29T06:01:00Z', snapshotCount: 5, latestSnapshotSize: 3905590 },
  { sourceID: 'youtube-grifball-channel', category: 'VODs', year: null, title: 'Grifball YouTube Channel', url: 'https://www.youtube.com/channel/UCTVJapRl-GGwLCfioTb9N8w', kind: 'YouTubeChannel',
    league: null, gameVersion: null, format: null, enabled: false, isDead: false, imported: false, lastFetchedAt: '2026-10-28T06:02:00Z',
    lastFetchStatus: 'Failed: answered 429', lastChangedAt: '2026-10-01T06:02:00Z', snapshotCount: 1, latestSnapshotSize: 1279468 },
  { sourceID: '2018-fl18-schedule', category: 'AGLA-2018', year: 2018, title: 'FL18 Schedule DEAD', url: 'https://docs.google.com/spreadsheets/d/fl18/edit', kind: 'GoogleSheet',
    league: null, gameVersion: null, format: null, enabled: false, isDead: true, imported: false, lastFetchedAt: null, lastFetchStatus: null, lastChangedAt: null,
    snapshotCount: 0, latestSnapshotSize: null },
];
const historicalJobs = [
  { jobID: 41, kind: 'Import', trigger: 'Scheduled', status: 'Succeeded', stage: 'Done', createdAt: '2026-10-29T06:02:00Z', finishedAt: '2026-10-29T06:09:00Z', needsReview: 1 },
  { jobID: 40, kind: 'Refresh', trigger: 'Scheduled', status: 'Succeeded', stage: 'Done', createdAt: '2026-10-29T06:00:00Z', finishedAt: '2026-10-29T06:02:00Z', needsReview: 0 },
  { jobID: 39, kind: 'Preview', trigger: 'Manual', status: 'Failed', stage: 'Failed', createdAt: '2026-10-28T20:00:00Z', finishedAt: '2026-10-28T20:00:05Z', needsReview: 0 },
];
const historicalJob = {
  jobID: 41, kind: 'Import', trigger: 'Scheduled', guarded: true, status: 'Succeeded', stage: 'Done', seasonsDone: 12, seasonsTotal: 12,
  report: {
    seasonsCreated: 0, seasonsUpdated: 11, teamsCreated: 0, teamsRemoved: 0, statsRemoved: 2, championshipsRemoved: 0, statsInserted: 3, statsUpdated: 540,
    recordedGamesImported: 1210, recordedGamesRemoved: 0, championshipsInserted: 0, championshipsUpdated: 0, dummyUsersCreated: 4, waypointMatchesPulled: 0,
    skipped: [], warnings: ['late-league-aspence-main/S8Halo 3 Stats: stopped for review, it would remove 30 of 64 stat rows, 2 of 8 teams, 0 of 0 titles'], errors: [],
    seasonRemovals: [], sourcesChanged: [], sourcesFailed: [], sourcesUnchanged: 0, importJobID: null,
  },
  error: null, createdAt: '2026-10-29T06:02:00Z', startedAt: '2026-10-29T06:02:01Z', finishedAt: '2026-10-29T06:09:00Z',
  reviews: [
    { reviewID: 7, seasonKey: 'late-league-aspence-main/S8Halo 3 Stats', seasonName: 'Late League Season 8 (Halo 3)', status: 'NeedsReview', statsBefore: 64, statsRemoved: 30,
      teamsBefore: 8, teamsRemoved: 2, titlesBefore: 0, titlesRemoved: 0, approvalJobID: null, decidedAt: null },
  ],
};

// VODs of the league's channel: a match of season 12 (linked to its match), a season 12 cast, an archive season 5 final,
// and two the matcher wasn't sure of (one with a suggestion)
const matchVod = {
  vodID: 1, youTubeVideoID: 'Hb2JmxJhQd8', title: 'Autumn Brawl Week 8 | Red Rampage vs Blue Bombers', publishedAt: '2026-10-31T02:00:00Z', durationSeconds: 6601,
  seasonID: 12, seasonName: 'Autumn Brawl', seasonMatchID: 31, teams: [{ teamID: 2, teamName: 'Blue Bombers' }, { teamID: 1, teamName: 'Red Rampage' }],
};
const castVod = {
  vodID: 2, youTubeVideoID: 'JzI-tOK6tTY', title: 'Fireside Chat with the Commish and Season Awards', publishedAt: '2026-10-20T01:00:00Z', durationSeconds: 3782,
  seasonID: 12, seasonName: 'Autumn Brawl', seasonMatchID: null, teams: [],
};
const archiveVod = {
  vodID: 3, youTubeVideoID: 'CJgRG--cbX0', title: 'SL10 Grand Final - Relentless vs Old Guard', publishedAt: '2010-08-30T00:00:00Z', durationSeconds: 11625,
  seasonID: 5, seasonName: 'Summer League 2010', seasonMatchID: null, teams: [{ teamID: 41, teamName: 'Relentless' }],
};
const vodReview = [
  { vodID: 4, youTubeVideoID: 'Do32R4ltee8', title: 'AGLA Playoffs: Zero Logic (3) vs Spank\'s Life Coaches (6)', publishedAt: '2026-05-30T00:00:00Z', durationSeconds: 7049,
    seasonID: 11, seasonName: 'Summer Smash', seasonMatchID: null, teams: [], description: null, publishedAtIsApproximate: true, matchStatus: 'NeedsReview',
    matchNote: 'Zero Logic vs Spank\'s Life Coaches: not teams of Summer Smash' },
  { vodID: 5, youTubeVideoID: 'HhxZZ_MqDfA', title: 'PlayGrifball: Intramural Customs (Part 2)', publishedAt: '2026-06-30T00:00:00Z', durationSeconds: 11155,
    seasonID: null, seasonName: null, seasonMatchID: null, teams: [], description: null, publishedAtIsApproximate: false, matchStatus: 'NeedsReview',
    matchNote: 'No season or teams recognised' },
];

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
