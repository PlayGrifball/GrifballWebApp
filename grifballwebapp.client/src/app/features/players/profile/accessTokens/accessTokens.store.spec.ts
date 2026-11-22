import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { AccessTokenDto } from '@api/models';
import { AccessTokensStore } from './accessTokens.store';

const listUrl = '/api/identity/access-tokens';
const token = (id: string, changes: Partial<AccessTokenDto> = {}): AccessTokenDto =>
  ({ id, name: 'Claude Code', createdAt: '2026-11-01T12:00:00Z', expiresAt: '2099-01-30T12:00:00Z', scope: 'Mcp', readOnly: false, ...changes });
const problem = (detail: string, status: number) =>
  [{ title: 'Refused', detail }, { status, statusText: 'Refused', headers: { 'Content-Type': 'application/problem+json' } }] as const;

describe('AccessTokensStore', () => {
  let store: AccessTokensStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [AccessTokensStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(AccessTokensStore);
    http = TestBed.inject(HttpTestingController);
    TestBed.tick();
    http.expectOne(listUrl).flush([token('a')]);
    await settle();
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('loads the tokens', () => {
    expect(store.loaded()).toBeTrue();
    expect(store.rows().map(t => t.id)).toEqual(['a']);
    expect(store.failed()).toBeFalse();
  });

  it('makes a token, keeps it to show once, says it is made, and loads the list again', () => {
    const made = jasmine.createSpy('made');
    store.create('Laptop', 30, 'Mcp', false, made);
    expect(store.creating()).toBeTrue();
    const make = http.expectOne({ method: 'POST', url: listUrl });
    expect(make.request.body).toEqual({ name: 'Laptop', days: 30, scope: 'Mcp', readOnly: false });
    make.flush({ accessToken: token('b'), token: 'grif_secret' });

    expect(store.creating()).toBeFalse();
    expect(store.created()).toBe('grif_secret');
    expect(store.createdScope()).toBe('Mcp');
    expect(made).toHaveBeenCalledTimes(1);
    TestBed.tick();
    http.expectOne(listUrl).flush([token('a'), token('b')]);
  });

  it('makes one token at a time, and says why one was refused', () => {
    const made = jasmine.createSpy('made');
    store.create('Laptop', 30, 'Mcp', false, made);
    store.create('Laptop', 30, 'Mcp', false, made);
    http.expectOne({ method: 'POST', url: listUrl }).flush(...problem('You have 10 tokens already: revoke one first.', 400));

    expect(store.creating()).toBeFalse();
    expect(store.created()).toBeNull();
    expect(store.createdScope()).toBeNull();
    expect(made).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('You have 10 tokens already: revoke one first.', 'Close');
  });

  it('makes a token with the chosen scope and read-only flag', () => {
    store.create('Script', 90, 'Api', true);
    const make = http.expectOne({ method: 'POST', url: listUrl });

    expect(make.request.body).toEqual({ name: 'Script', days: 90, scope: 'Api', readOnly: true });
    make.flush({ accessToken: token('b', { scope: 'Api', readOnly: true }), token: 'grif_secret' });
    expect(store.createdScope()).toBe('Api');
    TestBed.tick();
    http.expectOne(listUrl).flush([token('a'), token('b', { scope: 'Api', readOnly: true })]);
  });

  it('revokes a token, says so, and loads the list again', () => {
    store.revoke(token('a'));
    http.expectOne({ method: 'POST', url: `${listUrl}/a/revoke` }).flush(null, { status: 204, statusText: 'No Content' });
    expect(snackBar.open).toHaveBeenCalledWith('Revoked Claude Code.', 'Close');
    TestBed.tick();
    http.expectOne(listUrl).flush([]);
  });

  it('says why a token could not be revoked', () => {
    store.revoke(token('a'));
    http.expectOne(`${listUrl}/a/revoke`).flush(...problem('You have no such access token', 404));
    expect(snackBar.open).toHaveBeenCalledWith('You have no such access token', 'Close');
  });
});
