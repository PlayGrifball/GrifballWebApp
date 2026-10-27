import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, NgModel, Validators } from '@angular/forms';
import { ErrorMessageComponent } from './errorMessage.component';

describe('ErrorMessageComponent', () => {
  let fixture: ComponentFixture<ErrorMessageComponent>;
  let component: ErrorMessageComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ErrorMessageComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ErrorMessageComponent);
    component = fixture.componentInstance;
  });

  function messages(): string[] {
    return Array.from(fixture.nativeElement.querySelectorAll('div') as NodeListOf<HTMLElement>)
      .map(d => d.textContent?.trim() ?? '');
  }

  it('renders nothing when no control is bound', () => {
    fixture.detectChanges();
    expect(component.errorMessage()).toBeNull();
    expect(messages()).toEqual([]);
  });

  it('renders nothing when the bound control is null', () => {
    fixture.componentRef.setInput('control', null);
    fixture.detectChanges();
    expect(component.errorMessage()).toBeNull();
    expect(messages()).toEqual([]);
  });

  it('renders nothing for an invalid control that has not been touched', () => {
    fixture.componentRef.setInput('control', new FormControl('', Validators.required));
    fixture.detectChanges();
    expect(messages()).toEqual([]);
  });

  it('renders nothing for a valid touched control', () => {
    const control = new FormControl('value', Validators.required);
    control.markAsTouched();
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();
    expect(messages()).toEqual([]);
  });

  it('renders the validation message once an invalid control is touched', () => {
    const control = new FormControl('', Validators.required);
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();

    control.markAsTouched();
    fixture.detectChanges();

    expect(messages().length).toBe(1);
    expect(messages()).toEqual(['Required']);
  });

  it('removes the message again when the control becomes valid', () => {
    const control = new FormControl('', Validators.required);
    control.markAsDirty();
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();
    expect(messages().length).toBe(1);

    control.setValue('fixed');
    fixture.detectChanges();
    expect(messages()).toEqual([]);
  });

  it('updates when the bound control changes without a parent re-render', async () => {
    const control = new FormControl('', Validators.required);
    fixture.componentRef.setInput('control', control);
    fixture.autoDetectChanges();
    await fixture.whenStable();
    expect(messages()).toEqual([]);

    control.markAsTouched();
    await fixture.whenStable();
    expect(messages()).toEqual(['Required']);
  });

  it('follows the control of a bound form directive', () => {
    const directive = new NgModel(null!, null!, null!, null!);
    directive.control.setValidators(Validators.required);
    directive.control.updateValueAndValidity();
    directive.control.markAsDirty();
    fixture.componentRef.setInput('control', directive);
    fixture.detectChanges();
    expect(messages()).toEqual(['Required']);

    directive.control.setValue('fixed');
    fixture.detectChanges();
    expect(messages()).toEqual([]);
  });
});
