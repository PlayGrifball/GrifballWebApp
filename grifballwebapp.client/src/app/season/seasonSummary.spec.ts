import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { missing, SeasonSummaryService } from './seasonSummary';
import type { SeasonSummaryDto } from '@api/models';

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
    http.expectOne('/api/seasons/4/summary').flush({ seasonName: 'Four' });
    expect(names).toEqual(['Four', 'Four']);
  });

  it('asks again once a request is done, so the next page sees the season as it is now', () => {
    const names: string[] = [];
    service.get(4).subscribe(s => names.push(s.seasonName));
    http.expectOne('/api/seasons/4/summary').flush({ seasonName: 'Four' });
    service.get(4).subscribe(s => names.push(s.seasonName));
    http.expectOne('/api/seasons/4/summary').flush({ seasonName: 'Four (renamed)' });
    expect(names).toEqual(['Four', 'Four (renamed)']);
  });

  it('asks again after a failure', () => {
    let failed = false;
    service.get(5).subscribe({ error: () => failed = true });
    http.expectOne('/api/seasons/5/summary').flush('', { status: 503, statusText: 'down' });
    let name: string | undefined;
    service.get(5).subscribe(s => name = s.seasonName);
    http.expectOne('/api/seasons/5/summary').flush({ seasonName: 'Five' });
    expect(failed).toBeTrue();
    expect(name).toBe('Five');
  });

  it('asks again when every page stopped listening before it arrived', () => {
    const first = service.get(6).subscribe();
    const cancelled = http.expectOne('/api/seasons/6/summary');
    first.unsubscribe();
    expect(cancelled.cancelled).toBeTrue();
    service.get(6).subscribe();
    http.expectOne('/api/seasons/6/summary').flush({ seasonName: 'Six' });
  });

  it('keeps seasons apart', () => {
    const names: string[] = [];
    service.get(7).subscribe(s => names.push('7: ' + s.seasonName));
    service.get(8).subscribe(s => names.push('8: ' + s.seasonName));
    http.expectOne('/api/seasons/7/summary').flush({ seasonName: 'Seven' });
    http.expectOne('/api/seasons/8/summary').flush({ seasonName: 'Eight' });
    expect(names).toEqual(['7: Seven', '8: Eight']);
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
