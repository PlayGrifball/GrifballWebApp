import { DatePipe } from '@angular/common';
import { DateTime } from 'luxon';
import { fromApiDate } from './apiDates';
import { ToDatePipe } from './toDate.pipe';
import { parseSeason } from '../api/dtos/seasonDto';
import { parseUser } from '../userManagement/userResponseDto';
import { parseScheduledMatch } from '../season/scheduleList/scheduledMatchDto';
import { parseSeasonMatchPage } from '../season/seasonMatch/seasonMatchPageDto';

// What the server's DateTimeJsonConverter writes: UTC with a Z. DateTimeOffset keeps its offset.
const UTC = '2026-01-02T03:04:00Z';
const OFFSET = '2026-01-02T03:04:00+00:00';

describe('API dates', () => {
  const datePipe = new DatePipe('en-US');

  it('fromApiDate reads the server format as the same instant', () => {
    const value = fromApiDate(UTC);
    expect(value instanceof DateTime).toBeTrue();
    expect(value.isValid).toBeTrue();
    expect(value.toMillis()).toBe(Date.parse(UTC));
    expect(fromApiDate(OFFSET)!.toMillis()).toBe(Date.parse(UTC));
  });

  it('fromApiDate keeps a missing date missing', () => {
    expect(fromApiDate(null)).toBeNull();
    expect(fromApiDate(undefined)).toBeNull();
    expect(fromApiDate('')).toBeNull();
  });

  it('round-trips: a DateTime sent back serializes to the same instant', () => {
    const sent = JSON.parse(JSON.stringify({ time: fromApiDate(UTC) })).time as string;
    expect(Date.parse(sent)).toBe(Date.parse(UTC));
  });

  it('toDate + date pipe shows exactly what the date pipe showed for the ISO string', () => {
    const toDate = new ToDatePipe();
    expect(datePipe.transform(toDate.transform(fromApiDate(UTC)), 'short')).toBe(datePipe.transform(UTC, 'short'));
    expect(toDate.transform(null)).toBeNull();
    expect(toDate.transform(undefined)).toBeNull();
  });

  it('parseSeason converts every season date', () => {
    const season = parseSeason({ seasonID: 4, seasonName: 'S', signupsOpen: UTC, signupsClose: UTC, draftStart: UTC, seasonStart: UTC, seasonEnd: UTC, signupsCount: 2 });
    for (const field of ['signupsOpen', 'signupsClose', 'draftStart', 'seasonStart', 'seasonEnd'] as const) {
      expect(season[field] instanceof DateTime).withContext(field).toBeTrue();
      expect(season[field].toMillis()).withContext(field).toBe(Date.parse(UTC));
    }
    expect(season.seasonName).toBe('S');
    expect(season.signupsCount).toBe(2);
  });

  it('parseUser converts lockoutEnd, and keeps null', () => {
    expect(parseUser({ userID: 1, lockoutEnd: OFFSET }).lockoutEnd!.toMillis()).toBe(Date.parse(UTC));
    expect(parseUser({ userID: 1, lockoutEnd: null }).lockoutEnd).toBeNull();
    expect(parseUser({ userID: 1, userName: 'grif', lockoutEnd: null }).userName).toBe('grif');
  });

  it('parseScheduledMatch and parseSeasonMatchPage convert their times', () => {
    expect(parseScheduledMatch({ seasonMatchID: 1, time: UTC }).time.toMillis()).toBe(Date.parse(UTC));
    expect(parseSeasonMatchPage({ seasonID: 1, scheduledTime: UTC }).scheduledTime!.toMillis()).toBe(Date.parse(UTC));
    expect(parseSeasonMatchPage({ seasonID: 1, scheduledTime: null }).scheduledTime).toBeNull();
  });
});
