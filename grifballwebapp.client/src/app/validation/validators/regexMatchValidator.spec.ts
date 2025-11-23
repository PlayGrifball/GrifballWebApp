import { FormControl } from '@angular/forms';
import { regexMatchInvalidValidator, regexMatchValidValidator } from './regexMatchValidator';

describe('regexMatchValidators', () => {
  describe('regexMatchInvalidValidator', () => {
    it('should return error when regex matches (value is invalid)', () => {
      const regex = /[0-9]/;
      const control = new FormControl('123');
      const validator = regexMatchInvalidValidator(regex, 'Must not contain numbers');
      
      const result = validator(control);

      expect(result).toEqual({ regexValidation: { value: '123', errorMessage: 'Must not contain numbers' } });
    });

    it('should return null when regex does not match (value is valid)', () => {
      const regex = /[0-9]/;
      const control = new FormControl('abc');
      const validator = regexMatchInvalidValidator(regex, 'Must not contain numbers');
      
      const result = validator(control);

      expect(result).toBeNull();
    });

    it('should handle empty string', () => {
      const regex = /[0-9]/;
      const control = new FormControl('');
      const validator = regexMatchInvalidValidator(regex, 'Must not contain numbers');
      
      const result = validator(control);

      expect(result).toBeNull();
    });
  });

  describe('regexMatchValidValidator', () => {
    it('should return null when regex matches (value is valid)', () => {
      const regex = /^[a-zA-Z]+$/;
      const control = new FormControl('ValidText');
      const validator = regexMatchValidValidator(regex, 'Must contain only letters');
      
      const result = validator(control);

      expect(result).toBeNull();
    });

    it('should return error when regex does not match (value is invalid)', () => {
      const regex = /^[a-zA-Z]+$/;
      const control = new FormControl('Invalid123');
      const validator = regexMatchValidValidator(regex, 'Must contain only letters');
      
      const result = validator(control);

      expect(result).toEqual({ regexValidation: { value: 'Invalid123', errorMessage: 'Must contain only letters' } });
    });

    it('should handle complex password regex', () => {
      const regex = /^(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])(?=.*?[#?!@$%^&*-]).{12,}$/;
      const strongPassword = 'StrongPass123!@#';
      const weakPassword = 'weak';
      
      const validatorFn = regexMatchValidValidator(regex, 'Password too weak');
      
      expect(validatorFn(new FormControl(strongPassword))).toBeNull();
      expect(validatorFn(new FormControl(weakPassword))).toEqual({ 
        regexValidation: { value: 'weak', errorMessage: 'Password too weak' } 
      });
    });

    it('should return error for empty string when pattern requires content', () => {
      const regex = /^.+$/;  // Requires at least one character
      const control = new FormControl('');
      const validator = regexMatchValidValidator(regex, 'Cannot be empty');
      
      const result = validator(control);

      expect(result).toEqual({ regexValidation: { value: '', errorMessage: 'Cannot be empty' } });
    });
  });
});
