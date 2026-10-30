import { HttpBackend, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import { config as serverConfig } from './app.config.server';
import { API_ORIGIN, ServerApiBackend } from './serverApi.backend';

describe('appConfig', () => {
  it('sends changes to the API with the antiforgery token, and no bearer token', () => {
    TestBed.configureTestingModule({ providers: [...appConfig.providers, provideHttpClientTesting()] });
    localStorage.setItem('access_token', JSON.stringify({ accessToken: 'old' }));
    const backend = TestBed.inject(HttpTestingController);

    TestBed.inject(HttpClient).post('/api/Teams/AddCaptain', {}).subscribe();
    backend.expectOne('/api/Identity/Antiforgery').flush({ token: 'xsrf' });
    const request = backend.expectOne('/api/Teams/AddCaptain').request;

    expect(request.headers.get('X-XSRF-TOKEN')).toBe('xsrf');
    expect(request.headers.has('Authorization')).toBeFalse();
    backend.verify();
    localStorage.clear();
  });
});

describe('the server config', () => {
  type Provider = { provide?: unknown, useFactory?: () => string, useClass?: unknown };
  const providers = serverConfig.providers as Provider[];
  const find = (token: unknown) => providers.filter(p => p?.provide === token).at(-1);
  const env = (globalThis as { process?: unknown });
  const original = env.process;

  afterEach(() => env.process = original);

  it('sends HttpClient\'s requests through ServerApiBackend', () => {
    expect(find(HttpBackend)?.useClass).toBe(ServerApiBackend);
  });

  it('reaches the backend at API_URL, without a trailing slash', () => {
    env.process = { env: { API_URL: 'http://api.internal:8080/' } };
    expect(find(API_ORIGIN)!.useFactory!()).toBe('http://api.internal:8080');
  });

  it('reaches the backend at its Service by default, as the proxy does', () => {
    env.process = { env: {} };
    expect(find(API_ORIGIN)!.useFactory!()).toBe('http://grifballwebapp-server:5295');
  });
});
