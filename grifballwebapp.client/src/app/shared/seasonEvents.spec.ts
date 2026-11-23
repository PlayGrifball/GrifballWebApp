import { isUnderWay, SeasonEvent, seasonPhaseSteps, signupsOpen } from './seasonEvents';

describe('seasonEvents', () => {
  const now = Date.parse('2026-03-10T12:00:00Z');
  const ev = (eventType: SeasonEvent['eventType'], start: string, end: string, seasonID = 4): SeasonEvent =>
    ({ seasonID, name: 'Spring', start, end, eventType });

  it('is under way from its start, included, to its end, not included', () => {
    expect(isUnderWay(ev('Season', '2026-03-10T12:00:00Z', '2026-03-11T00:00:00Z'), now)).toBeTrue();
    expect(isUnderWay(ev('Season', '2026-03-01T00:00:00Z', '2026-03-10T12:00:00Z'), now)).toBeFalse();
    expect(isUnderWay(ev('Season', '2026-03-11T00:00:00Z', '2026-03-12T00:00:00Z'), now)).toBeFalse();
  });

  describe('signupsOpen', () => {
    it('is true while the season\'s signup window is open', () => {
      expect(signupsOpen([ev('Signup', '2026-03-01T00:00:00Z', '2026-03-20T00:00:00Z')], 4, now)).toBeTrue();
    });

    it('is false before the window opens', () => {
      expect(signupsOpen([ev('Signup', '2026-03-11T00:00:00Z', '2026-03-20T00:00:00Z')], 4, now)).toBeFalse();
    });

    it('is false after the window closed', () => {
      expect(signupsOpen([ev('Signup', '2026-03-01T00:00:00Z', '2026-03-09T00:00:00Z')], 4, now)).toBeFalse();
    });

    it('is false without a window for that season', () => {
      expect(signupsOpen([], 4, now)).toBeFalse();
      expect(signupsOpen([ev('Signup', '2026-03-01T00:00:00Z', '2026-03-20T00:00:00Z', 5)], 4, now)).toBeFalse();
      expect(signupsOpen([ev('Draft', '2026-03-01T00:00:00Z', '2026-03-20T00:00:00Z')], 4, now)).toBeFalse();
    });
  });

  describe('seasonPhaseSteps', () => {
    it('orders Signup, Draft and Season for the matching season and marks done, active and upcoming', () => {
      const steps = seasonPhaseSteps([
        ev('Season', '2026-03-15T00:00:00Z', '2026-05-01T00:00:00Z'),
        ev('Signup', '2026-03-01T00:00:00Z', '2026-03-10T00:00:00Z'),
        ev('Draft', '2026-03-10T00:00:00Z', '2026-03-12T00:00:00Z'),
        ev('Signup', '2026-03-01T00:00:00Z', '2026-03-20T00:00:00Z', 9),
      ], 4, now);

      expect(steps.map(s => [s.eventType, s.state])).toEqual([
        ['Signup', 'done'],
        ['Draft', 'active'],
        ['Season', 'upcoming'],
      ]);
    });
  });
});
