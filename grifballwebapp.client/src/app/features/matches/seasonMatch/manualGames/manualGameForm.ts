import { DateTime } from 'luxon';
import type { GameVersionOptionDto, TeamPagePlayerDto } from '@api/models';
import { ManualGameRequestDto, ReportedGameDto, SeasonMatchGameParticipantDto, SeasonMatchPageDto } from '../seasonMatchPageDto';

/** Halo Infinite (seeded): what a game is entered as unless the commissioner picks another. */
export const INFINITE_GAME_VERSION_ID = 6;

/** One player's line in the form: a roster player (or one already entered), counted only when played is ticked. */
export interface PlayerLine {
  userID: number,
  gamertag: string,
  isHomeTeam: boolean,
  played: boolean,
  kills: number,
  deaths: number,
  assists: number,
  /** Grifball goals (bomb scores). */
  goals: number,
  score: number | null,
}

/**
 * Where a game goes in the series, as the commissioner picks it (never guessed): a new game, or in place of a game
 * already reported (its SeriesGameID), one this game corrects.
 */
export type GameSlot = 'new' | number;

/** A choice of {@link GameSlot}, as offered. */
export interface SlotChoice {
  slot: GameSlot,
  label: string,
}

/**
 * Where a game being reported or entered can go: a new game while the series is undecided and has room, and in place of
 * each game it has. A forfeit or BYE decided the match, not its games: nothing then (Undo Result first).
 */
export function slotChoices(match: SeasonMatchPageDto | null): SlotChoice[] {
  if (match === null || decidedOtherwise(match))
    return [];
  const games = match.reportedGames;
  const choices: SlotChoice[] = [];
  if (!match.decidedBy && games.length < match.bestOf)
    choices.push({ slot: 'new', label: `A new game (game ${games.length + 1})` });
  for (const game of games)
    choices.push({ slot: game.seriesGameID, label: `Replaces game ${game.matchNumber} (${gameSource(game)})` });
  return choices;
}

/** A forfeit or a BYE decided the match, not its games (the server's decidedBy): those are undone first (Undo Result). */
export function decidedOtherwise(match: SeasonMatchPageDto): boolean {
  return match.decidedBy === 'Forfeit' || match.decidedBy === 'Bye';
}

/** Where a game of the series came from, as the choices and the unreport confirmation say it. */
export function gameSource(game: ReportedGameDto): string {
  if (game.matchID)
    return 'from Waypoint';
  return game.isManual ? 'entered by hand' : 'recorded';
}

/** What the form holds; playedAt is the datetime-local input's value (local time), '' for none. */
export interface ManualGameForm {
  gameVersionID: number,
  map: string,
  gametype: string,
  playedAt: string,
  homeTeamScore: number | null,
  awayTeamScore: number | null,
  players: PlayerLine[],
  /** A new game's place in the series, as picked; null until picked (a series with no games has only one). */
  slot: GameSlot | null,
}

/** The game version a game is entered as unless the commissioner picks another: Halo Infinite, or the first offered without it. */
export function defaultGameVersion(versions: GameVersionOptionDto[]): number {
  return versions.some(v => v.gameVersionID === INFINITE_GAME_VERSION_ID) || versions.length === 0
    ? INFINITE_GAME_VERSION_ID
    : versions[0].gameVersionID;
}

/** A line per roster player, filled in from the game being edited; its players not on a roster (subs) too. */
export function formLines(game: ReportedGameDto | null, homeRoster: TeamPagePlayerDto[], awayRoster: TeamPagePlayerDto[]): PlayerLine[] {
  const line = (p: SeasonMatchGameParticipantDto, isHomeTeam: boolean): PlayerLine => ({
    userID: p.userID!, gamertag: p.gamertag, isHomeTeam, played: true,
    kills: p.kills, deaths: p.deaths, assists: p.assists, goals: p.bombScores, score: p.score,
  });
  // A game entered by hand has a user on every line
  const entered = new Map([
    ...(game?.homePlayers ?? []).filter(p => p.userID != null).map(p => [p.userID!, line(p, true)] as const),
    ...(game?.awayPlayers ?? []).filter(p => p.userID != null).map(p => [p.userID!, line(p, false)] as const),
  ]);
  const roster = (players: TeamPagePlayerDto[], isHomeTeam: boolean): PlayerLine[] => players.map(p => {
    const entry = entered.get(p.userID);
    entered.delete(p.userID);
    return entry
      ?? { userID: p.userID, gamertag: p.gamertag ?? 'Unknown Spartan', isHomeTeam, played: false, kills: 0, deaths: 0, assists: 0, goals: 0, score: null };
  });
  const lines = [...roster(homeRoster, true), ...roster(awayRoster, false)];
  for (const sub of entered.values())
    lines.push(sub);
  return lines;
}

/** A form with nothing entered. */
export function emptyForm(): ManualGameForm {
  return { gameVersionID: INFINITE_GAME_VERSION_ID, map: '', gametype: '', playedAt: '', homeTeamScore: null, awayTeamScore: null, players: [], slot: null };
}

/** The form as the API takes it: the ticked players only, the time in UTC. */
export function toRequest(form: ManualGameForm): ManualGameRequestDto {
  const playedAt = form.playedAt ? DateTime.fromISO(form.playedAt) : null;
  return {
    gameVersionID: form.gameVersionID,
    map: form.map.trim() || null,
    gametype: form.gametype.trim() || null,
    playedAt: playedAt?.isValid ? playedAt.toUTC().toISO() : null,
    homeTeamScore: form.homeTeamScore ?? 0,
    awayTeamScore: form.awayTeamScore ?? 0,
    replacesSeriesGameID: typeof form.slot === 'number' ? form.slot : null,
    players: form.players.filter(p => p.played).map(p => ({
      userID: p.userID,
      isHomeTeam: p.isHomeTeam,
      kills: p.kills ?? 0,
      deaths: p.deaths ?? 0,
      assists: p.assists ?? 0,
      goals: p.goals ?? 0,
      score: p.score,
    })),
  };
}

/** An API time as a datetime-local input's value (local time), '' for none. */
export function toLocalInput(iso: string | null): string {
  if (!iso)
    return '';
  const time = DateTime.fromISO(iso);
  return time.isValid ? time.toLocal().toFormat("yyyy-MM-dd'T'HH:mm") : '';
}
