/** A season's signups, draft or regular season, as /api/Home/CurrentAndFutureEvents/ lists them. */
export interface SeasonEvent {
  seasonID: number,
  name: string,
  start: string,
  end: string,
  eventType: 'Signup' | 'Draft' | 'Season'
}

export const eventTitles: Record<SeasonEvent['eventType'], string> = {
  Signup: 'Signups',
  Draft: 'Draft',
  Season: 'Regular season',
};

/** Whether the event has started and not yet ended at `now` (ms since the epoch). */
export function isUnderWay(event: SeasonEvent, now: number): boolean {
  return Date.parse(event.start) <= now && now <= Date.parse(event.end);
}
