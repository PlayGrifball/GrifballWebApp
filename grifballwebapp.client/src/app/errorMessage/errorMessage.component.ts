import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { AbstractControl, AbstractControlDirective, FormControl } from '@angular/forms';
import { ValidationService } from './validationService';

@Component({
  selector: 'app-error-message',
  standalone: true,
  imports: [
    CommonModule,
  ],
  template: `
    <div *ngIf="errorMessage !== null">{{errorMessage}}</div>
    `
})
export class ErrorMessageComponent {
  @Input() control!: AbstractControl | AbstractControlDirective;

  get errorMessage(): string | null {
    if (this.control === null)
      return null;
    if (this.control === undefined)
      return null;
    for (let propertyName in this.control.errors) {
      if (
        this.control.errors.hasOwnProperty(propertyName) &&
        (this.control.touched || this.control.dirty)
      ) {
        return ValidationService.getValidatorErrorMessage(
          propertyName,
          this.control.errors[propertyName]
        );
      }
    }

    return null;
  }
}
