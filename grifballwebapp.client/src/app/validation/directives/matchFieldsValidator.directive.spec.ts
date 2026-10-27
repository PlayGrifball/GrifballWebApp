import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { MatchFieldsValidatorDirective } from './matchFieldsValidator.directive';

@Component({
  imports: [MatchFieldsValidatorDirective],
  template: `<div matchFields [field1]="password" [field2]="confirm"></div><div matchFields></div>`,
})
class HostComponent {
  password = new FormControl('secret');
  confirm = new FormControl('secret');
}

describe('MatchFieldsValidatorDirective', () => {
  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const [bound, unbound] = fixture.debugElement.queryAll(By.directive(MatchFieldsValidatorDirective))
      .map(d => d.injector.get(MatchFieldsValidatorDirective));
    return { host: fixture.componentInstance, bound, unbound };
  }

  it('passes while the fields are not bound yet', () => {
    expect(setup().unbound.validate(new FormControl())).toBeNull();
  });

  it('passes when both fields match', () => {
    expect(setup().bound.validate(new FormControl())).toBeNull();
  });

  it('fails when the fields differ', () => {
    const { host, bound } = setup();
    host.confirm.setValue('other');
    expect(bound.validate(new FormControl())).toEqual({ matchOtherValidator: { otherControlName: null } });
  });
});
