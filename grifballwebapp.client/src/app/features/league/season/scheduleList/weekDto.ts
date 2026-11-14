import { DateTime } from "luxon";
import { ScheduledMatchDto } from "./scheduledMatchDto";

export interface WeekDto {
  weekNumber: number,
  games: WeekGameDto[]
}

export interface WeekGameDto {
  seasonMatchID: number,
  homeTeamName: string,
  awayTeamName: string,
  complete: boolean,
  time: DateTime
}

/**
 * The scheduled matches by the week they're played in (the viewer's local week), numbered 1, 2, … in order. Weeks sort
 * by year then week number, so they stay chronological across a year boundary (week 52 of 2026 before week 1 of 2027);
 * each week's matches keep the order they came in.
 */
export function scheduleWeeks(matches: ScheduledMatchDto[]): WeekDto[] {
  const weeks = new Map<string, { year: number, week: number, games: WeekGameDto[] }>();
  for (const match of matches) {
    const year = match.time.localWeekYear;
    const week = match.time.localWeekNumber;
    const key = `${year}-${week}`;
    if (!weeks.has(key))
      weeks.set(key, { year, week, games: [] });
    weeks.get(key)!.games.push({
      seasonMatchID: match.seasonMatchID,
      homeTeamName: match.homeTeam,
      awayTeamName: match.awayTeam,
      complete: match.complete,
      time: match.time,
    });
  }
  return [...weeks.values()]
    .sort((a, b) => a.year - b.year || a.week - b.week)
    .map((week, index) => ({ weekNumber: index + 1, games: week.games }));
}
