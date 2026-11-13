import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { RegexMatchValidValidatorDirective } from './regexMatchValidValidatorDirective.directive';

@Component({
  imports: [RegexMatchValidValidatorDirective],
  template: `<input [regexExpression]="regex()" [errorMessage]="message()">`,
})
class HostComponent {
  regex = signal('');
  message = signal('Letters only');
}

describe('RegexMatchValidValidatorDirective', () => {
  function setup(regex: string) {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.regex.set(regex);
    fixture.detectChanges();
    return fixture.debugElement.query(By.directive(RegexMatchValidValidatorDirective)).injector.get(RegexMatchValidValidatorDirective);
  }

  it('accepts anything while no expression is bound', () => {
    expect(setup('').validate(new FormControl('123'))).toBeNull();
  });

  it('accepts a value that matches the bound expression', () => {
    expect(setup('^[a-z]+$').validate(new FormControl('grif'))).toBeNull();
  });

  it('rejects a value that does not match, with the bound message', () => {
    expect(setup('^[a-z]+$').validate(new FormControl('123'))).toEqual({
      regexValidation: { value: '123', errorMessage: 'Letters only' },
    });
  });
});
