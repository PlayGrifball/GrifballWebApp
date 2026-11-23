/** A season's signups, draft or regular season, as /api/events lists them. */
export interface SeasonEvent {
  seasonID: number,
  name: string,
  start: string,
  end: string,
  eventType: 'Signup' | 'Draft' | 'Season',
  /** A past season backfilled from the historical archive: its source spreadsheet (/api/seasons). */
  historicalSourceUrl?: string | null,
}

export const eventTitles: Record<SeasonEvent['eventType'], string> = {
  Signup: 'Signups',
  Draft: 'Draft',
  Season: 'Regular season',
};

/** Whether the event has started and not yet ended at `now` (ms since the epoch): from its start, included, to its end, not
 * included, as the server tells a season's phase (Season.PhaseAt). */
export function isUnderWay(event: SeasonEvent, now: number): boolean {
  return Date.parse(event.start) <= now && now < Date.parse(event.end);
}

/** Whether this season's signups are open at `now`: its Signup event has started and not ended. */
export function signupsOpen(events: SeasonEvent[], seasonID: number, now: number): boolean {
  return events.some(e => e.eventType === 'Signup' && e.seasonID === seasonID && isUnderWay(e, now));
}

export type SeasonPhaseState = 'done' | 'active' | 'upcoming';

export interface SeasonPhaseStep {
  eventType: SeasonEvent['eventType'];
  title: string;
  start: string;
  end: string;
  state: SeasonPhaseState;
}

const PHASE_ORDER: readonly SeasonEvent['eventType'][] = ['Signup', 'Draft', 'Season'];

/** Ordered lifecycle steps for `seasonID` from `/api/events`, marked active/done/upcoming at `now`. */
export function seasonPhaseSteps(events: SeasonEvent[], seasonID: number, now: number): SeasonPhaseStep[] {
  const byType = new Map<SeasonEvent['eventType'], SeasonEvent>();
  for (const event of events) {
    if (event.seasonID === seasonID && !byType.has(event.eventType))
      byType.set(event.eventType, event);
  }
  return PHASE_ORDER
    .map(type => byType.get(type))
    .filter((event): event is SeasonEvent => event != null)
    .map(event => ({
      eventType: event.eventType,
      title: eventTitles[event.eventType],
      start: event.start,
      end: event.end,
      state: phaseState(event, now),
    }));
}

function phaseState(event: SeasonEvent, now: number): SeasonPhaseState {
  if (isUnderWay(event, now))
    return 'active';
  return Date.parse(event.end) <= now ? 'done' : 'upcoming';
}
