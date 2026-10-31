import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { missing, SeasonSummaryDto, SeasonSummaryService } from './seasonSummary';

describe('SeasonSummaryService', () => {
  let service: SeasonSummaryService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting()] });
    service = TestBed.inject(SeasonSummaryService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('fetches a season once for every page that asks', () => {
    const names: string[] = [];
    service.get(4).subscribe(s => names.push(s.seasonName));
    service.get(4).subscribe(s => names.push(s.seasonName));
    http.expectOne('/api/Season/getSeasonSummary/4').flush({ seasonName: 'Four' });
    service.get(4).subscribe(s => names.push(s.seasonName));
    expect(names).toEqual(['Four', 'Four', 'Four']);
  });

  it('asks again after a failure', () => {
    service.get(5).subscribe({ error: () => {} });
    http.expectOne('/api/Season/getSeasonSummary/5').flush('', { status: 503, statusText: 'down' });
    service.get(5).subscribe();
    http.expectOne('/api/Season/getSeasonSummary/5').flush({ seasonName: 'Five' });
  });
});

describe('missing', () => {
  const summary = (isHistorical: boolean, signupCount: number) => ({ isHistorical, signupCount } as SeasonSummaryDto);

  it('is only for a backfilled season that has none', () => {
    expect(missing(summary(true, 0), s => s.signupCount)).toBeTrue();
    expect(missing(summary(true, 3), s => s.signupCount)).toBeFalse();
    expect(missing(summary(false, 0), s => s.signupCount)).toBeFalse();
    expect(missing(null, s => s.signupCount)).toBeFalse();
  });
});
