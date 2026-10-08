import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectorRef } from '@angular/core';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ExcelFormComponent } from './excelForm.component';

describe('ExcelFormComponent', () => {
  let fixture: ComponentFixture<ExcelFormComponent>;
  let component: ExcelFormComponent;
  let http: HttpTestingController;

  const guid = '0f8fad5b-d9cb-469f-a165-70867728950e';

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExcelFormComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ExcelFormComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('inputName', 'Season Stats');
    fixture.componentRef.setInput('inputSpreadsheetId', 'sheet-1');
    fixture.componentRef.setInput('inputSheetName', 'Week 1');
    fixture.detectChanges();
    await fixture.whenStable();
    // OnPush: the form registers its controls asynchronously, so re-render once they exist.
    fixture.componentRef.injector.get(ChangeDetectorRef).markForCheck();
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const submit = () => el().querySelector('button') as HTMLButtonElement;

  async function typeMatchID(value: string): Promise<void> {
    const input = el().querySelector('input[name="MatchID"]') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows the sheet name and pre-fills the spreadsheet fields from the inputs', () => {
    expect(el().querySelector('h2')!.textContent).toBe('Season Stats');
    expect(component.spreadsheetID).toBe('sheet-1');
    expect(component.sheetName).toBe('Week 1');
  });

  it('requires a GUID match id before enabling submit', async () => {
    expect(submit().disabled).toBeTrue();

    await typeMatchID('not-a-guid');
    expect(submit().disabled).toBeTrue();
    expect(el().textContent).toContain('Must be a valid GUID');

    await typeMatchID(guid);
    expect(submit().disabled).toBeFalse();
  });

  it('appends the match to the sheet and shows a spinner while submitting', () => {
    spyOn(console, 'log');
    component.sheetName = 'Week 2';
    component.onSubmit(guid);
    fixture.detectChanges();

    expect(el().querySelector('mat-spinner')).not.toBeNull();
    expect(el().querySelector('form')).toBeNull();

    const req = http.expectOne(r => r.url === '/api/Excel/AppendMatch/');
    expect(req.request.method).toBe('POST');
    expect(req.request.params.get('matchID')).toBe(guid);
    expect(req.request.body).toEqual({ name: 'Season Stats', spreadsheetID: 'sheet-1', sheetName: 'Week 2' });
    req.flush({});
    fixture.detectChanges();

    expect(component.isSubmittingMatch()).toBeFalse();
    expect(el().querySelector('form')).not.toBeNull();
  });

  it('clears the submitting state after a failure too', () => {
    spyOn(console, 'log');
    component.onSubmit(guid);
    http.expectOne(r => r.url === '/api/Excel/AppendMatch/').flush('x', { status: 500, statusText: 'Server Error' });

    expect(component.isSubmittingMatch()).toBeFalse();
  });
});
