import { Directive, input } from '@angular/core';
import { AbstractControl, NG_VALIDATORS, ValidationErrors, Validator, ValidatorFn } from '@angular/forms';
import { matchFieldsValidator } from '../validators/matchFieldsValidator';

@Directive({
  selector: '[matchFields]',
  providers: [
    {
      provide: NG_VALIDATORS,
      useExisting: MatchFieldsValidatorDirective,
      multi: true,
    },
  ],
  standalone: true,
})
export class MatchFieldsValidatorDirective {
  readonly field1 = input<AbstractControl>();
  readonly field2 = input<AbstractControl>();

  validate(control: AbstractControl): ValidationErrors | null {
    const field1 = this.field1();
    const field2 = this.field2();
    return field1 && field2
      ? matchFieldsValidator(field1, field2)(control)
      : null;
  }
}
