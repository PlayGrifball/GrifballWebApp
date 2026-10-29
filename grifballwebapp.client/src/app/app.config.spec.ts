import { HttpBackend } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { JWT_OPTIONS } from '@auth0/angular-jwt';
import { appConfig } from './app.config';
import { config as serverConfig } from './app.config.server';
import { API_ORIGIN, ServerApiBackend } from './serverApi.backend';

describe('appConfig', () => {
  afterEach(() => localStorage.clear());

  it('gives the JWT helper the stored access token', () => {
    TestBed.configureTestingModule({ providers: appConfig.providers });
    localStorage.setItem('access_token', 'stored');

    expect((TestBed.inject(JWT_OPTIONS) as { tokenGetter: () => string | null }).tokenGetter()).toBe('stored');
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
