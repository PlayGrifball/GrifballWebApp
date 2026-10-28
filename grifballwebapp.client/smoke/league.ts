import { Api, register, type SeasonDto, type TimeslotDto, type UserResponseDto } from './api';
import { smokeRunId, type Credentials } from './env';

// Test data, made through the app's own API as the smoke user: a season of this run's own,
// with its own throwaway players. No direct database access and no fixed IDs, so a run works on
// any preview and never touches the data the preview was seeded with.
//
// Everything is named smoke-<runid>-<random>-... (short: season, team and display names are limited
// to 30 characters, and a team named after its captain is "<user name>'s Team"). The API can't delete seasons, users, signups or
// brackets, so cleanup is limited to closing the season (all its dates moved to 2001), which keeps
// it out of the home page's open signups and current/upcoming events. Leftovers are harmless:
// a closed season and players with random passwords nobody knows.

export const DAYS = ['Monday', 'Tuesday', 'Wednesday'];
export const TIMES = ['19:00:00', '20:00:00', '21:00:00', '22:00:00'];

/** Far-future season dates: never the current season, and easy to recognise in a date picker. */
export const FUTURE = {
  draftStart: '2099-01-05T18:00:00Z',
  seasonStart: '2099-01-10T18:00:00Z',
  seasonEnd: '2099-03-01T18:00:00Z',
};

export interface Player extends Credentials {
  userID: number;
  /** What the app shows for them (no gamertag, Discord or display name: the user name). */
  name: string;
}

export interface League {
  seasonID: number;
  /** <runid>-<random>, in every name this league uses. */
  tag: string;
  name: string;
  players: Player[];
  /** The first `captains` players, in pick order, each captain of a team named by teamName(). */
  captains: Player[];
  close(): Promise<void>;
}

export const teamName = (league: { tag: string }, captainIndex: number) => `smoke ${league.tag} T${captainIndex + 1}`;

/** <runid>-<random>: unique even when a retry builds the same thing again. At most 12 characters. */
export function smokeTag(): string {
  return `${smokeRunId()}-${Math.random().toString(36).slice(2, 5)}`;
}

function randomPassword(): string {
  // Meets the default ASP.NET Identity rules: upper, lower, digit, symbol, 6+ characters.
  return `Sm0ke!${Math.random().toString(36).slice(2)}${Math.random().toString(36).slice(2).toUpperCase()}`;
}

/**
 * A season with signups open now and its playing dates in 2099, availability Mon-Wed 19:00-22:00 UTC,
 * `players` throwaway players signed up for it, and the first `captains` of them made captains.
 */
export async function createLeague(baseURL: string, admin: Api, label: string,
  opts: { players?: number; captains?: number } = {}): Promise<League> {
  const tag = smokeTag();
  const name = `smoke-${tag}-${label.slice(0, 7)}`;
  const now = Date.now();
  const day = 24 * 3600 * 1000;
  const season: SeasonDto = {
    seasonID: 0,
    seasonName: name,
    signupsOpen: new Date(now - day).toISOString(),
    signupsClose: new Date(now + 7 * day).toISOString(),
    ...FUTURE,
  };
  const seasonID = await admin.post<number>('EventOrganizer/UpsertSeason', season);
  await admin.post('Availability/UpdateSeasonAvailability', {
    seasonID,
    timeslots: DAYS.flatMap(dayOfWeek => TIMES.map(time => ({ dayOfWeek, time }))),
  });

  const count = opts.players ?? 0;
  const captainCount = opts.captains ?? 0;
  const players: Player[] = [];
  // A few at a time: each is several requests, and the preview is small.
  for (let i = 0; i < count; i += 4) {
    const batch = Array.from({ length: Math.min(4, count - i) }, (_, j) => i + j);
    players.push(...await Promise.all(batch.map(n => createPlayer(baseURL, admin, `smoke-${tag}-p${n + 1}`, seasonID,
      n < captainCount ? teamName({ tag }, n) : null))));
  }

  const captains = players.slice(0, captainCount);
  for (const [i, captain] of captains.entries()) {
    await admin.post('Teams/AddCaptain', { seasonID, personID: captain.userID, orderNumber: i + 1 });
  }

  return {
    seasonID,
    tag,
    name,
    players,
    captains,
    close: () => closeSeason(admin, { ...season, seasonID }),
  };
}

/** Registers a user, gives them the Player role and signs them up for the season. */
async function createPlayer(baseURL: string, admin: Api, username: string, seasonID: number,
  captainOf: string | null): Promise<Player> {
  const { credentials, user, api } = await newUser(baseURL, admin, username);
  try {
    await admin.post('UserManagement/EditUser', {
      ...user,
      roles: user.roles.map(r => ({ ...r, hasRole: r.hasRole || r.roleName === 'Player' })),
    });
    await api.relogin(); // Roles are in the token.
    const timeslots = await api.get<TimeslotDto[]>(`Signups/GetTimeslots/${seasonID}?offset=0`);
    await api.post('Signups/UpsertSignup', {
      seasonID,
      teamName: captainOf,
      willCaptain: captainOf !== null,
      requiresAssistanceDrafting: false,
      timeslots: timeslots.map((t, i) => ({ ...t, isChecked: i % 3 === 0 })),
    });
  } finally {
    await api.dispose();
  }
  return { ...credentials, userID: user.userID, name: username };
}

/** A registered user with a permanent lockout (LockoutEnd = DateTimeOffset.MaxValue). */
export async function createLockedOutUser(baseURL: string, admin: Api): Promise<UserResponseDto> {
  const { user, api } = await newUser(baseURL, admin, `smoke-${smokeTag()}-lock`);
  await api.dispose();
  await admin.post('UserManagement/EditUser', { ...user, lockoutEnd: '9999-12-31T23:59:59.9999999+00:00' });
  return admin.get<UserResponseDto>(`UserManagement/GetUser/${user.userID}`);
}

/**
 * Registers a user and signs in as them, which gives their ID (no lookup by name: the user search
 * isn't needed, and fails on PostgreSQL).
 */
async function newUser(baseURL: string, admin: Api, username: string):
  Promise<{ credentials: Credentials; user: UserResponseDto; api: Api }> {
  const credentials = { username, password: randomPassword() };
  await register(baseURL, credentials);
  const api = await Api.login(baseURL, credentials);
  const user = await admin.get<UserResponseDto>(`UserManagement/GetUser/${api.userID}`);
  return { credentials, user, api };
}

/** Best effort: moves every date of the season to 2001 so it no longer shows as open or upcoming. */
async function closeSeason(admin: Api, season: SeasonDto): Promise<void> {
  try {
    await admin.relogin(); // A long run outlives the 15-minute token.
    await admin.post('EventOrganizer/UpsertSeason', {
      ...season,
      signupsOpen: '2001-01-01T00:00:00Z',
      signupsClose: '2001-01-02T00:00:00Z',
      draftStart: '2001-01-03T00:00:00Z',
      seasonStart: '2001-01-04T00:00:00Z',
      seasonEnd: '2001-01-05T00:00:00Z',
    });
  } catch (e) {
    console.warn(`Could not close smoke season ${season.seasonName} (${season.seasonID}): ${e}`);
  }
}
