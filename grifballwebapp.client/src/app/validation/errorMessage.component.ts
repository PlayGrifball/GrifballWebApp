import { Component, ChangeDetectionStrategy, computed, effect, input, signal } from '@angular/core';
import { AbstractControl, AbstractControlDirective } from '@angular/forms';
import { ValidationService } from './validationService';

@Component({
    selector: 'app-error-message',
    imports: [],
    template: `
    @if (errorMessage() !== null) {
      <div>{{errorMessage()}}</div>
    }
    `,
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ErrorMessageComponent {
  readonly control = input<AbstractControl | AbstractControlDirective | null>();

  // Bumped on every value/status/touched/pristine event of the bound control. The control is
  // not a signal, so without this the message would not update while the input stays the same.
  private readonly controlEvents = signal(0);

  constructor() {
    effect(onCleanup => {
      const control = this.control();
      const formControl = control instanceof AbstractControlDirective ? control.control : control;
      const subscription = formControl?.events.subscribe(() => this.controlEvents.update(n => n + 1));
      onCleanup(() => subscription?.unsubscribe());
    });
  }

  readonly errorMessage = computed<string | null>(() => {
    this.controlEvents();
    const control = this.control();
    if (control === null)
      return null;
    if (control === undefined)
      return null;
    for (let propertyName in control.errors) {
      if (
        control.errors.hasOwnProperty(propertyName) &&
        (control.touched || control.dirty)
      ) {
        return ValidationService.getValidatorErrorMessage(
          propertyName,
          control.errors[propertyName]
        );
      }
    }

    return null;
  });
}
