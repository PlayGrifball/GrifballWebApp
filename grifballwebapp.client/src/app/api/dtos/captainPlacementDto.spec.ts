import { CaptainPlacementDto, CaptainAddedDto, RemoveCaptainDto } from './captainPlacementDto';

describe('CaptainPlacementDto', () => {
  it('should create an instance with default values', () => {
    const dto = new CaptainPlacementDto();
    
    expect(dto).toBeTruthy();
    expect(dto.seasonID).toBe(0);
    expect(dto.personID).toBe(0);
    expect(dto.orderNumber).toBe(0);
  });

  it('should allow setting properties', () => {
    const dto = new CaptainPlacementDto();
    dto.seasonID = 123;
    dto.personID = 456;
    dto.orderNumber = 1;
    
    expect(dto.seasonID).toBe(123);
    expect(dto.personID).toBe(456);
    expect(dto.orderNumber).toBe(1);
  });
});

describe('CaptainAddedDto', () => {
  it('should create an instance with default values', () => {
    const dto = new CaptainAddedDto();
    
    expect(dto).toBeTruthy();
    expect(dto.seasonID).toBe(0);
    expect(dto.personID).toBe(0);
    expect(dto.teamName).toBe('');
    expect(dto.captainName).toBe('');
    expect(dto.orderNumber).toBe(0);
  });

  it('should allow setting all properties', () => {
    const dto = new CaptainAddedDto();
    dto.seasonID = 123;
    dto.personID = 456;
    dto.teamName = 'Alpha Team';
    dto.captainName = 'John Doe';
    dto.orderNumber = 1;
    
    expect(dto.seasonID).toBe(123);
    expect(dto.personID).toBe(456);
    expect(dto.teamName).toBe('Alpha Team');
    expect(dto.captainName).toBe('John Doe');
    expect(dto.orderNumber).toBe(1);
  });
});

describe('RemoveCaptainDto', () => {
  it('should create an instance with default values', () => {
    const dto = new RemoveCaptainDto();
    
    expect(dto).toBeTruthy();
    expect(dto.seasonID).toBe(0);
    expect(dto.personID).toBe(0);
  });

  it('should allow setting properties', () => {
    const dto = new RemoveCaptainDto();
    dto.seasonID = 123;
    dto.personID = 456;
    
    expect(dto.seasonID).toBe(123);
    expect(dto.personID).toBe(456);
  });
});
