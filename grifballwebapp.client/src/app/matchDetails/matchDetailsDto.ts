import { DateTime } from 'luxon';
import { fromApiDate } from '../shared/apiDates';

/** One Halo Infinite match with every stat pulled for it (MatchDetails/GetMatchDetails). */
export interface MatchDetailsDto {
  matchID: string,
  startTime: DateTime | null,
  endTime: DateTime | null,
  /** The match's length, in seconds. */
  duration: number,
  /** The season match this game was reported for; null for a game not reported. */
  seasonMatch: MatchDetailsSeasonMatchDto | null,
  /** Infinite's teams, in team ID order. */
  teams: MatchDetailsTeamDto[],
  /** Every medal earned in the match, most earned first. */
  medals: MatchDetailsMedalDto[],
}

export interface MatchDetailsSeasonMatchDto {
  seasonMatchID: number,
  seasonID: number,
  seasonName: string,
  gameNumber: number,
  bestOf: number,
}

export interface MatchDetailsTeamDto {
  /** Infinite's team ID: 0 or 1. */
  teamID: number,
  /** The season match's team most of these players are on, when the game was reported. */
  seasonTeamID: number | null,
  seasonTeamName: string | null,
  score: number,
  outcome: 'Tie' | 'Won' | 'Lost' | 'DidNotFinish', // Outcomes enum, sent as its name
  players: MatchDetailsPlayerDto[],
}

export interface MatchDetailsPlayerDto {
  userID: number | null,
  gamertag: string,
  /** Infinite's mode score: Grifball goals. */
  score: number,
  personalScore: number,
  rank: number,
  kills: number,
  deaths: number,
  assists: number,
  kda: number,
  suicides: number,
  betrayals: number,
  meleeKills: number,
  powerWeaponKills: number,
  maxKillingSpree: number,
  calloutAssists: number,
  shotsFired: number,
  shotsHit: number,
  accuracy: number,
  damageDealt: number,
  damageTaken: number,
  /** In seconds. */
  averageLife: number,
  /** In seconds. */
  timePlayed: number,
  spawns: number,
  objectivesCompleted: number,
  joinedInProgress: boolean,
  leftInProgress: boolean,
  medals: MatchDetailsMedalDto[],
}

export interface MatchDetailsMedalDto {
  medalID: number,
  name: string,
  description: string,
  type: string,
  difficulty: string,
  count: number,
}

/**
 * A .NET TimeSpan as System.Text.Json writes it ("[-][d.]hh:mm:ss[.fffffff]"), in seconds; 0 for
 * anything else.
 */
export function fromApiTimeSpan(value: string | null | undefined): number {
  const match = /^(-)?(?:(\d+)\.)?(\d+):(\d+):(\d+(?:\.\d+)?)$/.exec(value ?? '');
  if (!match)
    return 0;
  const [, sign, days, hours, minutes, seconds] = match;
  const total = Number(days ?? 0) * 86400 + Number(hours) * 3600 + Number(minutes) * 60 + Number(seconds);
  return sign ? -total : total;
}

/** Seconds as m:ss (h:mm:ss from an hour up). */
export function formatSeconds(totalSeconds: number): string {
  const whole = Math.max(0, Math.round(totalSeconds));
  const hours = Math.floor(whole / 3600);
  const minutes = Math.floor((whole % 3600) / 60);
  const seconds = String(whole % 60).padStart(2, '0');
  return hours > 0 ? `${hours}:${String(minutes).padStart(2, '0')}:${seconds}` : `${minutes}:${seconds}`;
}

/** The match as the API sends it (ISO dates, TimeSpan strings), with DateTimes and seconds; null stays null. */
export function parseMatchDetails(raw: any): MatchDetailsDto | null {
  if (raw == null)
    return null;
  return {
    ...raw,
    startTime: fromApiDate(raw.startTime),
    endTime: fromApiDate(raw.endTime),
    duration: fromApiTimeSpan(raw.duration),
    teams: (raw.teams ?? []).map((team: any) => ({
      ...team,
      players: (team.players ?? []).map((player: any) => ({
        ...player,
        averageLife: fromApiTimeSpan(player.averageLife),
        timePlayed: fromApiTimeSpan(player.timePlayed),
      })),
    })),
    medals: raw.medals ?? [],
  };
}
