import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideLuxonDatetimeAdapter } from '@ng-matero/extensions-luxon-adapter';
import { DateTime } from 'luxon';
import { of, Subject, throwError } from 'rxjs';
import { SeasonEditComponent } from './seasonEdit.component';
import { EventOrganizerService } from '@api/services';
import type { SeasonDto } from '@api/models';
import { SeasonSettings } from '../api/seasonSettings';

describe('SeasonEditComponent', () => {
  let fixture: ComponentFixture<SeasonEditComponent>;
  let component: SeasonEditComponent;
  let api: jasmine.SpyObj<EventOrganizerService>;

  const t = DateTime.fromISO('2026-05-01T19:00:00');

  /** As the API sends it: a season's dates as ISO strings, which the component parses into Luxon DateTimes. */
  const seasonDto = (overrides: Partial<Omit<SeasonDto, 'publicAt'>> & { publicAt?: DateTime | null } = {}): SeasonDto => ({
    seasonID: 4, seasonName: 'Season 4', gameVersionID: 6, leagueID: 1, division: null, format: 'LeagueSeason',
    copyFromSeasonID: null, copyAvailability: false, copySignups: false, copyTeams: false, signupsCount: 0,
    signupsOpen: t.toISO()!, signupsClose: t.toISO()!, draftStart: t.toISO()!, seasonStart: t.toISO()!, seasonEnd: t.toISO()!,
    publicAt: null,
    ...overrides,
    publicAt: overrides.publicAt ? overrides.publicAt.toISO() : null,
  } as SeasonDto);

  /** A parsed season, as the form holds it, for setting the model directly. */
  const season = (overrides: Partial<SeasonSettings> = {}): SeasonSettings => Object.assign(new SeasonSettings(), {
    seasonID: 4, seasonName: 'Season 4', gameVersionID: 6, leagueID: 1,
    signupsOpen: t, signupsClose: t, draftStart: t, seasonStart: t, seasonEnd: t,
    ...overrides
  });

  beforeEach(async () => {
    api = jasmine.createSpyObj<EventOrganizerService>('EventOrganizerService', ['getSeason', 'getSeasons', 'upsertSeason', 'getGameVersions', 'getLeagues']);
    api.getGameVersions.and.returnValue(of([{ gameVersionID: 1, name: 'Halo 3' }, { gameVersionID: 6, name: 'Halo Infinite' }]));
    api.getLeagues.and.returnValue(of([{ leagueID: 1, name: 'AGLA' }, { leagueID: 2, name: 'GWL' }]));
    api.getSeason.and.returnValue(of(seasonDto()));
    api.getSeasons.and.returnValue(of([seasonDto({ seasonID: 3, seasonName: 'Season 3' }), seasonDto()]));

    await TestBed.configureTestingModule({
      imports: [SeasonEditComponent],
      providers: [
        provideRouter([]),
        provideLuxonDatetimeAdapter(),
        { provide: EventOrganizerService, useValue: api },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SeasonEditComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonID', '4');
  });

  async function render(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const saveButton = () => el().querySelector('form button[mat-flat-button]') as HTMLButtonElement;

  /** Opens a select and lists its options. */
  async function openSelect(name: string): Promise<string[]> {
    (el().querySelector(`mat-select[name="${name}"] .mat-mdc-select-trigger`) as HTMLElement).click();
    await fixture.whenStable();
    return Array.from(document.querySelectorAll('mat-option')).map(o => o.textContent!.trim());
  }

  /** Opens a select and picks the option with this text. */
  async function pick(name: string, text: string): Promise<void> {
    await openSelect(name);
    const option = Array.from(document.querySelectorAll<HTMLElement>('mat-option')).find(o => o.textContent!.trim() === text)!;
    option.click();
    await fixture.whenStable();
  }

  /** Opens "Copy from" and picks the season with this name. */
  const pickCopyFrom = (name: string) => pick('copyFromSeasonID', name);

  it('starts a new, invalid season when the id is 0', async () => {
    fixture.componentRef.setInput('seasonID', '0');
    await render();

    expect(api.getSeason).not.toHaveBeenCalled();
    expect(saveButton().textContent).toContain('Create New Season');
    expect(saveButton().disabled).toBeTrue();
    expect(el().textContent).not.toContain('Edit Availability');
  });

  it('loads an existing season and offers to save and edit availability', async () => {
    await render();

    expect(api.getSeason).toHaveBeenCalledWith({ seasonID: 4 });
    expect(component.model().seasonName).toBe('Season 4');
    expect(saveButton().textContent).toContain('Save Season');
    expect(saveButton().disabled).toBeFalse();
    const availability = Array.from(el().querySelectorAll('a')).find(a => a.textContent!.includes('Edit Availability'))!;
    expect(availability.getAttribute('href')).toBe('/seasonAvailability/4');
  });

  it('only shows the copy options when copying from another season', async () => {
    await render();
    expect(el().querySelectorAll('mat-checkbox')).toHaveSize(0);

    component.model.update(model => ({ ...model, copyFromSeasonID: 3 }));
    await render();
    expect(el().querySelectorAll('mat-checkbox')).toHaveSize(3);
  });

  it('does not save an invalid form', async () => {
    fixture.componentRef.setInput('seasonID', '0');
    await render();
    component.onSubmit();
    expect(api.upsertSeason).not.toHaveBeenCalled();
  });

  it('saves a valid season and keeps the returned id', async () => {
    fixture.componentRef.setInput('seasonID', '0');
    api.getSeason.and.returnValue(of(seasonDto()));
    await render();
    component.model.set(season({ seasonID: 0 }));
    await render();
    api.upsertSeason.and.returnValue(of(12));
    const submitted = component.model();

    component.onSubmit();

    expect(api.upsertSeason).toHaveBeenCalledWith({ body: submitted as unknown as SeasonDto });
    expect(component.model().seasonID).toBe(12);
  });

  it('logs a failed save', async () => {
    await render();
    const log = spyOn(console, 'log');
    api.upsertSeason.and.returnValue(throwError(() => 'nope'));

    component.onSubmit();
    expect(log).toHaveBeenCalledWith('nope');
    expect(component.model().seasonID).toBe(4);
  });

  it('fills the form once the season arrives, without a manual detectChanges', async () => {
    const loaded = new Subject<SeasonDto>();
    api.getSeason.and.returnValue(loaded);
    await fixture.whenStable();
    expect(el().textContent).not.toContain('Edit Availability');

    loaded.next(seasonDto({ seasonName: 'Arrived Later' }));
    await fixture.whenStable();

    expect(el().querySelector<HTMLInputElement>('input[name="seasonName"]')?.value).toBe('Arrived Later');
    expect(el().textContent).toContain('Edit Availability');
  });

  it('allows season names up to the 100 characters the database holds', async () => {
    await render();
    const name = el().querySelector<HTMLInputElement>('input[name="seasonName"]')!;
    name.value = 'x'.repeat(100);
    name.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(name.classList).toContain('ng-valid');

    name.value = 'x'.repeat(101);
    name.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(name.classList).toContain('ng-invalid');
  });

  it('replaces the model on form edits instead of mutating the loaded season', async () => {
    await render();
    const loaded = component.model();
    const name = el().querySelector<HTMLInputElement>('input[name="seasonName"]')!;
    name.value = 'Renamed';
    name.dispatchEvent(new Event('input'));
    await pickCopyFrom('Season 3');

    expect(component.model()).not.toBe(loaded);
    expect(component.model().seasonName).toBe('Renamed');
    expect(loaded.seasonName).toBe('Season 4');
    expect(component.model().copyFromSeasonID).toBe(3);
    expect(el().querySelectorAll('mat-checkbox')).toHaveSize(3);
  });

  it('offers the other seasons to copy from, by name, and copying nothing', async () => {
    await render();
    const options = await openSelect('copyFromSeasonID');
    // Not the season being edited (Season 4) itself
    expect(options).toEqual(['Nothing (start empty)', 'Season 3']);
    (document.querySelectorAll('mat-option')[0] as HTMLElement).click();
    await fixture.whenStable();
    expect(component.model().copyFromSeasonID).toBeNull();
    expect(el().querySelectorAll('mat-checkbox')).toHaveSize(0);
  });

  it('still works when the season list fails to load', async () => {
    const err = spyOn(console, 'error');
    api.getSeasons.and.returnValue(throwError(() => 'down'));
    await render();
    expect(err).toHaveBeenCalledWith('down');
    expect(component.copyableSeasons()).toEqual([]);
    expect(saveButton().disabled).toBeFalse();
  });

  it('labels its fields for people, not with the field names', async () => {
    await render();
    const labels = Array.from(el().querySelectorAll('mat-label')).map(l => l.textContent!.trim());
    expect(labels).toEqual(['Season name', 'Game', 'League', 'Division', 'Format', 'Public from', 'Signups open', 'Signups close', 'Draft start', 'Season start', 'Season end', 'Copy from']);
  });

  it('keeps a new season unannounced until a time is set', async () => {
    fixture.componentRef.setInput('seasonID', '0');
    await render();
    expect(component.model().publicAt ?? null).toBeNull();
    expect(el().querySelector<HTMLInputElement>('input[name="publicAt"]')!.required).toBeFalse();
  });

  it('shows and saves when a season is public from', async () => {
    const publicAt = DateTime.fromISO('2026-04-01T12:00:00');
    api.getSeason.and.returnValue(of(seasonDto({ publicAt })));
    api.upsertSeason.and.returnValue(of(4));
    await render();

    component.onSubmit();

    const body = (api.upsertSeason.calls.mostRecent().args[0] as { body: SeasonSettings }).body;
    expect(body.publicAt?.toMillis()).toBe(publicAt.toMillis());
  });

  it('resets the form to the new season when the route moves to another season', async () => {
    api.getSeason.and.callFake((params: { seasonID: number }) => of(seasonDto({ seasonID: params.seasonID, seasonName: `Season ${params.seasonID}` })));
    await render();
    component.patch({ seasonName: 'Unsaved edit' });

    fixture.componentRef.setInput('seasonID', '7');
    await render();

    expect(api.getSeason).toHaveBeenCalledWith({ seasonID: 7 });
    expect(component.model().seasonID).toBe(7);
    expect(el().querySelector<HTMLInputElement>('input[name="seasonName"]')!.value).toBe('Season 7');
    const availability = Array.from(el().querySelectorAll('a')).find(a => a.textContent!.includes('Edit Availability'))!;
    expect(availability.getAttribute('href')).toBe('/seasonAvailability/7');
  });

  it('empties the form for a new season, and drops the previous season when it arrives late', async () => {
    const late = new Subject<SeasonDto>();
    api.getSeason.and.returnValue(late);
    await render();

    fixture.componentRef.setInput('seasonID', '0');
    await render();
    late.next(seasonDto());
    await render();

    expect(component.model().seasonID).toBeUndefined();
    expect(component.model().seasonName).toBeUndefined();
    expect(saveButton().textContent).toContain('Create New Season');
  });

  it('keeps the empty form for a season that does not exist', async () => {
    api.getSeason.and.returnValue(of(null as unknown as SeasonDto));
    await render();
    expect(el().querySelector<HTMLInputElement>('input[name="seasonName"]')!.value).toBe('');
  });

  describe('game, league and division', () => {
    it('starts a new season in the main league, with no game or division, and needs a game to save', async () => {
      fixture.componentRef.setInput('seasonID', '0');
      await render();
      expect(component.model().leagueID).toBe(1);
      expect(component.model().gameVersionID).toBeNull();
      expect(component.model().division).toBeNull();
      expect(component.model().format).toBe('LeagueSeason');

      component.model.set(season({ seasonID: 0, gameVersionID: null }));
      await render();
      expect(saveButton().disabled).withContext('no game yet').toBeTrue();
      component.onSubmit();
      expect(api.upsertSeason).not.toHaveBeenCalled();

      await pick('gameVersionID', 'Halo 3');
      fixture.detectChanges();
      expect(component.model().gameVersionID).toBe(1);
      expect(saveButton().disabled).toBeFalse();
    });

    it('offers the games there are', async () => {
      await render();
      expect(await openSelect('gameVersionID')).toEqual(['Halo 3', 'Halo Infinite']);
    });

    it('offers the leagues there are, and saves the one picked with the game', async () => {
      api.upsertSeason.and.returnValue(of(4));
      await render();
      expect(await openSelect('leagueID')).toEqual(['AGLA', 'GWL']);
      (Array.from(document.querySelectorAll<HTMLElement>('mat-option')).find(o => o.textContent!.trim() === 'GWL')!).click();
      await fixture.whenStable();

      component.onSubmit();
      expect(api.upsertSeason).toHaveBeenCalledWith({ body: jasmine.objectContaining({ gameVersionID: 6, leagueID: 2 }) });
    });

    it('offers the formats, and saves the one picked', async () => {
      api.upsertSeason.and.returnValue(of(4));
      await render();
      expect(await openSelect('format')).toEqual(['League season', 'Tournament']);
      (Array.from(document.querySelectorAll<HTMLElement>('mat-option')).find(o => o.textContent!.trim() === 'Tournament')!).click();
      await fixture.whenStable();

      component.onSubmit();
      expect(api.upsertSeason).toHaveBeenCalledWith({ body: jasmine.objectContaining({ format: 'Tournament' }) });
    });

    it('saves a division, at most 50 characters, and none when cleared', async () => {
      await render();
      const division = el().querySelector<HTMLInputElement>('input[name="division"]')!;
      division.value = 'Pro';
      division.dispatchEvent(new Event('input'));
      await fixture.whenStable();
      expect(component.model().division).toBe('Pro');

      division.value = 'x'.repeat(51);
      division.dispatchEvent(new Event('input'));
      await fixture.whenStable();
      fixture.detectChanges();
      expect(division.classList).toContain('ng-invalid');

      division.value = '';
      division.dispatchEvent(new Event('input'));
      await fixture.whenStable();
      expect(component.model().division).toBeNull();
    });

    it('still works when the games and leagues fail to load', async () => {
      const err = spyOn(console, 'error');
      api.getGameVersions.and.returnValue(throwError(() => 'no games'));
      api.getLeagues.and.returnValue(throwError(() => 'no leagues'));
      await render();
      expect(err).toHaveBeenCalledWith('no games');
      expect(err).toHaveBeenCalledWith('no leagues');
      expect(component.gameVersions()).toEqual([]);
      expect(saveButton().disabled).toBeFalse();
    });
  });

  it("copies from a season by its ID, saying which league a name that several seasons share is in", async () => {
    api.getSeasons.and.returnValue(of([
      seasonDto({ seasonID: 3, seasonName: 'Season 3', leagueID: 1 }),
      seasonDto({ seasonID: 7, seasonName: 'Season 3', leagueID: 2 }),
      seasonDto({ seasonID: 8, seasonName: 'Season 3', leagueID: 9 }),
      seasonDto({ seasonID: 5, seasonName: 'Summer', leagueID: 2 }),
      seasonDto(),
    ]));
    api.upsertSeason.and.returnValue(of(4));
    await render();
    expect(await openSelect('copyFromSeasonID')).toEqual(['Nothing (start empty)', 'Season 3 (AGLA)', 'Season 3 (GWL)', 'Season 3 (League 9)', 'Summer']);
    (Array.from(document.querySelectorAll<HTMLElement>('mat-option')).find(o => o.textContent!.trim() === 'Season 3 (GWL)')!).click();
    await fixture.whenStable();
    expect(component.model().copyFromSeasonID).toBe(7);

    component.onSubmit();
    expect(api.upsertSeason).toHaveBeenCalledWith({ body: jasmine.objectContaining({ copyFromSeasonID: 7 }) });
  });
});
