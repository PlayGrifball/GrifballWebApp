import { Directive, effect, input } from '@angular/core';
import { AbstractControl, NG_VALIDATORS, ValidationErrors, Validator } from '@angular/forms';
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
export class MatchFieldsValidatorDirective implements Validator {
  readonly field1 = input<AbstractControl>();
  readonly field2 = input<AbstractControl>();

  private onValidatorChange?: () => void;

  constructor() {
    // The fields are bound after the form first validates: re-run validation when they change.
    effect(() => {
      this.field1();
      this.field2();
      this.onValidatorChange?.();
    });
  }

  registerOnValidatorChange(fn: () => void): void {
    this.onValidatorChange = fn;
  }

  validate(control: AbstractControl): ValidationErrors | null {
    const field1 = this.field1();
    const field2 = this.field2();
    return field1 && field2
      ? matchFieldsValidator(field1, field2)(control)
      : null;
  }
}
