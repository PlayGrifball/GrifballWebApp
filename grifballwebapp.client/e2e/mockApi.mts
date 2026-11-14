// A fake backend with realistic data, shaped like the API's DTOs. Used by the layout test
// (layout.spec.ts, through page.route) and by e2e/serve.mjs as the backend of the app's server
// (pages rendered on the server, Lighthouse CI), so both run without the .NET server. Plain TypeScript Node can run as is (no enums etc.).

import type { BrowserContext } from '@playwright/test';
import type { AnswerOf, Operation } from './apiOperations.mts';
import type {
  BracketsMatch, CommissionerDashboardDto, DayOfWeek, GamertagClaimDto, HistoricalImportJobDto, LetterDto, MatchDetailsDto, MatchDetailsMedalDto,
  MatchDetailsTeamDto, MatchmakingPlayerStatDto, MatchmakingSeasonDto, MetricLeaderDto, Outcomes, ParticpantResult, Participant, PossibleTeamDto,
  ProfileCommendationsDto, ProfileStatsDto, QuickSearchDto, QuickSearchPlayerDto, SeasonDto, SeasonInfoDto, SeasonListItemDto, SeasonPlayerStatDto,
  SeasonMatchPageDto, SeasonSummaryDto, Status, TeamDto, TeamHistoryDto, TeamHistorySeasonDto, TeamResponseDto, TeamsPlayerDto, TitleDto, UserResponseDto, ViewerDataDto,
} from '../src/generated/api/models';

export type Role = 'anon' | 'player' | 'admin';

/** A JSON body, or { text } for the endpoints that answer text/plain. null answers 204. */
// eslint-disable-next-line sonarjs/redundant-type-aliases -- names what a mock answers with, for whoever reads the handlers
export type MockBody = unknown;

/** How the mock gives that answer: nothing as null (204); text as { text }; JSON as itself, or null for no content. */
type MockAnswer<T> = [T] extends [void] ? null : [T] extends [string] ? { text: string } | null : T | null;

export type { Operation } from './apiOperations.mts';

/** A route of the mock: the API call it answers, and its answer. */
export interface MockRoute {
  operation: Operation;
  body: MockBody;
}

/**
 * A test's own JSON answer (page.route in a spec), checked as the mock's are: what `operation` answers in the generated
 * client. Returns it as it is.
 */
export function typed<K extends Operation>(operation: K, body: MockAnswer<AnswerOf<K>>): MockAnswer<AnswerOf<K>> {
  return body;
}

/** A route's answer, checked against what `operation` answers in the generated client: a mock can't drift from the API. */
function answer<K extends Operation>(operation: K, body: MockAnswer<AnswerOf<K>>): MockRoute {
  return { operation, body };
}

/** The fixed "now" the data is written around (a Thursday in week 3 of the Autumn Brawl season). */
export const MOCK_NOW = '2026-10-29T18:00:00Z';

/**
 * The session cookie (BrowserAuth.CookieName on the API, SESSION_COOKIE in src/app/core/server/pageCache.ts).
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
export async function signIn(context: Pick<BrowserContext, 'addCookies'>, role: Role): Promise<void> {
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

function roster(teamID: number): TeamDto {
  const [, name, captain] = teams[teamID - 1];
  const base = ['Plasma Pete', 'Banshee Bri', 'Warthog Wes', 'Needler Nan', 'Spartan Sam', 'Elite Eli', 'Grunt Gus', 'Jackal Jo'];
  return {
    teamName: name, season: seasonInfo(12, { hasEnded: false }), wins: 7 - teamID, losses: teamID - 1, tbd: 2, recordFromSource: false, titles: [],
    players: [
      { teamPlayerID: teamID * 10, userID: teamID * 10, gamertag: captain, draftCaptainOrder: teamID, draftRound: null, isCaptain: true, region: null },
      ...[0, 1, 2].map(i => ({ teamPlayerID: teamID * 10 + i + 1, userID: teamID * 10 + i + 1, gamertag: base[(teamID + i) % 8], draftCaptainOrder: null, draftRound: i + 1, isCaptain: false, region: null })),
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
const side = (id: number | null, position: number | null = null, extra: Partial<ParticpantResult> = {}): ParticpantResult => ({ id, position, forfeit: false, ...extra });
const participant = (id: number, name: string): Participant => ({ id, tournament_id: 12, name });
/** A bracket match: its status brackets-viewer's number, as the server sends it (Status, ViewerData.cs). */
const bracketMatch = (id: number, round: number, number: number, status: Status, opponent1: ParticpantResult | null, opponent2: ParticpantResult | null): BracketsMatch =>
  ({ id, stage_id: 1, group_id: 1, round_id: round, number, child_count: 0, status, opponent1, opponent2 });
const bracket: ViewerDataDto = {
  stages: [{ id: 1, tournament_id: 12, name: 'Autumn Brawl Playoffs', type: 'single_elimination', number: 1,
    settings: { size: 8, seedOrdering: ['inner_outer'], grandFinal: 'none', consolationFinal: false, balanceByes: false, groupCount: 1, manualOrdering: [],
      matchesChildCount: 0, roundRobinMode: 'simple', skipFirstRound: false } }],
  matches: [
    bracketMatch(101, 1, 1, 4, side(1, 1, { result: 'win' }), null),
    bracketMatch(102, 1, 2, 4, side(4, 4, { score: 1, result: 'loss' }), side(5, 5, { score: 2, result: 'win' })),
    bracketMatch(103, 1, 3, 4, side(2, 2, { score: 2, result: 'win' }), side(7, 7, { score: 0, result: 'loss' })),
    bracketMatch(104, 1, 4, 4, side(3, 3, { score: 0, result: 'loss', forfeit: true }), side(6, 6, { result: 'win' })),
    bracketMatch(105, 2, 1, 3, side(1, null, { score: 1 }), side(5, null, { score: 1 })),
    bracketMatch(106, 2, 2, 2, side(2), side(6)),
    bracketMatch(107, 3, 1, 0, side(null), side(null)),
  ],
  matchGames: [],
  participants: [...teams.map(([id, name]) => participant(id, name)), participant(7, 'Late Joiners')],
};

const days: DayOfWeek[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
const times = ['18:00:00', '19:00:00', '20:00:00', '21:00:00', '22:00:00'];
const timeslots = days.flatMap((dayOfWeek, d) => times.map((time, t) =>
  ({ optionID: d * times.length + t + 1, dayOfWeek, time, isChecked: (d * times.length + t + 1) % 3 === 0 })));

const players = ['MasterChief117', 'Plasma Pete', 'Banshee Bri', 'Warthog Wes', 'Needler Nan', 'Caboose42'];
const letters = ['S', 'A+', 'A', 'B+', 'B', 'C', 'D'];
const grades = (i: number, gamertag: string): LetterDto => {
  const L = (k: number) => letters[(i + k) % letters.length];
  return { xboxUserID: i, userID: null, gamertag, goals: L(0), kdSpread: L(1), punches: L(2), sprees: L(3), doubleKills: L(1), tripleKills: L(4),
    multiKills: L(5), xFactor: L(2), kills: L(0), gradeAvgMath: 3.2 - i * 0.2, gradeAvg: L(1) };
};

const possibleTeam = (teamID: number, score: number, outcome: Outcomes, offset: number): PossibleTeamDto => ({
  teamID, score, outcome,
  players: roster(teamID).players.map((p, i) => ({ xboxUserID: offset + i, gamertag: p.gamertag, score: 900 - offset * 20 - i * 80, kills: 14 - i, deaths: 6 + i, isOnTeam: true })),
});

// The match details page (/match/0f8f...): game 1 of season match 31, Red Rampage 5 - 3 Blue Bombers. A function:
// roster() reads values declared further down
const medal = (medalID: number, name: string, count: number): MatchDetailsMedalDto => ({ medalID, name, description: name, type: 'Mode', difficulty: 'Normal', count });
const matchDetailsTeam = (teamID: number, seasonTeamID: number, seasonTeamName: string, score: number, outcome: MatchDetailsTeamDto['outcome'], offset: number): MatchDetailsTeamDto => ({
  teamID, seasonTeamID, seasonTeamName, score, outcome,
  players: roster(seasonTeamID).players.map((p, i) => ({
    userID: p.userID, xboxUserID: 2535400000000000 + p.userID, gamertag: p.gamertag, goals: Math.max(0, score - i * 2), personalScore: 1400 - offset * 20 - i * 100, rank: i + 1 + (offset ? 4 : 0),
    kills: 14 - i, deaths: 6 + i, assists: 3 + i, kda: 9.5 - i, suicides: i % 2, betrayals: 0, meleeKills: 10 - i, powerWeaponKills: 3,
    maxKillingSpree: 5 - i, calloutAssists: i, shotsFired: 40, shotsHit: 22 - i, accuracy: 55 - i * 2.5, damageDealt: 2400 - i * 150,
    damageTaken: 1500 + i * 100, averageLife: '00:00:41.5000000', timePlayed: '00:09:58', spawns: 7 + i, objectivesCompleted: 0,
    joinedInProgress: false, leftInProgress: i === 3,
    medals: i === 0 ? [medal(1, 'Grand Slam', 2), medal(2, 'Pancake', 1)] : [medal(3, 'Double Kill', 1)],
  })),
});
const matchDetails = (): MatchDetailsDto => ({
  matchID: '0f8fad5b-d9cb-469f-a165-70867728950e', startTime: '2026-10-30T23:05:00Z', endTime: '2026-10-30T23:15:00Z', duration: '00:10:00',
  seasonMatch: { seasonMatchID: 31, seasonID: 12, seasonName: 'Autumn Brawl', gameNumber: 1, bestOf: 3 },
  teams: [matchDetailsTeam(0, 1, 'Red Rampage', 5, 'Won', 0), matchDetailsTeam(1, 2, 'Blue Bombers', 3, 'Lost', 10)],
  medals: [medal(3, 'Double Kill', 6), medal(1, 'Grand Slam', 4), medal(2, 'Pancake', 2)],
});

const users: UserResponseDto[] = ([['admin', 'Commander Grif', 'CommanderGrif', 'grif#0001'], ['chief', 'MasterChief117', 'MasterChief117', 'chief'], ['tucker', 'Tucker Tactics', null, 'tucker.t']] as [string, string, string | null, string][])
  .map(([userName, displayName, gamertag, discord], i) => ({ userID: i + 1, userName, lockoutEnd: null, lockoutEnabled: true, isDummyUser: false,
    accessFailedCount: 0, region: 'NA', regionID: 1, displayName, gamertag, discord, externalAuthCount: 1, hasPassword: true, roles: [] }));

// The team builder's board for the Winter Campaign draft (/season/13/draft): two captains with
// their first picks, the rest of the signups in the pool.
const draftPlayer = (personID: number, name: string, round: number | null = null): TeamsPlayerDto => ({ name, personID, pick: round, round, region: null });
const draftTeams: TeamResponseDto[] = [
  { teamID: 1, teamName: "Sarge Prime's Crew", captain: { name: 'Sarge Prime', personID: 201, order: 1, region: null }, players: [draftPlayer(301, 'Plasma Pete', 1)] },
  { teamID: 2, teamName: "Caboose42's Crew", captain: { name: 'Caboose42', personID: 202, order: 2, region: null }, players: [draftPlayer(302, 'Banshee Bri', 1)] },
];
const draftPool = ['Warthog Wes', 'Needler Nan', 'Spartan Sam', 'Elite Eli', 'Grunt Gus', 'Jackal Jo'].map((name, i) => draftPlayer(401 + i, name));

// The commissioner dashboard: a reschedule waiting for a decision, one whose match was reported since (Reject only),
// and a match past its time.
const commissionerDashboard: CommissionerDashboardDto = {
  pendingReschedules: [{ matchRescheduleID: 7, seasonMatchID: 32, homeCaptain: 'Hammer Bros', awayCaptain: 'Sword Swallowers',
    originalScheduledTime: '2026-10-29T01:00:00Z', newScheduledTime: '2026-10-31T01:00:00Z', reason: 'Half the team is at a LAN that night',
    requestedByGamertag: 'Grifmeister', requestedByUserID: 30, requestedAt: '2026-10-27T15:20:00Z', status: 'Pending', discordThreadID: null,
    canApprove: true, whyNotApprovable: null },
  { matchRescheduleID: 9, seasonMatchID: 34, homeCaptain: 'Red Rampage', awayCaptain: 'Blue Bombers',
    originalScheduledTime: '2026-10-28T01:00:00Z', newScheduledTime: '2026-11-01T01:00:00Z', reason: 'Two of us are travelling',
    requestedByGamertag: 'Sarge Prime', requestedByUserID: 10, requestedAt: '2026-10-27T18:00:00Z', status: 'Pending', discordThreadID: '1300000000000000009',
    canApprove: false, whyNotApprovable: 'This match already has a result.' }],
  overdueMatches: [{ seasonMatchID: 33, homeCaptain: 'Gravity Wells', awayCaptain: 'Sticky Situation', scheduledTime: '2026-10-27T00:30:00Z', hoursOverdue: 65 }],
  summary: { pendingRescheduleCount: 2, overdueMatchCount: 1, criticalOverdueCount: 1 },
};

// The Winter Campaign's signups close on 10 November, or (the server that renders the pages goes by
// the real clock, not MOCK_NOW) a month from now if that is later: open signups, with their Sign up
// buttons, whenever the tests run.
const signupsClose = new Date(Math.max(Date.parse('2026-11-10T23:59:00Z'), Date.now() + 30 * 24 * 3600 * 1000)).toISOString();

const seasonNames: Record<number, string> = { 11: 'Summer Smash', 12: 'Autumn Brawl', 13: 'Winter Campaign' };
/** A season's facts (SeasonInfoDto), as every DTO of or about a season has them: an AGLA Halo Infinite season of 2026 played in the app unless changed. */
const seasonInfo = (seasonID: number, changes: Partial<SeasonInfoDto> = {}): SeasonInfoDto => ({
  seasonID, seasonName: seasonNames[seasonID] ?? `Season ${seasonID}`, start: '2026-01-01T00:00:00Z', end: '2026-03-01T00:00:00Z', year: 2026,
  hasEnded: true, leagueID: 1, leagueName: 'AGLA', leagueSlug: 'agla', gameVersionID: 6, gameVersionName: 'Halo Infinite', gameVersionSlug: 'infinite',
  division: null, format: 'LeagueSeason', isHistorical: false, historicalSourceUrl: null, ...changes,
});

// The season editor's form (/seasonEdit/12), in the shape GetSeasons lists them
const season = (seasonID: number): SeasonDto => ({ seasonID, seasonName: seasonNames[seasonID], publicAt: null, signupsOpen: '2026-08-01T00:00:00Z',
  signupsClose: '2026-08-31T00:00:00Z', draftStart: '2026-09-02T00:00:00Z', seasonStart: '2026-09-07T00:00:00Z', seasonEnd: '2026-11-30T00:00:00Z', signupsCount: 24,
  gameVersionID: 6, leagueID: 1, division: null, format: 'LeagueSeason', copyFromSeasonID: null, copyAvailability: false, copySignups: false, copyTeams: false });

// A user as User Management edits and merges them (/usermanagement/edituser/2, /usermanagement/mergeuser/2/3)
const roleNames = ['Sysadmin', 'Commissioner', 'Player'];
const userDetail = (user: UserResponseDto): UserResponseDto => ({ ...user, roles: roleNames.map((roleName, i) => ({ roleName, hasRole: user.userID === 1 || i === 2 })) });

// A profile's most earned medals (/profile/117, /profile/999)
const commendations = (userID: number): ProfileCommendationsDto => ({
  userID, totalMedals: 1342, distinctMedals: 9,
  commendations: [
    { medalID: 1, medalName: 'Killing Spree', description: 'Kill 5 enemies without dying.', difficultyName: 'Normal', typeName: 'Spree', count: 412, matches: 120, tier: 4, tierFloor: 250, nextTierAt: 500 },
    { medalID: 3, medalName: 'Double Kill', description: 'Kill 2 enemies in quick succession.', difficultyName: 'Normal', typeName: 'Multikill', count: 64, matches: 41, tier: 2, tierFloor: 50, nextTierAt: 100 },
    { medalID: 5, medalName: 'Overkill', description: 'Kill 4 enemies in quick succession.', difficultyName: 'Mythic', typeName: 'Multikill', count: 3, matches: 3, tier: 0, tierFloor: 0, nextTierAt: 10 },
  ],
});

const profileStats = (userID: number, gamertag: string | null): ProfileStatsDto => ({
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
      gameVersionSlug: 'halo3',
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
      gameVersionSlug: 'halo3',
      seasonsCount: 1,
      gamesPlayed: 10,
      gamesDecidedByVote: 0,
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
    // Late League games in Halo Infinite (the profile's era tabs: All Eras, Halo 3, Halo Infinite)
    {
      gameVersionID: 6,
      gameVersionName: 'Halo Infinite',
      gameVersionSlug: 'infinite',
      seasonsCount: 0,
      gamesPlayed: 42,
      gamesDecidedByVote: 0,
      wins: 27,
      losses: 15,
      winPercent: 64.3,
      goals: 31,
      goalsPerGame: 0.74,
      kills: 412,
      deaths: 298,
      assists: 103,
      kdRatio: 1.38,
      kdSpread: 114,
      carrierKills: 69,
      stopsPerGame: 1.64,
      sprees: 18,
      multiKills: 40,
      exterminations: 2,
      betrayals: 1,
      hammerKills: 301,
      swordKills: 77,
      ballKills: 34,
      punches: 12,
      damageDealt: 251340,
      damageTaken: 236980,
      score: null,
    },
  ],
  overallCareer: {
    gameVersionID: null,
    gameVersionName: 'Career',
    gameVersionSlug: 'career',
    seasonsCount: 1,
    gamesPlayed: 10,
    gamesDecidedByVote: 0,
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
      userID,
      gamertag: gamertag ?? `User ${userID}`,
      season: seasonInfo(12, { leagueName: 'Grifball League', leagueSlug: 'gl', division: 'Pro' }),
      gameVersionID: 1,
      gameVersionName: 'Halo 3',
      gameVersionSlug: 'halo3',
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
      punches: null,
      swordKills: null,
      grabs: null,
      battingAverage: null,
      scoringAverage: null,
      combinedAverage: null,
      score: null,
      damageDealt: null,
      damageTaken: null,
    },
  ],
  // The Late League lines: the Discord queue now (Halo Infinite) and an archived season (Halo 3)
  lateLeague: [
    matchmakingStat(userID, gamertag ?? `User ${userID}`, 42, 27, 31, 412, 298, null),
    matchmakingStat(userID, gamertag ?? `User ${userID}`, 18, 10, 9, 150, 131, lateLeagueSeasons[0]),
  ],
  // The stat strip's scopes (ServiceRecordDto): league seasons, Late League, and both
  serviceRecord: {
    mmr: 1634,
    all: { gamesPlayed: 70, wins: 45, losses: 25, winPercent: 64.3, kills: 682, deaths: 489, kdRatio: 1.39, bombScores: 55, uncountedBombScoreGames: 2 },
    league: { gamesPlayed: 10, wins: 8, losses: 2, winPercent: 80, kills: 120, deaths: 60, kdRatio: 2.0, bombScores: 15, uncountedBombScoreGames: 0 },
    matchmaking: { gamesPlayed: 60, wins: 37, losses: 23, winPercent: 61.7, kills: 562, deaths: 429, kdRatio: 1.31, bombScores: 40, uncountedBombScoreGames: 2 },
  },
});

// Season match 31's games (ReportedGameDto). Game 1, from Waypoint: the match details page's game.
const gameLine = (userID: number, gamertag: string, kills: number, deaths: number, assists: number, bombScores: number, score: number | null) =>
  ({ userID, gamertag, kills, deaths, assists, bombScores, score, kdRatio: deaths > 0 ? Math.round(kills / deaths * 100) / 100 : null });
const waypointGame = {
  seriesGameID: 1, matchNumber: 1, matchID: '0f8fad5b-d9cb-469f-a165-70867728950e', recordedGameID: null, homeScore: 5, awayScore: 2,
  homePlayers: [gameLine(1, 'MasterChief117', 24, 12, 3, 3, 300), gameLine(2, 'Gravity Wells', 18, 15, 4, 2, 200)],
  awayPlayers: [gameLine(999, 'CommanderGrif', 16, 22, 1, 1, 100), gameLine(4, 'Blue Bomber', 11, 20, 2, 1, 100)],
  isManual: false, gameVersionID: null, gameVersionName: null, map: null, gametype: null, playedAt: null,
};
// Game 2, entered by hand (its stats missed by Halo Infinite): the rosters' players (roster()), with a game,
// map, gametype and time; it can be edited and unreported from the box score
const manualGame = {
  seriesGameID: 2, matchNumber: 2, matchID: null, recordedGameID: 7, homeScore: 3, awayScore: 5,
  homePlayers: [gameLine(10, 'Sarge Prime', 12, 14, 2, 1, null), gameLine(11, 'Banshee Bri', 9, 13, 5, 2, null)],
  awayPlayers: [gameLine(20, 'Tucker Tactics', 17, 9, 3, 3, null), gameLine(21, 'Warthog Wes', 10, 12, 4, 2, null)],
  isManual: true, gameVersionID: 6, gameVersionName: 'Halo Infinite', map: 'Bazaar', gametype: 'Grifball', playedAt: '2026-10-30T23:25:00Z',
};

/** A season played in the app, as GetSeasonSummary answers. */
const summary = (seasonID: number): SeasonSummaryDto => ({
  ...seasonInfo(seasonID, { hasEnded: seasonID === 11 }), teamCount: 6, signupCount: 10, matchCount: 12, bracketMatchCount: 7, playerStatCount: 0, champions: [],
});

// Season 5 was backfilled from the historical archive: teams, rosters, stats and a title, but no signups, schedule or matches
const HISTORICAL_SOURCE = 'https://docs.google.com/spreadsheets/d/e2e-summer-league-2010';
const historicalSeason = seasonInfo(5, { seasonName: 'Summer League 2010', start: '2010-01-01T00:00:00Z', end: '2010-12-31T00:00:00Z', year: 2010,
  gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', division: 'Pro', isHistorical: true, historicalSourceUrl: HISTORICAL_SOURCE });
const historicalTeams: [number, string, number, number][] = [[41, 'Relentless', 9, 1], [42, 'Benchwarmers', 5, 5], [43, 'Truthless and the Truthlessettes', 1, 9]];
const historicalStat = (id: number, gamertag: string, teamID: number, goals: number, isPlayoffs = false): SeasonPlayerStatDto => ({
  playerSeasonStatID: id, userID: 500 + id, gamertag, teamID, teamName: historicalTeams.find(t => t[0] === teamID)![1],
  tradeNote: null, roleNote: null, stintIndex: 1, isPlayoffs, isCombined: false, source: 'Manual', gamesPlayed: 10, wins: 6, losses: 4,
  winPercent: 60, goals, kills: 60 + goals, deaths: 40, assists: 5, kdRatio: 1.5, kdSpread: 20 + goals, carrierKills: 4, sprees: 2,
  multiKills: 3, exterminations: 0, punches: null, swordKills: null, grabs: null, battingAverage: null, scoringAverage: null, combinedAverage: null,
});
const historicalStats = [
  historicalStat(1, 'Legendary', 41, 14), historicalStat(2, 'Old Timer', 41, 9), historicalStat(3, 'Bench Boss', 42, 7),
  historicalStat(4, 'Truthless', 43, 3), historicalStat(5, 'Legendary', 41, 4, true),
];

/** A season in the season list (GET /api/seasons), played in the app unless changed. */
const seasonListItem = (seasonID: number, seasonName: string, start: string, end: string, changes: Partial<SeasonListItemDto> = {}): SeasonListItemDto => ({
  ...seasonInfo(seasonID, { seasonName, start: start + 'T00:00:00Z', end: end + 'T00:00:00Z', year: Number(start.slice(0, 4)) }),
  teamCount: 6, playerCount: 30, champions: [], teamNames: teams.map(t => t[1]), ...changes,
});
/** A title on the leaderboards: its season, tier and winning team. */
const title = (seasonID: number, seasonName: string, tier: string, teamID: number): TitleDto => ({ seasonID, seasonName, tier, teamID });
const leader = (rank: number, userID: number, gamertag: string, statValue: number, perGame: number | null): MetricLeaderDto =>
  ({ rank, userID, gamertag, statValue, gamesPlayed: 30, gamesDecidedByVote: 0, perGame, seasonsCount: 3, gameVersionName: null, secondaryStat: null });

// The seasons a long-time archive player played: a list long enough that, on one line, it once widened the claims
// table's Gamertag column until every other column was off to the side (#308)
const seasonRef = (seasonID: number, seasonName: string, year: number) => ({ seasonID, seasonName, year });
const veteranSeasons = [
  seasonRef(5, 'Summer League 2010', 2010), seasonRef(4, 'Spring League 2010', 2010), seasonRef(6, 'Winter League 2011 (DA)', 2011),
  seasonRef(7, 'Summer League 2012 (DA)', 2012), seasonRef(8, 'Winter League 2019 (Pro)', 2019), seasonRef(9, 'Summer League 2020 (Pro)', 2020),
  seasonRef(10, 'Winter League 2023 (DA)', 2023),
];

// Four pending gamertag claims on the backfilled season 5's players: a roster match with a team, a set gamertag
// without, an XUID lookup (who holds the gamertag now) and a user's claim of a past gamertag
const unclaimed = { claimantUserID: null, claimantName: null, firstSeen: null, lastSeen: null, rejectionReason: null };
const gamertagClaims: GamertagClaimDto[] = [
  { ...unclaimed, gamertagClaimID: 1, kind: 'RosterMatch', status: 'Pending', xboxUserID: 2535400000000117, xboxGamertag: 'MasterChief117', userID: 117,
    gamertag: 'Legendary', placeholderUserID: 501, seasonID: 5, seasonName: 'Summer League 2010', teamID: 41, teamName: 'Relentless',
    createdAt: '2026-10-27T15:20:00Z', currentGamertag: null, lookedUpAt: null, placeholderSeasons: [] },
  { ...unclaimed, gamertagClaimID: 2, kind: 'SetGamertag', status: 'Pending', xboxUserID: 2535400000000002, xboxGamertag: 'Old Timer', userID: 2,
    gamertag: 'Old Timer', placeholderUserID: 502, seasonID: 5, seasonName: 'Summer League 2010', teamID: null, teamName: null,
    createdAt: '2026-10-28T09:05:00Z', currentGamertag: null, lookedUpAt: null, placeholderSeasons: [] },
  { ...unclaimed, gamertagClaimID: 3, kind: 'XuidLookup', status: 'Pending', xboxUserID: 2535400000000503, xboxGamertag: null, userID: null,
    gamertag: 'Old School', placeholderUserID: 503, seasonID: 5, seasonName: 'Summer League 2010', teamID: null, teamName: null,
    createdAt: '2026-10-29T08:00:00Z', currentGamertag: 'ClutchKing22', lookedUpAt: '2026-10-29T09:00:00Z',
    placeholderSeasons: veteranSeasons },
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

// A franchise's history (/history/team/1): Red Rampage this season, champions of the Summer Smash, and in the archive
const franchiseSeason = (teamID: number, teamName: string, season: SeasonInfoDto, wins: number, losses: number, titles: string[], players: [number, string, boolean][],
  extra: Partial<TeamHistorySeasonDto> = {}): TeamHistorySeasonDto => ({
  teamID, teamName, season, wins, losses, titles, recordFromSource: false, ...extra,
  players: players.map(([userID, gamertag, isCaptain], i) => ({ teamPlayerID: teamID * 100 + i, userID, gamertag, draftCaptainOrder: isCaptain ? 1 : null, draftRound: isCaptain ? null : i, isCaptain, region: null })),
});
const teamHistory: TeamHistoryDto = {
  teamName: 'Red Rampage', leagueName: 'AGLA', appearances: 3, wins: 22, losses: 9, championships: 1, players: 9,
  seasons: [
    franchiseSeason(1, 'Red Rampage', seasonInfo(12, { hasEnded: false, start: '2026-09-07T00:00:00Z', end: '2026-11-30T00:00:00Z' }), 6, 0, [],
      roster(1).players.map(p => [p.userID, p.gamertag, p.draftCaptainOrder !== null] as [number, string, boolean])),
    franchiseSeason(21, 'Red Rampage', seasonInfo(11, { start: '2026-06-01T00:00:00Z', end: '2026-08-20T00:00:00Z' }), 9, 3, ['Open'],
      [[10, 'Sarge Prime', true], [117, 'MasterChief117', false], [12, 'Banshee Bri', false], [13, 'Warthog Wes', false]]),
    franchiseSeason(31, 'The Red Rampage', seasonInfo(4, { seasonName: 'Spring League 2010', start: '2010-03-01T00:00:00Z', end: '2010-06-30T00:00:00Z', year: 2010,
      gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', division: 'Pro', isHistorical: true, historicalSourceUrl: HISTORICAL_SOURCE }),
    7, 6, [], [], { recordFromSource: true }),
  ],
};

// Late League (/late-league, /late-league/3): the Discord queue's players now, and two archived seasons
const matchmakingSeason = (matchmakingSeasonID: number, name: string, startsAt: string, endsAt: string, gameCount: number, playerCount: number): MatchmakingSeasonDto => ({
  matchmakingSeasonID, name, gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', startsAt: startsAt + 'T00:00:00Z', endsAt: endsAt + 'T00:00:00Z',
  gameCount, playerCount, isHistorical: true, historicalSourceUrl: 'https://docs.google.com/spreadsheets/d/e2e-late-league-' + matchmakingSeasonID,
});
const lateLeagueSeasons = [matchmakingSeason(3, 'Late League Season 3', '2011-01-03', '2011-04-25', 212, 48), matchmakingSeason(2, 'Late League Season 2', '2010-08-02', '2010-12-13', 187, 41)];
const matchmakingStat = (userID: number, gamertag: string, gamesPlayed: number, wins: number, goals: number, kills: number, deaths: number, season: MatchmakingSeasonDto | null): MatchmakingPlayerStatDto => ({
  matchmakingSeasonID: season?.matchmakingSeasonID ?? null, matchmakingSeasonName: season?.name ?? null, userID, gamertag,
  gameVersionID: season ? 1 : 6, gameVersionName: season ? 'Halo 3' : 'Halo Infinite', gameVersionSlug: season ? 'halo3' : 'infinite',
  source: season ? 'Recorded' : 'Matches', gamesPlayed, gamesDecidedByVote: 0, wins, losses: gamesPlayed - wins, winPercent: Math.round(wins / gamesPlayed * 1000) / 10,
  goals, kills, deaths, assists: Math.round(kills / 4), kdRatio: Math.round(kills / deaths * 100) / 100, kdSpread: kills - deaths, carrierKills: Math.round(kills / 6),
});
const lateLeaguePlayers = (season: MatchmakingSeasonDto | null) => [
  matchmakingStat(117, 'MasterChief117', 42, 27, 31, 412, 298, season), matchmakingStat(501, 'Legendary', 38, 22, 25, 377, 301, season),
  matchmakingStat(12, 'Banshee Bri', 35, 16, 12, 289, 310, season), matchmakingStat(999, 'CommanderGrif', 29, 11, 8, 201, 260, season),
];

// Two players of one name, told apart by their labels (QuickSearchPlayerDto.Kind): the Xbox account that holds the
// gamertag now ("Current gamertag"), and the archive's player who used it (no Xbox account; "Archive · 2009–2011")
const oldTimers: QuickSearchPlayerDto[] = [
  { userID: 2, name: 'Old Timer', pastGamertag: null, matchedName: null, kind: 'Xbox', years: null },
  { userID: 502, name: 'Old Timer', pastGamertag: null, matchedName: null, kind: 'Archive', years: '2009–2011' },
];

// The quick search (Ctrl+K), whatever is typed (the query string is not matched): players (the two Old Timers; one
// found by a past gamertag, one by their Discord name), teams and seasons
const quickSearch: QuickSearchDto = {
  players: [
    ...oldTimers,
    { userID: 117, name: 'MasterChief117', pastGamertag: 'Legendary', matchedName: null, kind: null, years: null },
    { userID: 999, name: 'Commander Grif', pastGamertag: null, matchedName: 'grif#0001', kind: null, years: null },
  ],
  teams: [
    { teamID: 41, teamName: 'Relentless', seasonID: 5, seasonName: 'Summer League 2010' },
    { teamID: 1, teamName: 'Red Rampage', seasonID: 12, seasonName: 'Autumn Brawl' },
  ],
  seasons: [
    { seasonID: 5, seasonName: 'Summer League 2010', start: '2010-01-01T00:00:00Z', end: '2010-12-31T00:00:00Z', years: '2010' },
    { seasonID: 9, seasonName: 'Frostbite Cup', start: '2025-12-01T00:00:00Z', end: '2026-02-15T00:00:00Z', years: '2025–2026' },
  ],
};

// The Telemetry page (/usermanagement/telemetry): the last hour's metrics, a few log lines, and the traces (one open by ?trace=)
const minuteSeries = (name: string, base: number, wave: number) => ({
  name, points: Array.from({ length: 61 }, (_, i) => [Date.parse('2026-10-29T17:00:00Z') + i * 60_000, Math.round((base + wave * Math.sin(i / 6)) * 100) / 100]),
});
export const TRACE_ID = '4bf92f3577b34da6a3ce929d0e0e4736';
const telemetryMetrics = {
  start: '2026-10-29T17:00:00Z', end: '2026-10-29T18:00:00Z', stepSeconds: 60,
  panels: [
    { key: 'requests', title: 'Requests', unit: 'req/s', series: [minuteSeries('2xx', 4.2, 1.5), minuteSeries('4xx', 0.3, 0.2), minuteSeries('5xx', 0.02, 0.02)] },
    { key: 'latency', title: 'Response time', unit: 'ms', series: [minuteSeries('p95', 180, 60), minuteSeries('p50', 35, 10)] },
    { key: 'memory', title: 'Memory', unit: 'MB', series: [minuteSeries('working set', 412, 18)] },
    { key: 'jobs', title: 'Jobs failed', unit: 'runs', series: [] },
  ],
  routes: [
    { route: 'GET /api/seasons/{seasonID}/summary', requests: 4210, errors: 0, p95Ms: 42.5 },
    { route: 'GET /api/season-matches/{seasonMatchID}', requests: 1830, errors: 3, p95Ms: 210 },
    { route: 'POST /api/identity/login', requests: 96, errors: 0, p95Ms: 1240 },
    { route: 'GET /hub/TeamsHub', requests: 12, errors: 0, p95Ms: null },
  ],
};
const logLine = (timestamp: string, level: string, message: string, source: string, attributes: Record<string, string> = {}, traceId: string | null = null) => ({
  timestamp, level, message, traceId, spanId: traceId ? '00f067aa0ba902b7' : null, source, pod: 'grifballwebapp-7d9c8b6f5-x2k4q', attributes,
});
const telemetryLogs = {
  truncated: false,
  entries: [
    logLine('2026-10-29T17:58:12.512Z', 'error', 'Pulling the match history of 2535400000000117 failed: the Halo API answered 503', 'GrifballWebApp.Server.Services.MatchPullService',
      { RequestPath: '/api/season-matches/31/match-pull', StatusCode: '500', exception_type: 'System.Net.Http.HttpRequestException' }, TRACE_ID),
    logLine('2026-10-29T17:57:40.003Z', 'warn', 'The Discord bot was rate limited for 2.5 s', 'GrifballWebApp.Server.Discord.DiscordClient'),
    logLine('2026-10-29T17:55:00.120Z', 'info', 'Job recurring/historical-refresh succeeded in 42 ms', 'GrifballWebApp.Server.Jobs.JobRunner', { JobName: 'historical-refresh' }),
    logLine('2026-10-29T17:54:31.876Z', 'debug', 'Rendered /season/12 for a visitor', 'Microsoft.AspNetCore.Hosting.Diagnostics', { RequestPath: '/season/12', StatusCode: '200' }),
  ],
};
const span = (spanId: string, parentSpanId: string | null, name: string, kind: string, scope: string, offsetMs: number, durationMs: number, status: string,
  attributes: Record<string, string> = {}, extra: object = {}) => ({
  spanId, parentSpanId, name, kind, scope, service: 'grifballwebapp', pod: 'grifballwebapp-7d9c8b6f5-x2k4q', offsetMs, durationMs, status, statusMessage: null, attributes, events: [], ...extra,
});
const telemetryTrace = {
  traceId: TRACE_ID, start: '2026-10-29T17:58:11.300Z', durationMs: 1212.4,
  spans: [
    span('00f067aa0ba902b7', null, 'POST /api/season-matches/{seasonMatchID}/match-pull', 'Server', 'Microsoft.AspNetCore', 0, 1212.4, 'Error',
      { 'http.response.status_code': '500', 'url.path': '/api/season-matches/31/match-pull' }, { statusMessage: 'HttpRequestException',
        events: [{ name: 'exception', offsetMs: 1210.2, attributes: { 'exception.type': 'System.Net.Http.HttpRequestException', 'exception.message': 'Response status code does not indicate success: 503' } }] }),
    span('b7ad6b7169203331', '00f067aa0ba902b7', 'SELECT GrifballWebApp', 'Client', 'Microsoft.EntityFrameworkCore', 3.1, 8.4, 'Unset', { 'db.system': 'postgresql' }),
    span('c1f2a3b4c5d6e7f8', '00f067aa0ba902b7', 'GET halo-gateway', 'Client', 'System.Net.Http', 14.2, 1190.6, 'Error', { 'http.response.status_code': '503' }),
    span('d4e5f6a7b8c9d0e1', 'c1f2a3b4c5d6e7f8', 'retry 2', 'Internal', 'Polly', 610.5, 590.1, 'Error'),
  ],
};
const telemetryTraces = [
  { traceId: TRACE_ID, rootName: 'POST /api/season-matches/{seasonMatchID}/match-pull', rootService: 'grifballwebapp', start: '2026-10-29T17:58:11.300Z', durationMs: 1212.4,
    route: '/api/season-matches/{seasonMatchID}/match-pull', statusCode: 500 },
  { traceId: '6e0c63257de34c92bf9efcd03927272e', rootName: 'GET /api/seasons/{seasonID}/summary', rootService: 'grifballwebapp', start: '2026-10-29T17:57:02.118Z', durationMs: 18.9,
    route: '/api/seasons/{seasonID}/summary', statusCode: 200 },
  { traceId: '0af7651916cd43dd8448eb211c80319c', rootName: 'recurring/historical-refresh', rootService: 'grifballwebapp-worker', start: '2026-10-29T17:55:00.078Z', durationMs: 42,
    route: null, statusCode: null },
];

/** The mocked endpoints, by path (without the query string), and the few answered by their query too (path + "?..."). */
/** Match 31's page (SeasonMatchPageDto): a best of 3 at 1-1, game 1 reported from Waypoint, game 2 entered by hand by a
 * commissioner. */
function matchPage31(role: Role): SeasonMatchPageDto {
  return {
    seasonID: 12, seasonName: 'Autumn Brawl', isPlayoff: false,
    homeTeamName: 'Red Rampage', homeTeamID: 1, homeTeamScore: 1, homeTeamResult: 'TBD',
    awayTeamName: 'Blue Bombers', awayTeamID: 2, awayTeamScore: 1, awayTeamResult: 'TBD',
    // Undecided, its games for Undo Result to clear (SeasonMatch.DecidedBy, CanUndoResult)
    decidedBy: null, canUndo: true,
    scheduledTime: '2026-10-30T23:00:00Z', bestOf: 3,
    reportedGames: [waypointGame, manualGame],
    seriesTotals: {
      homePlayers: [...waypointGame.homePlayers, ...manualGame.homePlayers],
      awayPlayers: [...waypointGame.awayPlayers, ...manualGame.awayPlayers],
    },
    isImported: false,
    bracketInfo: null, activeRescheduleRequestId: null,
    // Signed in, the mock users play in (or run) this match: the server would take a reschedule request
    canRequestReschedule: role !== 'anon',
  };
}

export function mockRoutes(role: Role): Record<string, MockRoute> {
  return {
    // Who is signed in: a visitor gets 204 No Content
    '/api/identity/me': answer('me', role === 'anon' ? null : META[role]),
    '/api/identity/antiforgery-token': answer('antiforgery', { token: `e2e-xsrf-${role}` }),
    // The browser's telemetry batches (a browser not under automation, Lighthouse, may send them): taken
    '/api/client-telemetry': answer('reportClientTelemetry', null),
    '/api/CommitHash': answer('getCommitHash', { text: '9f3c2ab' }),
    '/api/CommitDate': answer('getCommitDate', { text: '2026-10-28T21:14:00Z' }),
    '/api/seasons/current': answer('getCurrentSeasonId', 12),
    '/api/events': answer('currentAndFutureEvents', [
              { seasonID: 13, name: 'Winter Campaign', start: '2026-10-20T00:00:00Z', end: signupsClose, eventType: 'Signup' },
              { seasonID: 13, name: 'Winter Campaign', start: '2026-11-14T19:00:00Z', end: '2026-11-14T23:00:00Z', eventType: 'Draft' },
              { seasonID: 12, name: 'Autumn Brawl', start: '2026-09-07T00:00:00Z', end: '2026-11-30T00:00:00Z', eventType: 'Season' },
              { seasonID: 13, name: 'Winter Campaign', start: '2026-11-20T00:00:00Z', end: '2027-02-15T00:00:00Z', eventType: 'Season' },
            ]),
    // The home page's matchmaking queue panel (its live updates come over hub/QueueHub, which hubMock.mts answers)
    '/api/queue': answer('getQueueStatus', { count: 6, open: true, signedIn: role !== 'anon', hasGamertag: role !== 'anon', inQueue: false, inMatch: false }),
    // The season list: the home page's past seasons and the seasons page (the backfilled season 5 among them)
    '/api/seasons': answer('getSeasonList', {
              totalCount: 7,
              results: [
                seasonListItem(12, 'Autumn Brawl', '2026-09-07', '2026-11-30', { hasEnded: false }),
                ...([['Summer Smash', '2026-06-01', '2026-08-20', 11], ['Spring Showdown', '2026-03-02', '2026-05-18', 10],
                  ['Frostbite Cup', '2025-12-01', '2026-02-15', 9], ['Harvest Hammer', '2025-09-01', '2025-11-20', 8], ['Season 7', '2025-06-02', '2025-08-18', 7]] as [string, string, string, number][])
                  .map(([name, start, end, seasonID]) => seasonListItem(seasonID, name, start, end)),
                seasonListItem(5, 'Summer League 2010', '2010-01-01', '2010-12-31', {
                  gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', division: 'Pro', isHistorical: true, historicalSourceUrl: HISTORICAL_SOURCE,
                  teamCount: 3, playerCount: 12, teamNames: historicalTeams.map(t => t[1]), champions: [{ tier: 'Pro', teamID: 41, teamName: 'Relentless', roster: [] }] }),
              ],
            }),
    '/api/leaderboards': answer('getLeaderboards', {
              ringLeaders: [{ rank: 1, userID: 501, gamertag: 'Legendary', ringsCount: 3, titles: [title(5, 'Summer League 2010', 'Pro', 41), title(11, 'Summer Smash', 'Open', 1), title(9, 'Frostbite Cup', 'Open', 2)] },
                { rank: 2, userID: 117, gamertag: 'MasterChief117', ringsCount: 1, titles: [title(12, 'Autumn Brawl', 'Open', 2)] }],
              dynasties: [{ rank: 1, teamName: 'Relentless', latestTeamID: 41, latestSeasonID: 5, championshipsCount: 2, seasonsActive: 3, titles: [title(5, 'Summer League 2010', 'Pro', 41), title(11, 'Summer Smash', 'Open', 1)] }],
              goalLeaders: [leader(1, 501, 'Legendary', 120, 4), leader(2, 117, 'MasterChief117', 88, 2.93)],
              killLeaders: [leader(1, 117, 'MasterChief117', 640, 21.33)],
              gamesPlayedLeaders: [leader(1, 501, 'Legendary', 30, null)],
              carrierStopLeaders: [leader(1, 501, 'Legendary', 45, 1.5)],
            }),
    // Format: Tournament alone: the tournaments' titles and games only
    '/api/leaderboards?formats=Tournament': answer('getLeaderboards', {
              ringLeaders: [{ rank: 1, userID: 117, gamertag: 'MasterChief117', ringsCount: 1, titles: [title(8, 'Harvest Hammer', 'Open', 3)] },
                { rank: 1, userID: 12, gamertag: 'Banshee Bri', ringsCount: 1, titles: [title(8, 'Harvest Hammer', 'Open', 3)] }],
              dynasties: [{ rank: 1, teamName: 'Hammer Bros', latestTeamID: 3, latestSeasonID: 12, championshipsCount: 1, seasonsActive: 2, titles: [title(8, 'Harvest Hammer', 'Open', 3)] }],
              goalLeaders: [leader(1, 12, 'Banshee Bri', 31, 3.1), leader(2, 117, 'MasterChief117', 19, 1.9)],
              killLeaders: [leader(1, 12, 'Banshee Bri', 140, 14)],
              gamesPlayedLeaders: [leader(1, 12, 'Banshee Bri', 10, null)],
              carrierStopLeaders: [leader(1, 117, 'MasterChief117', 12, 1.2)],
            }),
    '/api/leaderboards/player-rank': answer('getPlayerRank', {
              userID: 501,
              gamertag: 'Legendary',
              matchedAlias: null,
              pastGamertags: [],
              isDummyUser: true,
              rings: { rank: 1, totalRanked: 10, statValue: 3, secondaryText: null },
              goals: { rank: 1, totalRanked: 10, statValue: 120, secondaryText: '4.00 / G' },
              kills: { rank: 2, totalRanked: 10, statValue: 450, secondaryText: '1.80 K/D' },
              gamesPlayed: { rank: 1, totalRanked: 10, statValue: 30, secondaryText: '3 Seasons' },
              carrierStops: { rank: 1, totalRanked: 10, statValue: 45, secondaryText: '1.50 / G' },
            }),
    // The leaderboards' player suggestions, whatever is typed: the two Old Timers, and two more of each kind
    '/api/leaderboards/players': answer('searchPlayers', [
              ...oldTimers,
              { userID: 501, name: 'Legendary', pastGamertag: null, matchedName: null, kind: 'Archive', years: '2011–2012' },
              { userID: 117, name: 'MasterChief117', pastGamertag: null, matchedName: null, kind: 'Xbox', years: null },
            ]),
    ...Object.fromEntries(Object.entries(seasonNames).map(([id, name]) => [`/api/seasons/${id}/name`, answer('getSeasonName', { text: name })])),
    ...Object.fromEntries(Object.keys(seasonNames).map(id => [`/api/seasons/${id}/summary`, answer('getSeasonSummary', summary(Number(id)))])),
    '/api/seasons/5/summary': answer('getSeasonSummary', {
              ...historicalSeason, teamCount: 3, signupCount: 0,
              matchCount: 0, bracketMatchCount: 0, playerStatCount: historicalStats.length,
              champions: [{ tier: 'Pro', teamID: 41, teamName: 'Relentless', roster: [
                { userID: 501, gamertag: 'Legendary', isCaptain: true }, { userID: 502, gamertag: 'Old Timer', isCaptain: false }] }],
            }),
    // The season's stats, and on the team page (?teamID=41) its team's: matched on the path alone, so the same rows
    '/api/seasons/5/player-stats': answer('getSeasonPlayerStats', historicalStats),
    // Season 12 has no stored stats yet: its team pages show no stats table
    '/api/seasons/12/player-stats': answer('getSeasonPlayerStats', []),
    '/api/seasons/5/teams': answer('getSeasonTeams', [
              {
                teamID: 41, teamName: 'Relentless', wins: 9, losses: 1, tbd: 0, recordFromSource: true, titles: ['Pro'], draftCaptainOrder: null,
                players: [
                  { teamPlayerID: 4101, userID: 501, gamertag: 'Legendary', isCaptain: true, draftCaptainOrder: null, draftPick: null, draftRound: null, region: null },
                  { teamPlayerID: 4102, userID: 502, gamertag: 'Old Timer', isCaptain: false, draftCaptainOrder: null, draftPick: null, draftRound: null, region: null },
                ],
              },
            ]),
    '/api/seasons/12/teams': answer('getSeasonTeams', teams.map(([teamID, name], i) => ({
              teamID, teamName: name, wins: 7 - i - (i > 2 ? 1 : 0), losses: i + 1, tbd: 0, recordFromSource: false, titles: [], draftCaptainOrder: i + 1,
              players: [
                { teamPlayerID: teamID * 10 + 1, userID: teamID * 100 + 1, gamertag: `${name} Captain`, isCaptain: true, draftCaptainOrder: i + 1, draftPick: null, draftRound: null, region: null },
              ],
            }))),
    '/api/seasons/5/standings': answer('getTeamStandings', historicalTeams.map(([teamID, teamName, wins, losses]) => ({ teamID, teamName, wins, losses, recordFromSource: true }))),
    '/api/seasons/5/matches/scheduled': answer('getScheduledMatches', []),
    '/api/seasons/5/matches/unscheduled': answer('getUnscheduledMatches', []),
    '/api/teams/41': answer('team', {
              teamName: 'Relentless', season: historicalSeason, recordFromSource: true,
              wins: 9, losses: 1, tbd: 0, titles: ['Pro'],
              players: [
                { teamPlayerID: 4101, userID: 501, gamertag: 'Legendary', draftCaptainOrder: null, draftRound: null, isCaptain: true, region: null },
                { teamPlayerID: 4102, userID: 502, gamertag: 'Old Timer', draftCaptainOrder: null, draftRound: null, isCaptain: false, region: null },
              ],
            }),
    '/api/teams/41/matches': answer('matches', []),
    '/api/seasons/12/standings': answer('getTeamStandings', teams.map(([teamID, name], i) => ({ teamID, teamName: name, wins: 7 - i - (i > 2 ? 1 : 0), losses: i + 1, recordFromSource: false }))),
    '/api/seasons/12/matches/scheduled': answer('getScheduledMatches', scheduled),
    '/api/seasons/12/matches/unscheduled': answer('getUnscheduledMatches', [{ seasonMatchID: 51, homeCaptain: 'Gravity Wells', awayCaptain: 'Red Rampage', homeTeam: 'Gravity Wells', awayTeam: 'Red Rampage', complete: false }]),
    // The season's playoff bracket, for any season
    ...Object.fromEntries(Object.keys(seasonNames).map(id => [`/api/seasons/${id}/bracket/viewer-data`, answer('getViewerData', bracket)])),
    // A best of 3 at 1-1 (matchPage31); match 35, the same series once its games decided it, for its Match report and Undo
    '/api/season-matches/31': answer('getSeasonMatchPage', matchPage31(role)),
    '/api/season-matches/35': answer('getSeasonMatchPage', { ...matchPage31(role), homeTeamScore: 2, homeTeamResult: 'Won', awayTeamResult: 'Loss', decidedBy: 'Games', canRequestReschedule: false }),
    '/api/season-matches/35/possible-matches': answer('getPossibleMatches', []),
    '/api/season-matches/35/match-pull': answer('getMatchPullStatus', { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null }),
    '/api/season-matches/35/vods': answer('getSeasonMatchVods', []),
    '/api/season-matches/31/possible-matches': answer('getPossibleMatches', [{ matchID: '7c9e6679-7425-40de-944b-e07fc1f90ae7', homeTeam: possibleTeam(1, 5, 'Won', 0), awayTeam: possibleTeam(2, 3, 'Lost', 10) }]),
    '/api/season-matches/31/match-pull': answer('getMatchPullStatus', { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: { firedAt: '2026-10-30T18:00:00Z', durationMs: 42000, result: 'Succeeded', error: null } }),
    '/api/matches/0f8fad5b-d9cb-469f-a165-70867728950e': answer('getMatchDetails', matchDetails()),
    ...Object.fromEntries(teams.map(([id]) => [`/api/teams/${id}`, answer('team', roster(id))])),
    '/api/teams/1/matches': answer('matches', [
              { seasonMatchID: 21, scheduledTime: '2026-10-21T23:00:00Z', bestOf: 3, score: 2, result: 'Won', otherTeamID: 3, otherTeamName: 'Hammer Bros', otherScore: 1, otherResult: 'Loss' },
              { seasonMatchID: 31, scheduledTime: '2026-10-30T23:00:00Z', bestOf: 3, score: null, result: null, otherTeamID: 2, otherTeamName: 'Blue Bombers', otherScore: null, otherResult: null },
            ]),
    '/api/users/117/gamertag': answer('getGamertag', { gamertag: 'MasterChief117' }),
    '/api/users/999/gamertag': answer('getGamertag', { gamertag: 'CommanderGrif' }),
    '/api/users/117/stats': answer('getProfileStats', profileStats(117, 'MasterChief117')),
    '/api/users/999/stats': answer('getProfileStats', profileStats(999, 'CommanderGrif')),
    // The profile's deployment history: each team the player was on
    '/api/users/117/deployment-history': answer('getDeploymentHistory', [
              { season: seasonInfo(12, { leagueName: 'Grifball League', leagueSlug: 'gl', division: 'Pro' }), teamID: 2, teamName: 'Blue Bombers', isCaptain: true, wins: 5, losses: 1, recordFromSource: false, finish: 'Champion', championshipTiers: ['Pro'] },
              { season: historicalSeason, teamID: 41, teamName: 'Relentless', isCaptain: false, wins: 4, losses: 4, recordFromSource: true, finish: 'Playoffs', championshipTiers: [] },
            ]),
    '/api/users/999/deployment-history': answer('getDeploymentHistory', []),
    '/api/users/117/commendations': answer('getCommendations', commendations(117)),
    '/api/users/999/commendations': answer('getCommendations', commendations(999)),
    '/api/me/external-logins': answer('getMyExternalLogins', { hasPassword: true, logins: [{ loginProvider: 'Discord', providerKey: '81234567890123456', providerDisplayName: 'Discord' }], providers: [{ name: 'Discord', displayName: 'Discord' }] }),
    '/api/seasons/13/signups/mine': answer('getSignup', null),
    '/api/seasons/13/signups/mine/timeslots': answer('getTimeslots', timeslots),
    '/api/seasons/12/signups': answer('getSignups', players.map((personName, i) => ({ seasonID: 12, userID: i, personName, timestamp: `2026-10-2${i}T18:0${i}:00Z`,
              teamName: i % 3 === 0 ? `${personName}'s Crew` : null, willCaptain: i % 3 === 0, requiresAssistanceDrafting: i === 3, region: null, timeslots: [] }))),
    '/api/seasons/12/grades': answer('getGrades', { totals: [], perMinutes: [], letters: players.slice(0, 5).map((p, i) => grades(i, p)) }),
    '/api/seasons/settings': answer('getSeasons', [12, 13, 11].map(season)),
    '/api/seasons/12/settings': answer('getSeason', season(12)),
    '/api/game-versions': answer('getGameVersions', [{ gameVersionID: 1, name: 'Halo 3' }, { gameVersionID: 6, name: 'Halo Infinite' }]),
    '/api/leagues': answer('getLeagues', [{ leagueID: 1, name: 'AGLA' }, { leagueID: 2, name: 'Late League' }]),
    // The times each season offers for signups: weeknights at 7 and 8
    ...Object.fromEntries(Object.keys(seasonNames).map(id => [`/api/seasons/${id}/availability`, answer('getSeasonAvailability', (['Monday', 'Wednesday', 'Thursday'] as DayOfWeek[]).flatMap(dayOfWeek => ['19:00:00', '20:00:00'].map(time => ({ dayOfWeek, time }))))])),
    '/api/halo/check': answer('checkStatus', { text: 'Signed in to Halo Infinite (token valid for 41 minutes)' }),
    // The Infinite Client page in the default Gateway mode: the gateway's in-cluster address, plain HTTP as the chart's is
    // eslint-disable-next-line sonarjs/no-clear-text-protocols -- the Halo gateway's in-cluster address: plain HTTP inside the cluster
    '/api/halo/status': answer('haloStatus', { mode: 'Gateway', gatewayUrl: 'http://halo-gateway.halo-gateway.svc:8080/', gatewayAdminUrl: 'https://grunt.grifball.xyz/admin', apiKeyConfigured: true, reachable: true, accountStatus: 'Healthy', apiCode: 200, healthy: true, message: 'Halo gateway account Healthy; test call: Halo Infinite API answered 200' }), // NOSONAR(S5332): mock data, an in-cluster address
    '/api/sheets/default': answer('defaultSheetInfo', [{ name: 'Autumn Brawl results', spreadsheetID: '1xGrifballAutumnBrawl2026', sheetName: 'Week 3' }]),
    '/api/users': answer('getUsers', { totalCount: users.length, results: users }),
    // A user's password reset link (POST, the user management page's RESET PASSWORD)
    '/api/password-reset-links': answer('generatePasswordResetLink', { resetLink: '/reset-password?token=e2e-reset-token', expiresAt: '2026-10-30T18:00:00Z' }),
    ...Object.fromEntries(users.map(user => [`/api/users/${user.userID}`, answer('getUser', userDetail(user))])),
    // The accounts linked to a user, on their edit page
    ...Object.fromEntries(users.map(user => [`/api/users/${user.userID}/external-logins`, answer('getUserExternalLogins', { hasPassword: user.hasPassword,
      logins: [{ loginProvider: 'Discord', providerKey: '8123456789012345' + user.userID, providerDisplayName: 'Discord' }], providers: [{ name: 'Discord', displayName: 'Discord' }] })])),
    '/api/seasons/13/draft/teams': answer('getTeams', draftTeams),
    '/api/seasons/13/draft/player-pool': answer('getPlayerPool', draftPool),
    '/api/seasons/13/draft/captains/locked': answer('areCaptainsLocked', true),
    '/api/commissioner-dashboard': answer('getDashboardData', commissionerDashboard),
    // The historical import page (/usermanagement/historicalimport): its sources, jobs, schedule, and a guarded import's job
    '/api/historical-sources': answer('getHistoricalSources', historicalSources),
    '/api/historical-imports': answer('getHistoricalImportJobs', historicalJobs),
    '/api/historical-imports/schedule': answer('getHistoricalImportSchedule', { paused: false, interval: '1.00:00:00', lastRunAt: '2026-10-29T06:00:00Z', nextRunAt: '2026-10-30T06:00:00Z', lastJobID: 40, lastRunNote: null }),
    '/api/historical-imports/41': answer('getHistoricalImportJob', historicalJob),
    // The Jobs page (/usermanagement/jobs) and each job's runs (?take= is not matched)
    '/api/jobs': answer('getJobs', jobsOverview),
    ...Object.fromEntries(jobsOverview.jobs.map(j => [`/api/jobs/${j.group}/${j.name}/runs`, answer('getJobHistory', jobHistory)])),
    // VODs: the match page's, the seasons', the teams' and the profiles' (through their teams), and the review list
    '/api/season-matches/31/vods': answer('getSeasonMatchVods', [matchVod]),
    '/api/seasons/12/vods': answer('getSeasonVods', [matchVod, castVod]),
    '/api/seasons/5/vods': answer('getSeasonVods', [archiveVod]),
    '/api/teams/1/vods': answer('getTeamVods', [matchVod]),
    '/api/teams/41/vods': answer('getTeamVods', [archiveVod]),
    '/api/users/117/vods': answer('getPlayerVods', [matchVod, archiveVod]),
    '/api/users/999/vods': answer('getPlayerVods', []),
    '/api/vods/review': answer('getVodReview', vodReview),
    // The gamertag claims page (/usermanagement/claims): four pending claims (?status=Pending, or no status), and its
    // Approved and Rejected tabs' decided ones
    '/api/gamertag-claims': answer('getGamertagClaims', gamertagClaims),
    '/api/gamertag-claims?status=Approved': answer('getGamertagClaims', [
              { ...gamertagClaims[0], gamertagClaimID: 11, status: 'Approved', createdAt: '2026-10-20T15:20:00Z' },
              { ...gamertagClaims[2], gamertagClaimID: 13, status: 'Approved', createdAt: '2026-10-21T08:00:00Z' },
            ]),
    '/api/gamertag-claims?status=Rejected': answer('getGamertagClaims', [
              { ...gamertagClaims[3], gamertagClaimID: 14, status: 'Rejected', rejectionReason: 'That was someone else: the 2009 roster lists Timer Old on Benchwarmers' },
            ]),
    // Its "Find possible duplicates": an archive placeholder that looks like a user, and two placeholders alike
    '/api/duplicate-suggestions': answer('getDuplicateSuggestions', [
              { sourceUserID: 503, sourceGamertag: 'Old School', targetUserID: 2, targetGamertag: 'OldSchool #1234', targetIsPlaceholder: false, match: 'Framing', distance: 0,
                sourceSeasons: [{ seasonID: 5, seasonName: 'Summer League 2010', year: 2010 }], targetSeasons: [{ seasonID: 12, seasonName: 'Autumn Brawl', year: 2026 }] },
              { sourceUserID: 504, sourceGamertag: 'Timer Old', targetUserID: 502, targetGamertag: 'Old Timer', targetIsPlaceholder: true, match: 'Token', distance: 2,
                sourceSeasons: [{ seasonID: 4, seasonName: 'Spring League 2010', year: 2010 }], targetSeasons: [{ seasonID: 5, seasonName: 'Summer League 2010', year: 2010 }] },
            ]),
    // A profile's "Claim a past gamertag", whatever is typed (?gamertag= is not matched): the archive's player of that name
    '/api/me/gamertag-claims/preview': answer('previewGamertagClaim', { gamertag: 'Old Timer', placeholderUserID: 502, placeholderName: 'Old Timer',
              seasons: [{ seasonID: 4, seasonName: 'Spring League 2010', year: 2010 }, { seasonID: 5, seasonName: 'Summer League 2010', year: 2010 }], refusal: null }),
    // The schedule's recommended times (organizers, editing the schedule): the last run's, not applied yet, one with a conflict
    '/api/seasons/12/schedule-recommendation': answer('getScheduleRecommendation', {
              run: { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: { firedAt: '2026-10-29T17:00:00Z', durationMs: 5400, result: 'Succeeded', error: null } },
              latest: {
                scheduleRecommendationID: 3, createdAt: '2026-10-29T17:00:05Z', requestedBy: 'Commander Grif', fitness: 0.92, generations: 400, appliedAt: null, appliedBy: null,
                matches: [
                  { seasonMatchID: 31, homeTeam: 'Red Rampage', awayTeam: 'Blue Bombers', scheduledTime: '2026-10-30T23:00:00Z', currentTime: '2026-10-30T23:00:00Z',
                    overlaps: false, homeTeamAvailable: true, awayTeamAvailable: true, decided: false },
                  { seasonMatchID: 41, homeTeam: 'Red Rampage', awayTeam: 'Sword Swallowers', scheduledTime: '2026-11-04T23:00:00Z', currentTime: '2026-11-05T00:00:00Z',
                    overlaps: false, homeTeamAvailable: true, awayTeamAvailable: false, decided: false },
                  { seasonMatchID: 51, homeTeam: 'Gravity Wells', awayTeam: 'Red Rampage', scheduledTime: '2026-11-06T00:00:00Z', currentTime: null,
                    overlaps: false, homeTeamAvailable: true, awayTeamAvailable: true, decided: false },
                  { seasonMatchID: 33, homeTeam: 'Gravity Wells', awayTeam: 'Sticky Situation', scheduledTime: '2026-10-27T00:30:00Z', currentTime: '2026-10-27T00:30:00Z',
                    overlaps: false, homeTeamAvailable: true, awayTeamAvailable: true, decided: true },
                ],
              },
            }),
    // Linking a VOD from the review list (the historical import page): the seasons, and the Summer Smash's teams and matches
    '/api/vods/link-seasons': answer('getVodLinkSeasons', [12, 11, 5].map(seasonID => ({ seasonID, seasonName: seasonID === 5 ? 'Summer League 2010' : seasonNames[seasonID] }))),
    '/api/seasons/11/vod-link-options': answer('getVodLinkOptions', {
              teams: teams.slice(0, 4).map(([teamID, teamName]) => ({ teamID: teamID + 20, teamName })),
              matches: [{ seasonMatchID: 201, label: 'Week 1: Red Rampage vs Blue Bombers', teamIDs: [21, 22] }, { seasonMatchID: 202, label: 'Week 1: Hammer Bros vs Sword Swallowers', teamIDs: [23, 24] }],
            }),
    '/api/user-merges': answer('getUserMerges', { totalCount: userMerges.length, results: userMerges }),
    // The signed-in admin's own profile (/profile/999): their claims of past gamertags
    '/api/me/gamertag-claims': answer('getMyGamertagClaims', [{ ...gamertagClaims[3], claimantUserID: 999, userID: 999, claimantName: 'Commander Grif', status: 'Rejected', rejectionReason: 'That was someone else' }]),
    '/api/xuid-lookups/status': answer('getXuidLookupStatus', { placeholders: 12, due: 3, found: 7, notFound: 1, failed: 1, lastLookedUpAt: '2026-10-29T09:00:00Z' }),
    // A franchise's seasons, from its team of this season (the team page's "Franchise history" link)
    '/api/teams/1/history': answer('history', teamHistory),
    '/api/late-league/queue': answer('getLateLeagueQueue', { gameCount: 96, voteDecidedGameCount: 0, players: lateLeaguePlayers(null) }),
    '/api/late-league/seasons': answer('getLateLeagueSeasons', lateLeagueSeasons),
    '/api/late-league/seasons/3': answer('getLateLeagueSeason', { season: lateLeagueSeasons[0], players: lateLeaguePlayers(lateLeagueSeasons[0]) }),
    '/api/search': answer('quickSearch', quickSearch),
    // The database backup page (/databasebackup): a SQLite backup made, and the last export done
    '/api/backups/info': answer('backupInfo', { provider: 'PostgreSQL', latestMigration: '20261028211400_MatchmakingSeasons', formats: ['sqlite', 'postgres', 'sqlserver', 'dump'] }),
    '/api/backups/sqlite/status': answer('backupDownloadStatus', { state: 'Succeeded', format: 'sqlite', started: '2026-10-29T17:40:00Z', finished: '2026-10-29T17:41:12Z',
              fileName: 'GrifballWebApp-2026-10-29.sqlite', tables: 64, rows: 182340, message: null, size: 41_943_040, runningFormat: null, progress: null, progressMessage: null }),
    '/api/database-exports/status': answer('backupExportStatus', { state: 'Succeeded', provider: 'SqlServer', started: '2026-10-28T09:00:00Z', finished: '2026-10-28T09:03:10Z', tables: 64, rows: 181902, message: null, progress: null, progressMessage: null }),
    // The Telemetry page (/usermanagement/telemetry): every backend set up
    '/api/telemetry/status': answer('getTelemetryStatus', { serviceName: 'grifballwebapp', logs: true, metrics: true, traces: true }),
    '/api/telemetry/metrics': answer('getTelemetryMetrics', telemetryMetrics),
    '/api/telemetry/logs': answer('getTelemetryLogs', telemetryLogs),
    '/api/telemetry/traces': answer('getTelemetryTraces', telemetryTraces),
    [`/api/telemetry/traces/${TRACE_ID}`]: answer('getTelemetryTrace', telemetryTrace),
    // The past gamertags page (/usermanagement/pastgamertags?xboxUserID=...): one manually-added row, one detected from a rename
    '/api/past-gamertags': answer('getPastGamertags', {
              xboxUserID: 2535400000000117, currentGamertag: 'MasterChief117', userID: 117,
              pastGamertags: [
                { xboxGamertagHistoryID: 1, xboxUserID: 2535400000000117, gamertag: 'Legendary', firstSeen: '2010-01-01T00:00:00Z',
                  lastSeen: '2012-06-01T00:00:00Z', source: 'Manual', note: 'From the Summer League 2010 roster', canRemove: true },
                { xboxGamertagHistoryID: 2, xboxUserID: 2535400000000117, gamertag: 'MasterChief117', firstSeen: '2012-06-02T00:00:00Z',
                  lastSeen: null, source: 'RenameDetected', note: null, canRemove: false },
              ],
            }),
  };
}

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
const historicalJob: HistoricalImportJobDto = {
  jobID: 41, kind: 'Import', trigger: 'Scheduled', guarded: true, status: 'Succeeded', stage: 'Done', seasonsDone: 12, seasonsTotal: 12,
  report: {
    seasonsCreated: 0, seasonsUpdated: 11, teamsCreated: 0, teamsRemoved: 0, statsRemoved: 2, championshipsRemoved: 0, statsInserted: 3, statsUpdated: 540,
    recordedGamesImported: 1210, recordedGamesRemoved: 0, championshipsInserted: 0, championshipsUpdated: 0, dummyUsersCreated: 4, waypointMatchesPulled: 0,
    skipped: [], warnings: ['late-league-aspence-main/S8Halo 3 Stats: stopped for review, it would remove 30 of 64 stat rows, 2 of 8 teams, 0 of 0 titles'], errors: [],
    seasonRemovals: [], sourcesChanged: [], sourcesFailed: [], sourcesUnchanged: 0, importJobID: null, discordClaimsCreated: 0, discordLinked: 0,
    discordMerged: 0, gamesLinked: 0, manualReplacedByWaypoint: 0, matchmakingSeasonsCreated: 0, matchmakingSeasonsUpdated: 0, missingMatches: [],
    recordedGamesSkippedAsWaypoint: 0, regionsSet: 0, seasonMatchesCreated: 0, seasonMatchesRemoved: 0, seasonsRetired: 0, suggestionsCreated: 0,
  },
  error: null, createdAt: '2026-10-29T06:02:00Z', startedAt: '2026-10-29T06:02:01Z', finishedAt: '2026-10-29T06:09:00Z',
  reviews: [
    { reviewID: 7, seasonID: null, seasonKey: 'late-league-aspence-main/S8Halo 3 Stats', seasonName: 'Late League Season 8 (Halo 3)', status: 'NeedsReview', statsBefore: 64, statsRemoved: 30,
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

/**
 * How to answer one request: status, content type and body; undefined for an unknown path (404). `query`: the request's
 * query string ("?status=Approved"); an endpoint mocked for that exact query answers it, any other query its path's.
 */
export function mockResponse(role: Role, path: string, query = ''): { status: number, contentType: string, body: string } | undefined {
  const routes = mockRoutes(role);
  const key = query && path + query in routes ? path + query : path;
  if (!(key in routes))
    return undefined;
  const { body } = routes[key];
  if (body === null)
    return { status: 204, contentType: 'application/json', body: '' };
  if (typeof body === 'object' && body !== null && 'text' in body && Object.keys(body).length === 1)
    return { status: 200, contentType: 'text/plain', body: String((body as { text: string }).text) };
  return { status: 200, contentType: 'application/json', body: JSON.stringify(body) };
}
