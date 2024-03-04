import { AbstractControl, ValidationErrors, ValidatorFn } from "@angular/forms";

/** If regex matches then control is invalid */
export function regexMatchInvalidValidator(regexExpression: RegExp, errorMessage: string): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const forbidden = regexExpression.test(control.value);
      return forbidden ? { regexValidation: { value: control.value, errorMessage: errorMessage } } : null;
    };
}

/** If regex matches then control is valid */
export function regexMatchValidValidator(regexExpression: RegExp, errorMessage: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const forbidden = !regexExpression.test(control.value);
    return forbidden ? { regexValidation: { value: control.value, errorMessage: errorMessage } } : null;
  };
}
