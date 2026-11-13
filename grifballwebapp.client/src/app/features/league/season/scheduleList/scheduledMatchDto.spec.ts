import { parseScheduledMatch } from './scheduledMatchDto';

// What the server's DateTimeJsonConverter writes: UTC with a Z.
const UTC = '2026-01-02T03:04:00Z';

describe('parseScheduledMatch', () => {
  it('parseScheduledMatch converts its time', () => {
    expect(parseScheduledMatch({ seasonMatchID: 1, time: UTC }).time.toMillis()).toBe(Date.parse(UTC));
  });
});
