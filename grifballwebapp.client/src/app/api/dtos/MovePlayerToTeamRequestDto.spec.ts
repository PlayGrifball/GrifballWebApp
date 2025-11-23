import { MovePlayerToTeamRequestDto } from './MovePlayerToTeamRequestDto';

describe('MovePlayerToTeamRequestDto', () => {
  it('should create an object with all properties', () => {
    const dto: MovePlayerToTeamRequestDto = {
      seasonID: 123,
      previousCaptainID: 456,
      newCaptainID: 789,
      personID: 111,
      roundNumber: 5
    };
    
    expect(dto.seasonID).toBe(123);
    expect(dto.previousCaptainID).toBe(456);
    expect(dto.newCaptainID).toBe(789);
    expect(dto.personID).toBe(111);
    expect(dto.roundNumber).toBe(5);
  });

  it('should allow modification of properties', () => {
    const dto: MovePlayerToTeamRequestDto = {
      seasonID: 1,
      previousCaptainID: 2,
      newCaptainID: 3,
      personID: 4,
      roundNumber: 1
    };
    
    dto.seasonID = 100;
    dto.previousCaptainID = 200;
    dto.newCaptainID = 300;
    dto.personID = 400;
    dto.roundNumber = 10;
    
    expect(dto.seasonID).toBe(100);
    expect(dto.previousCaptainID).toBe(200);
    expect(dto.newCaptainID).toBe(300);
    expect(dto.personID).toBe(400);
    expect(dto.roundNumber).toBe(10);
  });
});
