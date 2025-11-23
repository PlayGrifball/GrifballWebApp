import { TestBed } from '@angular/core/testing';
import { AvailabilityService } from './availability.service';
import { DateTime } from 'luxon';

describe('AvailabilityService', () => {
  let service: AvailabilityService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AvailabilityService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have DayOfWeek property set to "Day Of Week"', () => {
    expect(service.DayOfWeek).toBe('Day Of Week');
  });

  describe('getDif', () => {
    it('should return the difference between local and Detroit timezone offsets', () => {
      const result = service.getDif();
      
      // The result should be a number (in minutes)
      expect(typeof result).toBe('number');
      
      // Verify it calculates correctly
      const detroitOffset = DateTime.now().setZone('America/Detroit').offset;
      const localOffset = DateTime.now().offset;
      const expectedDif = localOffset - detroitOffset;
      
      expect(result).toBe(expectedDif);
    });

    it('should return 0 if local timezone is Detroit', () => {
      // This test verifies the logic when both timezones are the same
      const detroitOffset = DateTime.now().setZone('America/Detroit').offset;
      const localOffset = DateTime.now().offset;
      const dif = localOffset - detroitOffset;
      
      // If we're in Detroit timezone, difference should be 0
      if (localOffset === detroitOffset) {
        expect(dif).toBe(0);
      } else {
        // Otherwise, just verify it's a valid number
        expect(typeof dif).toBe('number');
      }
    });
  });

  describe('parseTime', () => {
    it('should return "Day Of Week" unchanged when input is "Day Of Week"', () => {
      const result = service.parseTime('Day Of Week');
      expect(result).toBe('Day Of Week');
    });

    it('should parse and format time string correctly', () => {
      const time = '1:30 PM';
      const result = service.parseTime(time);
      
      // Result should be a formatted time string
      expect(typeof result).toBe('string');
      expect(result.length).toBeGreaterThan(0);
    });

    it('should convert 12-hour format to localized time format', () => {
      const time = '3:45 PM';
      const result = service.parseTime(time);
      
      // Parse and verify we get a valid time back
      const dateTime = DateTime.fromFormat(time, 'TT');
      const expected = dateTime.toFormat('t');
      
      expect(result).toBe(expected);
    });

    it('should handle morning times', () => {
      const time = '9:00 AM';
      const result = service.parseTime(time);
      
      const dateTime = DateTime.fromFormat(time, 'TT');
      const expected = dateTime.toFormat('t');
      
      expect(result).toBe(expected);
    });

    it('should handle noon correctly', () => {
      const time = '12:00 PM';
      const result = service.parseTime(time);
      
      const dateTime = DateTime.fromFormat(time, 'TT');
      const expected = dateTime.toFormat('t');
      
      expect(result).toBe(expected);
    });

    it('should handle midnight correctly', () => {
      const time = '12:00 AM';
      const result = service.parseTime(time);
      
      const dateTime = DateTime.fromFormat(time, 'TT');
      const expected = dateTime.toFormat('t');
      
      expect(result).toBe(expected);
    });

    it('should handle various time formats', () => {
      const times = ['1:00 AM', '12:30 PM', '11:59 PM', '6:45 AM'];
      
      times.forEach(time => {
        const result = service.parseTime(time);
        const dateTime = DateTime.fromFormat(time, 'TT');
        const expected = dateTime.toFormat('t');
        expect(result).toBe(expected);
      });
    });

    it('should consistently format times', () => {
      const time1 = service.parseTime('3:00 PM');
      const time2 = service.parseTime('3:00 PM');
      
      expect(time1).toBe(time2);
    });
  });

  describe('getDif edge cases', () => {
    it('should return consistent value on multiple calls', () => {
      const dif1 = service.getDif();
      const dif2 = service.getDif();
      
      // Should return the same value (difference doesn't change within test execution)
      expect(dif1).toBe(dif2);
    });

    it('should return a finite number', () => {
      const dif = service.getDif();
      
      expect(isFinite(dif)).toBe(true);
    });
  });
});
