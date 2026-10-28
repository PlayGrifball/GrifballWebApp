import { DatePipe } from '@angular/common';
import { DateTime } from 'luxon';
import { fromApiDate, fromRequiredApiDate, toApiDate } from './apiDates';
import { ToDatePipe } from './toDate.pipe';
import { parseSeason } from '../api/dtos/seasonDto';
import { parseUser } from '../userManagement/userResponseDto';
import { parseScheduledMatch } from '../season/scheduleList/scheduledMatchDto';
import { parseSeasonMatchPage } from '../season/seasonMatch/seasonMatchPageDto';
import { parseTeamMatch } from '../season/team/team.component';

// What the server's DateTimeJsonConverter writes: UTC with a Z. DateTimeOffset keeps its offset.
const UTC = '2026-01-02T03:04:00Z';
const OFFSET = '2026-01-02T03:04:00+00:00';

describe('API dates', () => {
  const datePipe = new DatePipe('en-US');

  it('fromApiDate reads the server format as the same instant', () => {
    const value = fromApiDate(UTC)!;
    expect(value instanceof DateTime).toBeTrue();
    expect(value.isValid).toBeTrue();
    expect(value.toMillis()).toBe(Date.parse(UTC));
    expect(fromRequiredApiDate(UTC).toMillis()).toBe(Date.parse(UTC));
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

  it('toApiDate writes UTC, so the end of time stays within year 9999 in any zone', () => {
    // DateTimeOffset.MaxValue (a permanent lockout) as the API sends it, seen from a zone ahead of UTC.
    const max = DateTime.fromISO('9999-12-31T23:59:59.9999999+00:00').setZone('Europe/Berlin');
    expect(JSON.parse(JSON.stringify({ v: max })).v).toMatch(/^\+010000-/); // what toJSON would send
    expect(toApiDate(max)).toBe('9999-12-31T23:59:59.999Z');
    expect(toApiDate(fromApiDate('2026-01-02T03:04:00+01:00'))).toBe('2026-01-02T02:04:00.000Z');
    expect(toApiDate(null)).toBeNull();
  });

  it('toDate + date pipe shows exactly what the date pipe showed for the ISO string', () => {
    const toDate = new ToDatePipe();
    expect(datePipe.transform(toDate.transform(fromApiDate(UTC)), 'short')).toBe(datePipe.transform(UTC, 'short'));
    expect(toDate.transform(null)).toBeNull();
    expect(toDate.transform(undefined)).toBeNull();
  });

  it('parseSeason converts every season date', () => {
    const season = parseSeason({ seasonID: 4, seasonName: 'S', signupsOpen: UTC, signupsClose: UTC, draftStart: UTC, seasonStart: UTC, seasonEnd: UTC, signupsCount: 2 })!;
    for (const field of ['signupsOpen', 'signupsClose', 'draftStart', 'seasonStart', 'seasonEnd'] as const) {
      expect(season[field] instanceof DateTime).withContext(field).toBeTrue();
      expect(season[field].toMillis()).withContext(field).toBe(Date.parse(UTC));
    }
    expect(season.seasonName).toBe('S');
    expect(season.signupsCount).toBe(2);
  });

  it('parseUser converts lockoutEnd, and keeps null', () => {
    expect(parseUser({ userID: 1, lockoutEnd: OFFSET })!.lockoutEnd!.toMillis()).toBe(Date.parse(UTC));
    expect(parseUser({ userID: 1, lockoutEnd: null })!.lockoutEnd).toBeNull();
    expect(parseUser({ userID: 1, userName: 'grif', lockoutEnd: null })!.userName).toBe('grif');
  });

  it('parseScheduledMatch and parseSeasonMatchPage convert their times', () => {
    expect(parseScheduledMatch({ seasonMatchID: 1, time: UTC }).time.toMillis()).toBe(Date.parse(UTC));
    expect(parseSeasonMatchPage({ seasonID: 1, scheduledTime: UTC })!.scheduledTime!.toMillis()).toBe(Date.parse(UTC));
    expect(parseSeasonMatchPage({ seasonID: 1, scheduledTime: null })!.scheduledTime).toBeNull();
  });

  it('parseTeamMatch converts scheduledTime, and keeps an unscheduled match null', () => {
    expect(parseTeamMatch({ seasonMatchID: 1, scheduledTime: UTC, bestOf: 3 }).scheduledTime!.toMillis()).toBe(Date.parse(UTC));
    expect(parseTeamMatch({ seasonMatchID: 1, scheduledTime: null, bestOf: 3 }).scheduledTime).toBeNull();
    expect(parseTeamMatch({ seasonMatchID: 1, scheduledTime: null, bestOf: 3 }).bestOf).toBe(3);
  });

  it('the parsers for single DTOs keep a null body (204: no such season, user or match) null', () => {
    expect(parseSeason(null)).toBeNull();
    expect(parseUser(null)).toBeNull();
    expect(parseSeasonMatchPage(null)).toBeNull();
    expect(parseUser(undefined)).toBeNull();
  });
});
