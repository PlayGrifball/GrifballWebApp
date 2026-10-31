import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { missing, SeasonSummaryService } from './seasonSummary';
import { SeasonSummaryDto } from '../api/dtos/seasonSummaryDto';

describe('SeasonSummaryService', () => {
  let service: SeasonSummaryService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting()] });
    service = TestBed.inject(SeasonSummaryService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shares one request between the pages that ask while it is on its way', () => {
    const names: string[] = [];
    service.get(4).subscribe(s => names.push(s.seasonName));
    service.get(4).subscribe(s => names.push(s.seasonName));
    http.expectOne('/api/Season/getSeasonSummary/4').flush({ seasonName: 'Four' });
    expect(names).toEqual(['Four', 'Four']);
  });

  it('asks again once a request is done, so the next page sees the season as it is now', () => {
    const names: string[] = [];
    service.get(4).subscribe(s => names.push(s.seasonName));
    http.expectOne('/api/Season/getSeasonSummary/4').flush({ seasonName: 'Four' });
    service.get(4).subscribe(s => names.push(s.seasonName));
    http.expectOne('/api/Season/getSeasonSummary/4').flush({ seasonName: 'Four (renamed)' });
    expect(names).toEqual(['Four', 'Four (renamed)']);
  });

  it('asks again after a failure', () => {
    service.get(5).subscribe({ error: () => {} });
    http.expectOne('/api/Season/getSeasonSummary/5').flush('', { status: 503, statusText: 'down' });
    service.get(5).subscribe();
    http.expectOne('/api/Season/getSeasonSummary/5').flush({ seasonName: 'Five' });
  });

  it('asks again when every page stopped listening before it arrived', () => {
    const first = service.get(6).subscribe();
    const cancelled = http.expectOne('/api/Season/getSeasonSummary/6');
    first.unsubscribe();
    expect(cancelled.cancelled).toBeTrue();
    service.get(6).subscribe();
    http.expectOne('/api/Season/getSeasonSummary/6').flush({ seasonName: 'Six' });
  });

  it('keeps seasons apart', () => {
    service.get(7).subscribe();
    service.get(8).subscribe();
    http.expectOne('/api/Season/getSeasonSummary/7').flush({});
    http.expectOne('/api/Season/getSeasonSummary/8').flush({});
  });
});

describe('missing', () => {
  const summary = (isHistorical: boolean, hasEnded: boolean, signupCount: number) =>
    ({ isHistorical, hasEnded, signupCount } as SeasonSummaryDto);

  it('is for a backfilled or ended season that has none', () => {
    expect(missing(summary(true, false, 0), s => s.signupCount)).toBeTrue();
    expect(missing(summary(false, true, 0), s => s.signupCount)).toBeTrue();
    expect(missing(summary(true, true, 3), s => s.signupCount)).toBeFalse();
  });

  it('is not for a live season that has none yet, or no summary', () => {
    expect(missing(summary(false, false, 0), s => s.signupCount)).toBeFalse();
    expect(missing(null, s => s.signupCount)).toBeFalse();
    expect(missing(undefined, s => s.signupCount)).toBeFalse();
  });
});
