import { SignupResponseDto, TimeslotDto } from './signupResponseDto';

describe('SignupResponseDto', () => {
  it('defaults to an empty signup with no timeslots', () => {
    const dto = new SignupResponseDto();
    expect(dto.seasonID).toBe(0);
    expect(dto.userID).toBe(0);
    expect(dto.personName).toBeNull();
    expect(dto.teamName).toBeNull();
    expect(dto.willCaptain).toBeFalse();
    expect(dto.requiresAssistanceDrafting).toBeFalse();
    expect(dto.timeslots).toEqual([]);
  });

  it('gives each signup its own timeslot array', () => {
    const a = new SignupResponseDto();
    const b = new SignupResponseDto();
    a.timeslots.push(new TimeslotDto());
    expect(b.timeslots).toEqual([]);
  });
});

describe('TimeslotDto', () => {
  it('defaults to an unchecked, enabled, non-header slot', () => {
    const slot = new TimeslotDto();
    expect(slot).toEqual(jasmine.objectContaining({
      id: 0, dayOfWeek: '', time: '', isChecked: false, isDisabled: false, isHeader: false
    }));
  });
});
