import { PlayerDto } from './playerDto';

describe('PlayerDto', () => {
  it('should create an instance with default values', () => {
    const dto = new PlayerDto();
    
    expect(dto).toBeTruthy();
    expect(dto.personID).toBe(0);
    expect(dto.name).toBe('');
    expect(dto.round).toBeNull();
    expect(dto.pick).toBeNull();
  });

  it('should allow setting all properties', () => {
    const dto = new PlayerDto();
    dto.personID = 123;
    dto.name = 'John Doe';
    dto.round = 5;
    dto.pick = 3;
    
    expect(dto.personID).toBe(123);
    expect(dto.name).toBe('John Doe');
    expect(dto.round).toBe(5);
    expect(dto.pick).toBe(3);
  });

  it('should allow null round and pick values', () => {
    const dto = new PlayerDto();
    dto.personID = 456;
    dto.name = 'Jane Smith';
    dto.round = null;
    dto.pick = null;
    
    expect(dto.round).toBeNull();
    expect(dto.pick).toBeNull();
  });
});
