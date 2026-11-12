import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, RouterLink } from '@angular/router';
import { By } from '@angular/platform-browser';
import { of, Subject, throwError } from 'rxjs';
import { SeasonManagerComponent } from './seasonManager.component';
import { EventOrganizerService } from '@api/services';
import type { SeasonDto } from '@api/models';

describe('SeasonManagerComponent', () => {
  let fixture: ComponentFixture<SeasonManagerComponent>;
  let api: jasmine.SpyObj<EventOrganizerService>;

  // As the API sends it: dates as ISO strings, parsed into Luxon DateTimes by the component.
  const season = (seasonID: number, seasonName: string): SeasonDto => ({
    seasonID, seasonName, signupsCount: seasonID * 10, signupsOpen: '2026-01-02T03:04:00',
    signupsClose: '2026-01-02T03:04:00', draftStart: '2026-01-02T03:04:00', seasonStart: '2026-01-02T03:04:00', seasonEnd: '2026-01-02T03:04:00',
    publicAt: null, gameVersionID: null, leagueID: 1, division: null, format: 'LeagueSeason', copyFromSeasonID: null,
    copyAvailability: false, copySignups: false, copyTeams: false,
  });

  beforeEach(async () => {
    api = jasmine.createSpyObj<EventOrganizerService>('EventOrganizerService', ['getSeasons']);
    await TestBed.configureTestingModule({
      imports: [SeasonManagerComponent],
      providers: [provideRouter([]), { provide: EventOrganizerService, useValue: api }]
    }).compileComponents();
    fixture = TestBed.createComponent(SeasonManagerComponent);
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('lists each season with its signup count and an edit link', async () => {
    api.getSeasons.and.returnValue(of([season(1, 'Season 1'), season(2, 'Season 2')]));
    fixture.detectChanges();
    // The parser (and Luxon) load lazily, so the rows arrive a tick after the API answers.
    await fixture.whenStable();
    fixture.detectChanges();

    const rows = Array.from(el().querySelectorAll('tr.mat-mdc-row'));
    expect(rows).toHaveSize(2);
    const cells = Array.from(rows[1].querySelectorAll('td')).map(td => td.textContent!.trim());
    expect(cells[0]).toBe('Season 2');
    expect(cells[1]).toMatch(/^Jan 2, 2026, 3:04\sAM$/);
    expect(cells[6]).toBe('20');
    // The name links to the season's page; Edit to its editor
    expect(Array.from(rows[1].querySelectorAll('a')).map(a => a.getAttribute('href'))).toEqual(['/season/2', '/seasonEdit/2']);
  });

  it('links "Create New Season" to a new season edit page', async () => {
    api.getSeasons.and.returnValue(of([]));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const create = fixture.debugElement.query(By.css('a[mat-flat-button]'));
    expect(create.nativeElement.textContent).toContain('Create New Season');
    expect(create.injector.get(RouterLink).urlTree!.toString()).toBe('/seasonEdit/0');
  });

  it('logs and shows no rows when loading fails', async () => {
    const err = spyOn(console, 'error');
    api.getSeasons.and.returnValue(throwError(() => 'boom'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(err).toHaveBeenCalledWith('boom');
    expect(el().querySelectorAll('tr.mat-mdc-row')).toHaveSize(0);
    expect(el().querySelector('[role="alert"]')!.textContent).toContain('Could not load the seasons');
  });

  it('shows a loading state, then an empty state when there are no seasons', async () => {
    const seasons = new Subject<SeasonDto[]>();
    api.getSeasons.and.returnValue(seasons);
    await fixture.whenStable();
    expect(el().querySelector('[role="status"]')!.textContent).toContain('Loading seasons');

    seasons.next([]);
    await fixture.whenStable();
    expect(el().querySelector('[role="status"]')).toBeNull();
    expect(el().textContent).toContain('No seasons yet');
    expect(el().querySelector('table')).toBeNull();
  });

  it('labels its columns for people, not with the field names', async () => {
    api.getSeasons.and.returnValue(of([season(1, 'Season 1')]));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const headers = Array.from(el().querySelectorAll('th')).map(th => th.textContent!.trim());
    expect(headers).toEqual(['Season', 'Signups open', 'Signups close', 'Draft', 'Start', 'End', 'Signups', 'Edit']);
  });

  it('renders the seasons once they arrive, without a manual detectChanges', async () => {
    const seasons = new Subject<SeasonDto[]>();
    api.getSeasons.and.returnValue(seasons);
    await fixture.whenStable();

    seasons.next([season(1, 'Season 1')]);
    await fixture.whenStable();

    expect(el().querySelectorAll('tr.mat-mdc-row')).toHaveSize(1);
  });
});
