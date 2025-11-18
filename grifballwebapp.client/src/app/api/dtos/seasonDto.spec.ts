import { SeasonDto } from './seasonDto';
import { DateTime } from 'luxon';

describe('SeasonDto', () => {
  it('should create an instance with default values', () => {
    const dto = new SeasonDto();
    
    expect(dto).toBeTruthy();
    expect(dto.seasonID).toBe(0);
    expect(dto.seasonName).toBe('');
    expect(dto.signupsCount).toBe(0);
    expect(dto.copyFrom).toBeNull();
    expect(dto.copyAvailability).toBe(false);
    expect(dto.copySignups).toBe(false);
    expect(dto.copyTeams).toBe(false);
  });

  it('should allow setting all numeric and string properties', () => {
    const dto = new SeasonDto();
    dto.seasonID = 42;
    dto.seasonName = 'Spring 2024';
    dto.signupsCount = 100;
    dto.copyFrom = 'Season41';
    
    expect(dto.seasonID).toBe(42);
    expect(dto.seasonName).toBe('Spring 2024');
    expect(dto.signupsCount).toBe(100);
    expect(dto.copyFrom).toBe('Season41');
  });

  it('should allow setting DateTime properties', () => {
    const dto = new SeasonDto();
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
    const dto = new SeasonDto();
    dto.copyAvailability = true;
    dto.copySignups = true;
    dto.copyTeams = true;
    
    expect(dto.copyAvailability).toBe(true);
    expect(dto.copySignups).toBe(true);
    expect(dto.copyTeams).toBe(true);
  });
});
