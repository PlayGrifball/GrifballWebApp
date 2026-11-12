import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DateTime } from 'luxon';
import { SeasonSettingsApiService } from './seasonSettingsApi.service';
import { SeasonSettings } from './seasonSettings';

/** A real event loop turn: long enough for the lazily-imported parser's promise chain to settle. */
const tick = () => new Promise<void>(resolve => setTimeout(resolve));

describe('SeasonSettingsApiService', () => {
  let service: SeasonSettingsApiService;
  let http: HttpTestingController;

  const t = DateTime.fromISO('2026-05-01T19:00:00');
  const dto = (overrides: Partial<Record<string, unknown>> = {}) => ({
    seasonID: 4, seasonName: 'Season 4', gameVersionID: 6, leagueID: 1, division: null, format: 'LeagueSeason',
    copyFromSeasonID: null, copyAvailability: false, copySignups: false, copyTeams: false, signupsCount: 0,
    publicAt: null,
    signupsOpen: t.toISO(), signupsClose: t.toISO(), draftStart: t.toISO(), seasonStart: t.toISO(), seasonEnd: t.toISO(),
    ...overrides,
  });

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting()] });
    service = TestBed.inject(SeasonSettingsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('parses every season\'s dates into Luxon DateTimes', async () => {
    let result: SeasonSettings[] | undefined;
    service.getSeasons().subscribe(seasons => result = seasons);
    http.expectOne('/api/seasons/settings').flush([dto(), dto({ seasonID: 5, seasonName: 'Season 5' })]);
    await tick();

    expect(result).toHaveSize(2);
    expect(result![0].signupsOpen).toEqual(t);
    expect(result![1].seasonName).toBe('Season 5');
  });

  it('gets one season, parsed', async () => {
    let result: SeasonSettings | null | undefined;
    service.getSeason(4).subscribe(season => result = season);
    http.expectOne('/api/seasons/4/settings').flush(dto());
    await tick();

    expect(result?.seasonID).toBe(4);
    expect(result?.signupsOpen).toEqual(t);
  });

  it('is null for a season that does not exist (404)', async () => {
    let result: SeasonSettings | null | undefined;
    service.getSeason(99).subscribe(season => result = season);
    http.expectOne('/api/seasons/99/settings').flush('', { status: 404, statusText: 'Not Found' });
    await tick();

    expect(result).toBeNull();
  });

  it('passes any other error on', () => {
    let failed = false;
    service.getSeason(4).subscribe({ error: () => failed = true });
    http.expectOne('/api/seasons/4/settings').flush('', { status: 500, statusText: 'down' });

    expect(failed).toBeTrue();
  });

  it('saves a season as the editor holds it', () => {
    let savedID: number | undefined;
    const settings = Object.assign(new SeasonSettings(), { seasonID: 0, seasonName: 'New Season', signupsOpen: t, signupsClose: t, draftStart: t, seasonStart: t, seasonEnd: t });
    service.upsertSeason(settings).subscribe(id => savedID = id);

    const req = http.expectOne('/api/seasons/settings');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.seasonName).toBe('New Season');
    req.flush(12);

    expect(savedID).toBe(12);
  });
});
