import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ANTIFORGERY_HEADER, ANTIFORGERY_REJECTED_HEADER, authInterceptor } from './auth.interceptor';
import { ANTIFORGERY_URL, AuthSession } from './authSession.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  const rejected = { status: 400, statusText: 'Bad Request', headers: { [ANTIFORGERY_REJECTED_HEADER]: 'true' } };

  for (const method of ['GET', 'HEAD', 'OPTIONS'] as const) {
    it(`sends a ${method} as it is: no token, no Authorization header`, () => {
      http.request(method, '/api/Season/getCurrentSeasonID/').subscribe();

      const req = backend.expectOne('/api/Season/getCurrentSeasonID/');
      expect(req.request.headers.has(ANTIFORGERY_HEADER)).toBeFalse();
      expect(req.request.headers.has('Authorization')).toBeFalse();
      backend.expectNone(ANTIFORGERY_URL);
      req.flush(12);
    });
  }

  for (const method of ['POST', 'PUT', 'PATCH', 'DELETE'] as const) {
    it(`adds the antiforgery token to a ${method} to the API`, async () => {
      const result = firstValueFrom(http.request(method, '/api/Teams/AddCaptain', { body: {}, responseType: 'text' }));

      backend.expectOne(ANTIFORGERY_URL).flush({ token: 'xsrf-1' });
      const req = backend.expectOne('/api/Teams/AddCaptain');
      expect(req.request.headers.get(ANTIFORGERY_HEADER)).toBe('xsrf-1');
      expect(req.request.headers.has('Authorization')).toBeFalse();
      req.flush('ok');
      expect(await result).toBe('ok');
    });
  }

  it('adds it to relative api/ URLs too', () => {
    http.post('api/Availability/UpdateSeasonAvailability', {}).subscribe();

    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'xsrf-1' });
    expect(backend.expectOne('api/Availability/UpdateSeasonAvailability').request.headers.get(ANTIFORGERY_HEADER)).toBe('xsrf-1');
  });

  it('never sends the token anywhere but the API', () => {
    http.post('https://example.com/api/thing', {}).subscribe();
    http.post('/assets/thing', {}).subscribe();

    backend.expectNone(ANTIFORGERY_URL);
    expect(backend.expectOne('https://example.com/api/thing').request.headers.has(ANTIFORGERY_HEADER)).toBeFalse();
    expect(backend.expectOne('/assets/thing').request.headers.has(ANTIFORGERY_HEADER)).toBeFalse();
  });

  it('fetches the token once for the session and shares it', () => {
    http.post('/api/a', {}).subscribe();
    http.post('/api/b', {}).subscribe();

    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'xsrf-1' });
    expect(backend.expectOne('/api/a').request.headers.get(ANTIFORGERY_HEADER)).toBe('xsrf-1');
    expect(backend.expectOne('/api/b').request.headers.get(ANTIFORGERY_HEADER)).toBe('xsrf-1');

    http.post('/api/c', {}).subscribe();
    backend.expectNone(ANTIFORGERY_URL);
    expect(backend.expectOne('/api/c').request.headers.get(ANTIFORGERY_HEADER)).toBe('xsrf-1');
  });

  it('fetches a new token after a sign-in or sign-out', () => {
    http.post('/api/a', {}).subscribe();
    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'visitor' });
    backend.expectOne('/api/a').flush(null);

    TestBed.inject(AuthSession).signedInOrOut();
    http.post('/api/b', {}).subscribe();

    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'signed-in' });
    expect(backend.expectOne('/api/b').request.headers.get(ANTIFORGERY_HEADER)).toBe('signed-in');
  });

  it('sends the request without a token when it cannot be fetched, and tries again next time', () => {
    http.post('/api/a', {}).subscribe({ error: () => {} });
    backend.expectOne(ANTIFORGERY_URL).flush('down', { status: 503, statusText: 'Unavailable' });
    expect(backend.expectOne('/api/a').request.headers.has(ANTIFORGERY_HEADER)).toBeFalse();

    http.post('/api/b', {}).subscribe();
    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'xsrf-2' });
    expect(backend.expectOne('/api/b').request.headers.get(ANTIFORGERY_HEADER)).toBe('xsrf-2');
  });

  it('sends without a token when the answer has none', () => {
    http.post('/api/a', {}).subscribe();
    backend.expectOne(ANTIFORGERY_URL).flush(null);
    expect(backend.expectOne('/api/a').request.headers.has(ANTIFORGERY_HEADER)).toBeFalse();
  });

  it('fetches a new token and tries once more when the API says the token is stale', async () => {
    const result = firstValueFrom(http.post('/api/a', {}, { responseType: 'text' }));
    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'stale' });
    backend.expectOne('/api/a').flush(null, rejected);

    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'fresh' });
    const retry = backend.expectOne('/api/a');
    expect(retry.request.headers.get(ANTIFORGERY_HEADER)).toBe('fresh');
    retry.flush('done');
    expect(await result).toBe('done');
  });

  it('gives up after one retry', async () => {
    const result = firstValueFrom(http.post('/api/a', {})).catch((e: HttpErrorResponse) => e);
    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'stale' });
    backend.expectOne('/api/a').flush(null, rejected);
    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'still-stale' });
    backend.expectOne('/api/a').flush(null, rejected);

    expect((await result as HttpErrorResponse).status).toBe(400);
    backend.expectNone(ANTIFORGERY_URL);
  });

  it('passes other errors on as they are, without a retry', async () => {
    const result = firstValueFrom(http.post('/api/a', {})).catch((e: HttpErrorResponse) => e);
    backend.expectOne(ANTIFORGERY_URL).flush({ token: 'xsrf' });
    backend.expectOne('/api/a').flush('nope', { status: 400, statusText: 'Bad Request' });

    expect((await result as HttpErrorResponse).status).toBe(400);
    backend.expectNone('/api/a');
  });
});
