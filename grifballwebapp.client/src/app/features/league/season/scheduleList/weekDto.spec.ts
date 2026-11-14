import { DateTime } from 'luxon';
import { ScheduledMatchDto } from './scheduledMatchDto';
import { scheduleWeeks } from './weekDto';

describe('scheduleWeeks', () => {
  const match = (seasonMatchID: number, iso: string): ScheduledMatchDto => ({
    seasonMatchID, homeCaptain: 'H', awayCaptain: 'A', homeTeam: `Home${seasonMatchID}`, awayTeam: `Away${seasonMatchID}`,
    complete: false, time: DateTime.fromISO(iso),
  });

  it('numbers the weeks in order, across a year boundary, each keeping its matches\' order', () => {
    // A week of 2027 sorts after 2026's last one, though its match comes first
    const weeks = scheduleWeeks([
      match(3, '2027-01-06T12:00:00'),
      match(1, '2026-12-23T12:00:00'),
      match(2, '2026-12-22T12:00:00'),
    ]);
    expect(weeks.map(w => w.weekNumber)).toEqual([1, 2]);
    expect(weeks.map(w => w.games.map(g => g.seasonMatchID))).toEqual([[1, 2], [3]]);
    expect(weeks[0].games[0]).toEqual(jasmine.objectContaining({ homeTeamName: 'Home1', awayTeamName: 'Away1', complete: false }));
  });

  it('has no weeks without matches', () => {
    expect(scheduleWeeks([])).toEqual([]);
  });
});
