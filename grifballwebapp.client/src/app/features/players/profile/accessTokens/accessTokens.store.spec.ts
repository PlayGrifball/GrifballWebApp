import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { AccessTokenDto } from '@api/models';
import { AccessTokensStore } from './accessTokens.store';

const listUrl = '/api/identity/access-tokens';
const token = (id: string): AccessTokenDto => ({ id, name: 'Claude Code', createdAt: '2026-11-01T12:00:00Z', expiresAt: '2099-01-30T12:00:00Z' });
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
    store.create('Laptop', 30, made);
    expect(store.creating()).toBeTrue();
    const make = http.expectOne({ method: 'POST', url: listUrl });
    expect(make.request.body).toEqual({ name: 'Laptop', days: 30 });
    make.flush({ accessToken: token('b'), token: 'grif_secret' });

    expect(store.creating()).toBeFalse();
    expect(store.created()).toBe('grif_secret');
    expect(made).toHaveBeenCalledTimes(1);
    TestBed.tick();
    http.expectOne(listUrl).flush([token('a'), token('b')]);
  });

  it('makes one token at a time, and says why one was refused', () => {
    const made = jasmine.createSpy('made');
    store.create('Laptop', 30, made);
    store.create('Laptop', 30, made);
    http.expectOne({ method: 'POST', url: listUrl }).flush(...problem('You have 10 tokens already: revoke one first.', 400));

    expect(store.creating()).toBeFalse();
    expect(store.created()).toBeNull();
    expect(made).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('You have 10 tokens already: revoke one first.', 'Close');
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
