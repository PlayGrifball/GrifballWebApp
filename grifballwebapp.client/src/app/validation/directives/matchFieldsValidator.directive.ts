import { Directive, Input } from '@angular/core';
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
  @Input('field1') field1!: AbstractControl;
  @Input('field2') field2!: AbstractControl;

  validate(control: AbstractControl): ValidationErrors | null {
    return this.field1 && this.field2
      ? matchFieldsValidator(this.field1, this.field2)(control)
      : null;
  }
}
