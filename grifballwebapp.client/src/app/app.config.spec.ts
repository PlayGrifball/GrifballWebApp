import { HttpBackend, HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, PendingTasks } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import { config as serverConfig } from './app.config.server';
import { API_ORIGIN, ServerApiBackend } from './core/server/serverApi.backend';
import { rowEnterAnimation } from './shared/rowEnterAnimation';

describe('appConfig', () => {
  it('sends changes to the API with the antiforgery token, and no bearer token', () => {
    TestBed.configureTestingModule({ providers: [...appConfig.providers, provideHttpClientTesting()] });
    localStorage.setItem('access_token', JSON.stringify({ accessToken: 'old' }));
    const backend = TestBed.inject(HttpTestingController);

    TestBed.inject(HttpClient).post('/api/seasons/1/draft/captains', {}).subscribe();
    backend.expectOne('/api/identity/antiforgery-token').flush({ token: 'xsrf' });
    const request = backend.expectOne('/api/seasons/1/draft/captains').request;

    expect(request.headers.get('X-XSRF-TOKEN')).toBe('xsrf');
    expect(request.headers.has('Authorization')).toBeFalse();
    backend.verify();
    localStorage.clear();
  });

  it('fades in the first rows of a table reached by a client-side navigation, while the navigation keeps the app busy', async () => {
    TestBed.configureTestingModule({ providers: [...appConfig.providers, provideHttpClientTesting()] });
    await TestBed.inject(ApplicationRef).whenStable(); // the first page is in
    TestBed.inject(PendingTasks).add(); // a navigation to a page with a table, as the Router holds one

    const name = TestBed.runInInjectionContext(() => rowEnterAnimation());

    expect(name()).toBe('row-enter');
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
    // eslint-disable-next-line sonarjs/no-clear-text-protocols -- the backend's in-cluster address (plain HTTP), as API_URL gives it
    env.process = { env: { API_URL: 'http://api.internal:8080/' } };
    // eslint-disable-next-line sonarjs/no-clear-text-protocols -- the backend's in-cluster address (plain HTTP), as API_URL gives it
    expect(find(API_ORIGIN)!.useFactory!()).toBe('http://api.internal:8080');
  });

  it('reaches the backend at its Service by default, as the proxy does', () => {
    env.process = { env: {} };
    // eslint-disable-next-line sonarjs/no-clear-text-protocols -- the backend's in-cluster address (plain HTTP), as API_URL gives it
    expect(find(API_ORIGIN)!.useFactory!()).toBe('http://grifballwebapp-server:5295');
  });
});
