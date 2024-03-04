import { Directive, Input } from '@angular/core';
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
  @Input('regexExpression') regexExpression = '';
  @Input('errorMessage') errorMessage = 'Must not match regex';

  validate(control: AbstractControl): ValidationErrors | null {
    return this.regexExpression
      ? regexMatchValidValidator(new RegExp(this.regexExpression, 'g'), this.errorMessage)(control)
      : null;
  }
}
