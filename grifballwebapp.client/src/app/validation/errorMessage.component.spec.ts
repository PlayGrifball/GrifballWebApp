import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, Validators } from '@angular/forms';
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
    expect(component.errorMessage).toBeNull();
    expect(messages()).toEqual([]);
  });

  it('renders nothing for an invalid control that has not been touched', () => {
    component.control = new FormControl('', Validators.required);
    fixture.detectChanges();
    expect(messages()).toEqual([]);
  });

  it('renders nothing for a valid touched control', () => {
    const control = new FormControl('value', Validators.required);
    control.markAsTouched();
    component.control = control;
    fixture.detectChanges();
    expect(messages()).toEqual([]);
  });

  it('renders the validation message once an invalid control is touched', () => {
    const control = new FormControl('', Validators.required);
    component.control = control;
    fixture.detectChanges();

    control.markAsTouched();
    // Control state is not a change notification; in forms the blur listener marks the view dirty.
    fixture.componentRef.changeDetectorRef.markForCheck();
    fixture.detectChanges();

    expect(messages().length).toBe(1);
    expect(messages()).toEqual(['Required']);
  });

  it('removes the message again when the control becomes valid', () => {
    const control = new FormControl('', Validators.required);
    control.markAsDirty();
    component.control = control;
    fixture.detectChanges();
    expect(messages().length).toBe(1);

    control.setValue('fixed');
    fixture.componentRef.changeDetectorRef.markForCheck();
    fixture.detectChanges();
    expect(messages()).toEqual([]);
  });
});
