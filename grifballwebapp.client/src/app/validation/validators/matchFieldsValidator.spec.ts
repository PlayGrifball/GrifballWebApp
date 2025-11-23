import { FormControl, FormGroup } from '@angular/forms';
import { matchFieldsValidator } from './matchFieldsValidator';

describe('matchFieldsValidator', () => {
  it('should return null when fields match', () => {
    const field1 = new FormControl('password123');
    const field2 = new FormControl('password123');
    const group = new FormGroup({ field1, field2 });
    
    const validator = matchFieldsValidator(field1, field2);
    const result = validator(field2);

    expect(result).toBeNull();
  });

  it('should return error when fields do not match', () => {
    const field1 = new FormControl('password123');
    const field2 = new FormControl('different');
    const group = new FormGroup({ password: field1, confirmPassword: field2 });
    
    const validator = matchFieldsValidator(field1, field2);
    const result = validator(field2);

    expect(result).toEqual({ matchOtherValidator: { otherControlName: 'password' } });
  });

  it('should handle empty values that match', () => {
    const field1 = new FormControl('');
    const field2 = new FormControl('');
    const group = new FormGroup({ field1, field2 });
    
    const validator = matchFieldsValidator(field1, field2);
    const result = validator(field2);

    expect(result).toBeNull();
  });

  it('should handle null parent control', () => {
    const field1 = new FormControl('value1');
    const field2 = new FormControl('value2');
    // No parent group
    
    const validator = matchFieldsValidator(field1, field2);
    const result = validator(field2);

    expect(result).toEqual({ matchOtherValidator: { otherControlName: null } });
  });

  it('should return error when field name cannot be found', () => {
    const field1 = new FormControl('value');
    const field2 = new FormControl('value');
    const field3 = new FormControl('other');
    const group = new FormGroup({ field1, field2 });
    // field3 is not in the group
    
    const validator = matchFieldsValidator(field3, field2);
    const result = validator(field2);

    // Since field3 is not in the parent group, getName returns null
    // and the validator returns an error with null as the name
    expect(result).toEqual({ matchOtherValidator: { otherControlName: null } });
  });
});
