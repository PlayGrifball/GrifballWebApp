import { Directive, input } from '@angular/core';
import { AbstractControl, NG_VALIDATORS, ValidationErrors, Validator } from '@angular/forms';
import { regexMatchValidValidator } from '../validators/regexMatchValidator';

@Directive({
  selector: '[regexExpression]',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: RegexMatchValidValidatorDirective,
      multi: true,
    },
  ],
  standalone: true,
})
export class RegexMatchValidValidatorDirective implements Validator {
  readonly regexExpression = input('');
  readonly errorMessage = input('Must not match regex');

  validate(control: AbstractControl): ValidationErrors | null {
    const regexExpression = this.regexExpression();
    return regexExpression
      ? regexMatchValidValidator(new RegExp(regexExpression, 'g'), this.errorMessage())(control)
      : null;
  }
}
