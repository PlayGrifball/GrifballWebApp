import { CaptainDto } from './captainDto';

describe('CaptainDto', () => {
  it('should create an instance with default values', () => {
    const dto = new CaptainDto();
    
    expect(dto).toBeTruthy();
    expect(dto.personID).toBe(0);
    expect(dto.name).toBe('');
    expect(dto.order).toBeNull();
  });

  it('should allow setting all properties', () => {
    const dto = new CaptainDto();
    dto.personID = 123;
    dto.name = 'Captain America';
    dto.order = 1;
    
    expect(dto.personID).toBe(123);
    expect(dto.name).toBe('Captain America');
    expect(dto.order).toBe(1);
  });

  it('should allow null order value', () => {
    const dto = new CaptainDto();
    dto.personID = 456;
    dto.name = 'Captain Marvel';
    dto.order = null;
    
    expect(dto.order).toBeNull();
  });
});
