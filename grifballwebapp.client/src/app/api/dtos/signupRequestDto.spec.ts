import { SignupRequestDto } from './signupRequestDto';
import { TimeslotDto } from './signupResponseDto';

describe('SignupRequestDto', () => {
  it('should create an instance with default values', () => {
    const dto = new SignupRequestDto();
    
    expect(dto).toBeTruthy();
    expect(dto.seasonID).toBe(0);
    expect(dto.userID).toBe(0);
    expect(dto.teamName).toBeNull();
    expect(dto.willCaptain).toBe(false);
    expect(dto.requiresAssistanceDrafting).toBe(false);
    expect(dto.timeslots).toEqual([]);
  });

  it('should allow setting all properties', () => {
    const dto = new SignupRequestDto();
    dto.seasonID = 123;
    dto.userID = 456;
    dto.teamName = 'Alpha Squad';
    dto.willCaptain = true;
    dto.requiresAssistanceDrafting = true;
    
    expect(dto.seasonID).toBe(123);
    expect(dto.userID).toBe(456);
    expect(dto.teamName).toBe('Alpha Squad');
    expect(dto.willCaptain).toBe(true);
    expect(dto.requiresAssistanceDrafting).toBe(true);
  });

  it('should allow setting timeslots array', () => {
    const dto = new SignupRequestDto();
    const timeslot1: TimeslotDto = {
      id: 1,
      dayOfWeek: 'Monday',
      time: '19:00',
      isChecked: true,
      isDisabled: false,
      isHeader: false
    };
    const timeslot2: TimeslotDto = {
      id: 2,
      dayOfWeek: 'Wednesday',
      time: '20:00',
      isChecked: true,
      isDisabled: false,
      isHeader: false
    };
    
    dto.timeslots = [timeslot1, timeslot2];
    
    expect(dto.timeslots.length).toBe(2);
    expect(dto.timeslots[0]).toEqual(timeslot1);
    expect(dto.timeslots[1]).toEqual(timeslot2);
  });

  it('should handle null teamName for non-captains', () => {
    const dto = new SignupRequestDto();
    dto.willCaptain = false;
    dto.teamName = null;
    
    expect(dto.teamName).toBeNull();
    expect(dto.willCaptain).toBe(false);
  });
});
