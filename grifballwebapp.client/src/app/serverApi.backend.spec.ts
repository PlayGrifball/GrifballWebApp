import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { REQUEST } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { lastValueFrom } from 'rxjs';
import { API_ORIGIN, SERVER_API_TIMEOUT_MS, ServerApiBackend } from './serverApi.backend';

describe('ServerApiBackend', () => {
  let fetch: jasmine.Spy;

  function create(page: Request | null): ServerApiBackend {
    TestBed.configureTestingModule({
      providers: [
        ServerApiBackend,
        { provide: API_ORIGIN, useValue: 'http://backend:5295' },
        { provide: REQUEST, useValue: page },
      ],
    });
    return TestBed.inject(ServerApiBackend);
  }

  // Not a real Request: a browser's drops a Cookie header, a server's keeps it.
  const page = (headers: Record<string, string> = {}) =>
    ({ url: 'https://grifball.xyz/season/12', headers: new Headers(headers) }) as unknown as Request;
  const sent = () => fetch.calls.mostRecent().args as [string, RequestInit];
  const body = (response: unknown) => (response as HttpResponse<unknown>).body;

  beforeEach(() => {
    fetch = spyOn(globalThis, 'fetch').and.callFake(async () =>
      new Response('{"ok":true}', { status: 200, headers: { 'Content-Type': 'application/json' } }));
  });

  it('sends the app\'s /api calls straight to the backend, without the prefix', async () => {
    const backend = create(page());

    const events = await lastValueFrom(backend.handle(new HttpRequest('GET', 'https://grifball.xyz/api/Season/getSeasonName/12?x=1')));

    expect(sent()[0]).toBe('http://backend:5295/Season/getSeasonName/12?x=1');
    expect(body(events)).toEqual({ ok: true });
  });

  it('passes the visitor\'s cookies and address on, and not other headers', async () => {
    const backend = create(page({ cookie: 'a=b', 'x-real-ip': '203.0.113.9', 'x-forwarded-for': '203.0.113.9', 'user-agent': 'test' }));

    await lastValueFrom(backend.handle(new HttpRequest('GET', 'https://grifball.xyz/api/CommitHash', { responseType: 'text' })));

    const headers = new Headers(sent()[1].headers);
    expect(headers.get('cookie')).toBe('a=b');
    expect(headers.get('x-real-ip')).toBe('203.0.113.9');
    expect(headers.get('x-forwarded-for')).toBe('203.0.113.9');
    expect(headers.get('user-agent')).toBeNull();
  });

  it('keeps a header the request already has', async () => {
    const backend = create(page({ cookie: 'from-page=1' }));
    const req = new HttpRequest('GET', 'https://grifball.xyz/api/CommitHash').clone({ setHeaders: { cookie: 'own=1' } });

    await lastValueFrom(backend.handle(req));

    expect(new Headers(sent()[1].headers).get('cookie')).toBe('own=1');
  });

  for (const [what, url] of [
    ['another host', 'https://example.com/api/CommitHash'],
    ['a path outside /api', 'https://grifball.xyz/assets/og-image.png'],
  ]) {
    it(`sends a request for ${what} as it is`, async () => {
      const backend = create(page());
      await lastValueFrom(backend.handle(new HttpRequest('GET', url)));
      expect(sent()[0]).toBe(url);
    });
  }

  it('sends requests as they are when there is no page request (e.g. outside a render)', async () => {
    const backend = create(null);
    await lastValueFrom(backend.handle(new HttpRequest('GET', 'https://grifball.xyz/api/CommitHash')));
    expect(sent()[0]).toBe('https://grifball.xyz/api/CommitHash');
  });

  it('gives up on an answer after a while, as a 504, so the page renders without it', () => {
    jasmine.clock().install();
    try {
      fetch.and.returnValue(new Promise<Response>(() => { }));
      const backend = create(page());
      let error: unknown;
      backend.handle(new HttpRequest('GET', 'https://grifball.xyz/api/SeasonMatch/GetPossibleMatches/1')).subscribe({ error: e => error = e });

      jasmine.clock().tick(SERVER_API_TIMEOUT_MS - 1);
      expect(error).toBeUndefined();
      jasmine.clock().tick(2);

      expect(error).toEqual(jasmine.any(HttpErrorResponse));
      expect((error as HttpErrorResponse).status).toBe(504);
      expect((error as HttpErrorResponse).url).toBe('http://backend:5295/SeasonMatch/GetPossibleMatches/1');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('passes other failures on as they are', async () => {
    fetch.and.rejectWith(new TypeError('fetch failed'));
    const backend = create(page());

    const error = await lastValueFrom(backend.handle(new HttpRequest('GET', 'https://grifball.xyz/api/CommitHash'))).catch(e => e);

    expect(error).toEqual(jasmine.any(HttpErrorResponse));
    expect((error as HttpErrorResponse).status).toBe(0);
  });
});
