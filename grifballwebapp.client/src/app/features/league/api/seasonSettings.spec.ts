import type { SeasonDto } from '@api/models';
import { parseSeason, SeasonSettings } from './seasonSettings';
import { DateTime } from 'luxon';

describe('SeasonSettings', () => {
  it('should create an instance with default values', () => {
    const dto = new SeasonSettings();
    
    expect(dto).toBeTruthy();
    expect(dto.seasonID).toBe(0);
    expect(dto.seasonName).toBe('');
    expect(dto.signupsCount).toBe(0);
    expect(dto.copyFromSeasonID).toBeNull();
    expect(dto.gameVersionID).toBeNull();
    expect(dto.leagueID).toBe(1);
    expect(dto.division).toBeNull();
    expect(dto.copyAvailability).toBe(false);
    expect(dto.copySignups).toBe(false);
    expect(dto.copyTeams).toBe(false);
  });

  it('should allow setting all numeric and string properties', () => {
    const dto = new SeasonSettings();
    dto.seasonID = 42;
    dto.seasonName = 'Spring 2024';
    dto.signupsCount = 100;
    dto.copyFromSeasonID = 41;
    
    expect(dto.seasonID).toBe(42);
    expect(dto.seasonName).toBe('Spring 2024');
    expect(dto.signupsCount).toBe(100);
    expect(dto.copyFromSeasonID).toBe(41);
  });

  it('should allow setting DateTime properties', () => {
    const dto = new SeasonSettings();
    const now = DateTime.now();
    
    dto.signupsOpen = now;
    dto.signupsClose = now.plus({ days: 7 });
    dto.draftStart = now.plus({ days: 14 });
    dto.seasonStart = now.plus({ days: 21 });
    dto.seasonEnd = now.plus({ days: 90 });
    
    expect(dto.signupsOpen).toEqual(now);
    expect(dto.signupsClose).toEqual(now.plus({ days: 7 }));
    expect(dto.draftStart).toEqual(now.plus({ days: 14 }));
    expect(dto.seasonStart).toEqual(now.plus({ days: 21 }));
    expect(dto.seasonEnd).toEqual(now.plus({ days: 90 }));
  });

  it('should allow setting copy flags', () => {
    const dto = new SeasonSettings();
    dto.copyAvailability = true;
    dto.copySignups = true;
    dto.copyTeams = true;
    
    expect(dto.copyAvailability).toBe(true);
    expect(dto.copySignups).toBe(true);
    expect(dto.copyTeams).toBe(true);
  });
});

// What the server's DateTimeJsonConverter writes: UTC with a Z.
const UTC = '2026-01-02T03:04:00Z';

describe('parseSeason', () => {
  it('parseSeason converts every season date', () => {
    const season = parseSeason({ seasonID: 4, seasonName: 'S', signupsOpen: UTC, signupsClose: UTC, draftStart: UTC, seasonStart: UTC, seasonEnd: UTC, signupsCount: 2 } as SeasonDto)!;
    for (const field of ['signupsOpen', 'signupsClose', 'draftStart', 'seasonStart', 'seasonEnd'] as const) {
      expect(season[field] instanceof DateTime).withContext(field).toBeTrue();
      expect(season[field].toMillis()).withContext(field).toBe(Date.parse(UTC));
    }
    expect(season.seasonName).toBe('S');
    expect(season.signupsCount).toBe(2);
  });

  it('keeps a null body (204: no such season) null', () => {
    expect(parseSeason(null)).toBeNull();
  });
});
