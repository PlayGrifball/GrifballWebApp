import { isUnderWay, SeasonEvent, signupsOpen } from './seasonEvents';

describe('seasonEvents', () => {
  const now = Date.parse('2026-03-10T12:00:00Z');
  const ev = (eventType: SeasonEvent['eventType'], start: string, end: string, seasonID = 4): SeasonEvent =>
    ({ seasonID, name: 'Spring', start, end, eventType });

  it('is under way from its start to its end, both included', () => {
    expect(isUnderWay(ev('Season', '2026-03-10T12:00:00Z', '2026-03-11T00:00:00Z'), now)).toBeTrue();
    expect(isUnderWay(ev('Season', '2026-03-01T00:00:00Z', '2026-03-10T12:00:00Z'), now)).toBeTrue();
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
});
