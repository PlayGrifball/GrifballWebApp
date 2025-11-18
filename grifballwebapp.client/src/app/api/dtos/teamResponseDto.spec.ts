import { TeamResponseDto } from './teamResponseDto';
import { CaptainDto } from './captainDto';
import { PlayerDto } from './playerDto';

describe('TeamResponseDto', () => {
  it('should create an instance with default values', () => {
    const dto = new TeamResponseDto();
    
    expect(dto).toBeTruthy();
    expect(dto.teamID).toBe(0);
    expect(dto.teamName).toBe('');
    expect(dto.captain).toBeDefined();
    expect(typeof dto.captain).toBe('object');
  });

  it('should allow setting captain property', () => {
    const dto = new TeamResponseDto();
    const captain = new CaptainDto();
    captain.personID = 123;
    captain.name = 'Team Captain';
    captain.order = 1;
    
    dto.teamID = 456;
    dto.teamName = 'Alpha Team';
    dto.captain = captain;
    
    expect(dto.teamID).toBe(456);
    expect(dto.teamName).toBe('Alpha Team');
    expect(dto.captain.personID).toBe(123);
    expect(dto.captain.name).toBe('Team Captain');
  });

  it('should allow setting players array', () => {
    const dto = new TeamResponseDto();
    const player1 = new PlayerDto();
    player1.personID = 111;
    player1.name = 'Player 1';
    
    const player2 = new PlayerDto();
    player2.personID = 222;
    player2.name = 'Player 2';
    
    dto.players = [player1, player2];
    
    expect(dto.players.length).toBe(2);
    expect(dto.players[0].personID).toBe(111);
    expect(dto.players[1].personID).toBe(222);
  });

  it('should handle empty players array', () => {
    const dto = new TeamResponseDto();
    dto.players = [];
    
    expect(dto.players).toEqual([]);
  });
});
