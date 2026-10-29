import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AccountService } from './account.service';
import { EARLY_FETCH_MAX_AGE_MS, EarlyResponse, earlyFetchInterceptor } from './earlyFetch.interceptor';

describe('earlyFetchInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  const accessToken = signal<string | undefined>(undefined);

  /** Stands in for the inline script in index.html. */
  function early(responses: Record<string, EarlyResponse | null>, { token = null as string | null, at = Date.now() } = {}): void {
    window.grifEarly = {
      at,
      token,
      responses: Object.fromEntries(Object.entries(responses).map(([url, r]) => [url, Promise.resolve(r)])),
    };
  }

  const json = (body: unknown, status = 200): EarlyResponse => ({ status, contentType: 'application/json', body: JSON.stringify(body) });

  beforeEach(() => {
    accessToken.set(undefined);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([earlyFetchInterceptor])),
        provideHttpClientTesting(),
        { provide: AccountService, useValue: { accessToken } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    delete window.grifEarly;
    backend.verify();
  });

  it('sends requests as usual when nothing was fetched early', async () => {
    const result = firstValueFrom(http.get('/api/Home/CurrentAndFutureEvents/'));
    backend.expectOne('/api/Home/CurrentAndFutureEvents/').flush([1]);
    expect(await result).toEqual([1]);
  });

  it('answers the first request for a URL with the early response, without asking the server', async () => {
    early({ '/api/Home/CurrentAndFutureEvents/': json([{ seasonID: 12 }]) });

    expect(await firstValueFrom(http.get('/api/Home/CurrentAndFutureEvents/'))).toEqual([{ seasonID: 12 }]);
    backend.expectNone('/api/Home/CurrentAndFutureEvents/');
  });

  it('uses each early response only once', async () => {
    early({ '/api/Season/getCurrentSeasonID/': json(12) });
    await firstValueFrom(http.get('/api/Season/getCurrentSeasonID/'));

    const again = firstValueFrom(http.get('/api/Season/getCurrentSeasonID/'));
    backend.expectOne('/api/Season/getCurrentSeasonID/').flush(13);
    expect(await again).toBe(13);
  });

  it('matches relative URLs and query parameters as the page sent them', async () => {
    early({
      '/api/profile/getgamertag/117': json({ gamertag: 'MasterChief117' }),
      '/api/Home/PastSeasons?pageSize=10&pageNumber=1': json({ totalCount: 0, results: [] }),
    });

    expect(await firstValueFrom(http.get('api/profile/getgamertag/117'))).toEqual({ gamertag: 'MasterChief117' });
    expect(await firstValueFrom(http.get('/api/Home/PastSeasons', { params: { pageSize: 10, pageNumber: 1 } })))
      .toEqual({ totalCount: 0, results: [] });
  });

  it('does not answer a request with other query parameters', async () => {
    early({ '/api/Home/PastSeasons?pageSize=10&pageNumber=1': json({ totalCount: 0, results: [] }) });

    const result = firstValueFrom(http.get('/api/Home/PastSeasons', { params: { pageSize: 10, pageNumber: 2 } }));
    backend.expectOne('/api/Home/PastSeasons?pageSize=10&pageNumber=2').flush({ totalCount: 1, results: [] });
    expect(await result).toEqual({ totalCount: 1, results: [] });
  });

  it('gives text as text and an empty JSON body (204) as null, as HttpClient does', async () => {
    early({
      '/api/CommitHash': { status: 200, contentType: 'text/plain', body: '9f3c2ab' },
      '/api/Season/getSeasonName/12': { status: 200, contentType: 'text/plain', body: 'Season 12' },
      '/api/Signups/getSignup/13': { status: 204, contentType: null, body: '' },
    });

    expect(await firstValueFrom(http.get('/api/CommitHash', { responseType: 'text' }))).toBe('9f3c2ab');
    expect(await firstValueFrom(http.get('/api/Season/getSeasonName/12', { responseType: 'text' }))).toBe('Season 12');
    expect(await firstValueFrom(http.get('/api/Signups/getSignup/13'))).toBeNull();
  });

  it('keeps the status and headers of the early response', async () => {
    early({ '/api/Season/getCurrentSeasonID/': json(12) });

    const response = await firstValueFrom(http.get('/api/Season/getCurrentSeasonID/', { observe: 'response' }));
    expect(response.status).toBe(200);
    expect(response.headers.get('Content-Type')).toBe('application/json');
    expect(response.url).toBe('/api/Season/getCurrentSeasonID/');
  });

  for (const [what, response] of [
    ['failed', null],
    ['was an error', json('boom', 500)],
    ['was not JSON', { status: 200, contentType: 'text/html', body: '<!doctype html>' }],
  ] as [string, EarlyResponse | null][]) {
    it(`asks the server again when the early fetch ${what}`, async () => {
      early({ '/api/Home/CurrentAndFutureEvents/': response });

      const result = firstValueFrom(http.get('/api/Home/CurrentAndFutureEvents/'));
      await new Promise(resolve => setTimeout(resolve));
      backend.expectOne('/api/Home/CurrentAndFutureEvents/').flush([2]);
      expect(await result).toEqual([2]);
    });
  }

  it('asks the server again for a response type it does not keep (blob)', async () => {
    early({ '/api/Excel/Export': json('x') });

    const result = firstValueFrom(http.get('/api/Excel/Export', { responseType: 'blob' }));
    await new Promise(resolve => setTimeout(resolve));
    backend.expectOne('/api/Excel/Export').flush(new Blob(['x']));
    expect(await result).toBeInstanceOf(Blob);
  });

  it('does not use an early response that is too old', () => {
    early({ '/api/Season/getCurrentSeasonID/': json(12) }, { at: Date.now() - EARLY_FETCH_MAX_AGE_MS - 1 });

    http.get('/api/Season/getCurrentSeasonID/').subscribe();
    backend.expectOne('/api/Season/getCurrentSeasonID/').flush(12);
  });

  it('does not use an early response fetched for another user', () => {
    accessToken.set('token-of-now');
    early({ '/api/identity/metaInfo': json({ userID: 1 }) }, { token: 'token-at-load' });

    http.get('/api/identity/metaInfo').subscribe();
    backend.expectOne('/api/identity/metaInfo').flush({ userID: 2 });
  });

  it('uses an early response fetched with the same access token', async () => {
    accessToken.set('token-1');
    early({ '/api/identity/metaInfo': json({ userID: 1 }) }, { token: 'token-1' });

    expect(await firstValueFrom(http.get('/api/identity/metaInfo'))).toEqual({ userID: 1 });
  });

  it('never answers a request that is not a GET', () => {
    early({ '/api/Season/getCurrentSeasonID/': json(12) });

    http.post('/api/Season/getCurrentSeasonID/', {}).subscribe();
    backend.expectOne('/api/Season/getCurrentSeasonID/').flush(12);
    expect(window.grifEarly!.responses['/api/Season/getCurrentSeasonID/']).toBeDefined();
  });
});
