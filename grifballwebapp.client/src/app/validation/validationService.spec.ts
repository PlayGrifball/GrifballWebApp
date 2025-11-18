import { ValidationService } from './validationService';

describe('ValidationService', () => {
  describe('getValidatorErrorMessage', () => {
    it('should return "Required" for required validator', () => {
      const result = ValidationService.getValidatorErrorMessage('required', {});
      expect(result).toBe('Required');
    });

    it('should return credit card message for invalidCreditCard validator', () => {
      const result = ValidationService.getValidatorErrorMessage('invalidCreditCard', {});
      expect(result).toBe('Is invalid credit card number');
    });

    it('should return email message for invalidEmailAddress validator', () => {
      const result = ValidationService.getValidatorErrorMessage('invalidEmailAddress', {});
      expect(result).toBe('Invalid email address');
    });

    it('should return password message for invalidPassword validator', () => {
      const result = ValidationService.getValidatorErrorMessage('invalidPassword', {});
      expect(result).toContain('Invalid password');
    });

    it('should return minlength message with required length', () => {
      const result = ValidationService.getValidatorErrorMessage('minlength', { requiredLength: 8 });
      expect(result).toBe('Minimum length 8');
    });

    it('should return regex validation message with custom error', () => {
      const result = ValidationService.getValidatorErrorMessage('regexValidation', { errorMessage: 'Custom error' });
      expect(result).toBe('Custom error');
    });

    it('should return match validator message with field name', () => {
      const result = ValidationService.getValidatorErrorMessage('matchOtherValidator', { otherControlName: 'password' });
      expect(result).toBe('Must match password');
    });

    it('should return undefined for unknown validator', () => {
      const result = ValidationService.getValidatorErrorMessage('unknownValidator', {});
      expect(result).toBeUndefined();
    });
  });
});
