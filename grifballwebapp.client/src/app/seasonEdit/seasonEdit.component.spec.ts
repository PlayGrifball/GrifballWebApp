import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideLuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';
import { DateTime } from 'luxon';
import { of, throwError } from 'rxjs';
import { SeasonEditComponent } from './seasonEdit.component';
import { ApiClientService } from '../api/apiClient.service';
import { SeasonDto } from '../api/dtos/seasonDto';

describe('SeasonEditComponent', () => {
  let fixture: ComponentFixture<SeasonEditComponent>;
  let component: SeasonEditComponent;
  let api: jasmine.SpyObj<ApiClientService>;
  let seasonID: string;

  const t = DateTime.fromISO('2026-05-01T19:00:00');
  const season = (overrides: Partial<SeasonDto> = {}): SeasonDto => Object.assign(new SeasonDto(), {
    seasonID: 4, seasonName: 'Season 4',
    signupsOpen: t, signupsClose: t, draftStart: t, seasonStart: t, seasonEnd: t,
    ...overrides
  });

  beforeEach(async () => {
    seasonID = '4';
    api = jasmine.createSpyObj('ApiClientService', ['getSeason', 'upsertSeason']);
    api.getSeason.and.returnValue(of(season()));

    await TestBed.configureTestingModule({
      imports: [SeasonEditComponent],
      providers: [
        provideRouter([]),
        provideLuxonDatetimeAdapter(),
        { provide: ApiClientService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => seasonID } } } },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SeasonEditComponent);
    component = fixture.componentInstance;
  });

  async function render(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const saveButton = () => el().querySelector('form button[mat-flat-button]') as HTMLButtonElement;

  it('starts a new, invalid season when the id is 0', async () => {
    seasonID = '0';
    await render();

    expect(api.getSeason).not.toHaveBeenCalled();
    expect(saveButton().textContent).toContain('Create New Season');
    expect(saveButton().disabled).toBeTrue();
    expect(el().textContent).not.toContain('Edit Availability');
  });

  it('loads an existing season and offers to save and edit availability', async () => {
    await render();

    expect(api.getSeason).toHaveBeenCalledWith(4);
    expect(component.model.seasonName).toBe('Season 4');
    expect(saveButton().textContent).toContain('Save Season');
    expect(saveButton().disabled).toBeFalse();
    const availability = Array.from(el().querySelectorAll('button')).find(b => b.textContent!.includes('Edit Availability'))!;
    expect(availability).toBeDefined();
  });

  it('only shows the copy options when copying from another season', async () => {
    await render();
    expect(el().querySelectorAll('mat-checkbox').length).toBe(0);

    component.model.copyFrom = '3';
    await render();
    expect(el().querySelectorAll('mat-checkbox').length).toBe(3);
  });

  it('does not save an invalid form', async () => {
    seasonID = '0';
    await render();
    component.onSubmit();
    expect(api.upsertSeason).not.toHaveBeenCalled();
  });

  it('saves a valid season and keeps the returned id', async () => {
    seasonID = '0';
    api.getSeason.and.returnValue(of(season()));
    await render();
    component.model = season({ seasonID: 0 });
    await render();
    api.upsertSeason.and.returnValue(of(12));

    component.onSubmit();

    expect(api.upsertSeason).toHaveBeenCalledWith(component.model);
    expect(component.model.seasonID).toBe(12);
  });

  it('logs a failed save', async () => {
    await render();
    const log = spyOn(console, 'log');
    api.upsertSeason.and.returnValue(throwError(() => 'nope'));

    component.onSubmit();
    expect(log).toHaveBeenCalledWith('nope');
    expect(component.model.seasonID).toBe(4);
  });
});
