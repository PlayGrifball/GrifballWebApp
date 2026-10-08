import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { NgForm } from '@angular/forms';
import { provideLuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';
import { DateTime } from 'luxon';
import { SeasonAvailabilityComponent } from './seasonAvailability.component';
import { AvailabilityOption } from '../../api/dtos/availabilityOption';

describe('SeasonAvailabilityComponent', () => {
  let fixture: ComponentFixture<SeasonAvailabilityComponent>;
  let component: SeasonAvailabilityComponent;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  const option = (dayOfWeek: string, time: string): AvailabilityOption => Object.assign(new AvailabilityOption(), { dayOfWeek, time });

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [SeasonAvailabilityComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideLuxonDatetimeAdapter(),
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SeasonAvailabilityComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('form', new NgForm([], []));
    fixture.componentRef.setInput('seasonID', 14);
  });

  afterEach(() => http.verify());

  function init(options: AvailabilityOption[] = []): void {
    fixture.detectChanges();
    http.expectOne('api/Availability/GetSeasonAvailability?seasonID=14').flush(options);
    fixture.detectChanges();
  }

  it('loads the season availability and renders one row per timeslot', () => {
    init([option('Monday', '19:00'), option('Tuesday', '20:00')]);

    expect(component.options.map(o => o.dayOfWeek)).toEqual(['Monday', 'Tuesday']);
    const deleteButtons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .filter(b => b.textContent!.trim() === 'Delete');
    expect(deleteButtons.length).toBe(2);
  });

  it('reports a failure to load availability', () => {
    fixture.detectChanges();
    http.expectOne('api/Availability/GetSeasonAvailability?seasonID=14').flush('x', { status: 500, statusText: 'Server Error' });

    expect(snackBar.open).toHaveBeenCalledWith('Failed to get season availability');
  });

  it('lists every day of the week, all checked by default', () => {
    expect(component.daysOfWeek()).toEqual(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']);
    expect(component.checkboxes.every(c => c.isChecked)).toBeTrue();
  });

  it('bulk-generates a slot per checked day at each interval between start and end', () => {
    init([option('Monday', 'old')]);
    component.start = DateTime.fromObject({ hour: 19 });
    component.end = DateTime.fromObject({ hour: 20 });
    component.minutes = 30;
    component.checkboxes.forEach(c => c.isChecked = c.name === 'Monday' || c.name === 'Friday');

    component.bulkModify();

    expect(component.options.map(o => `${o.dayOfWeek} ${DateTime.fromISO(o.time).toFormat('HH:mm')}`)).toEqual([
      'Monday 19:00', 'Friday 19:00',
      'Monday 19:30', 'Friday 19:30',
      'Monday 20:00', 'Friday 20:00',
    ]);
  });

  it('produces no slots when no days are checked', () => {
    init();
    component.start = DateTime.fromObject({ hour: 19 });
    component.end = DateTime.fromObject({ hour: 21 });
    component.checkboxes.forEach(c => c.isChecked = false);

    component.bulkModify();
    expect(component.options).toEqual([]);
  });

  it('adds a blank slot at the top and deletes slots by index', () => {
    init([option('Monday', '19:00'), option('Tuesday', '20:00')]);

    component.add();
    expect(component.options.length).toBe(3);
    expect(component.options[0]).toEqual(new AvailabilityOption());

    component.delete(1);
    expect(component.options.map(o => o.dayOfWeek)).toEqual(['', 'Tuesday']);
  });

  it('saves the timeslots for the season', () => {
    init([option('Monday', '19:00')]);
    component.saveChanges();

    const req = http.expectOne('api/Availability/UpdateSeasonAvailability');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ seasonID: 14, timeslots: [option('Monday', '19:00')] });
    req.flush({});
    expect(snackBar.open).toHaveBeenCalledWith('Saved');
  });

  it('reports a failure to save', () => {
    init();
    component.saveChanges();
    http.expectOne('api/Availability/UpdateSeasonAvailability').flush('x', { status: 400, statusText: 'Bad Request' });

    expect(snackBar.open).toHaveBeenCalledWith('Failed to save');
  });
});
