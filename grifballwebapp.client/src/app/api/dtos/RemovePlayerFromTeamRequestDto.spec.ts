import { RemovePlayerFromTeamRequestDto } from './RemovePlayerFromTeamRequestDto';

describe('RemovePlayerFromTeamRequestDto', () => {
  it('should create an object with all properties', () => {
    const dto: RemovePlayerFromTeamRequestDto = {
      seasonID: 123,
      captainID: 456,
      personID: 789
    };
    
    expect(dto.seasonID).toBe(123);
    expect(dto.captainID).toBe(456);
    expect(dto.personID).toBe(789);
  });

  it('should allow modification of properties', () => {
    const dto: RemovePlayerFromTeamRequestDto = {
      seasonID: 1,
      captainID: 2,
      personID: 3
    };
    
    dto.seasonID = 100;
    dto.captainID = 200;
    dto.personID = 300;
    
    expect(dto.seasonID).toBe(100);
    expect(dto.captainID).toBe(200);
    expect(dto.personID).toBe(300);
  });
});
