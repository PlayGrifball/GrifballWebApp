import { FormControl, FormGroup } from '@angular/forms';
import { matchFieldsValidator } from './matchFieldsValidator';

describe('matchFieldsValidator', () => {
  it('should return null when fields match', () => {
    const field1 = new FormControl('password123');
    const field2 = new FormControl('password123');
    const group = new FormGroup({ field1, field2 });
    
    const validator = matchFieldsValidator(group.controls.field1, group.controls.field2);
    const result = validator(field2);

    expect(result).toBeNull();
  });

  it('should return error when fields do not match', () => {
    const field1 = new FormControl('password123');
    const field2 = new FormControl('different');
    const group = new FormGroup({ password: field1, confirmPassword: field2 });
    
    const validator = matchFieldsValidator(group.controls.password, group.controls.confirmPassword);
    const result = validator(field2);

    expect(result).toEqual({ matchOtherValidator: { otherControlName: 'password' } });
  });

  it('should handle empty values that match', () => {
    const field1 = new FormControl('');
    const field2 = new FormControl('');
    const group = new FormGroup({ field1, field2 });
    
    const validator = matchFieldsValidator(group.controls.field1, group.controls.field2);
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
});
