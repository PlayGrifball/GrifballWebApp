import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { HaloStatusDto } from '@api/models';
import { InfiniteClientStore } from './infiniteClient.store';

const gateway = { mode: 'Gateway', healthy: true } as unknown as HaloStatusDto;
const direct = { mode: 'Direct' } as unknown as HaloStatusDto;

describe('InfiniteClientStore', () => {
  let store: InfiniteClientStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    spyOn(console, 'log');
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [InfiniteClientStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(InfiniteClientStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("shows the gateway's status in Gateway mode, and says when it can't be checked again", () => {
    store.loadStatus();
    expect(store.checking()).toBeTrue();
    http.expectOne('/api/halo/status').flush(gateway);
    expect(store.checking()).toBeFalse();
    expect(store.mode()).toBe('Gateway');
    expect(store.gateway()).toEqual(gateway);

    store.loadStatus();
    http.expectOne('/api/halo/status').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.mode()).toBe('Gateway');
    expect(snackBar.open).toHaveBeenCalledWith('Failed to check the Halo gateway', 'Close');
  });

  it("checks the app's own sign-in in Direct mode, once", () => {
    store.loadStatus();
    http.expectOne('/api/halo/status').flush(direct);
    http.expectOne('/api/halo/check').flush('Token valid');
    expect(store.status()).toBe('Token valid');

    store.loadStatus();
    http.expectOne('/api/halo/status').flush(direct);
    http.expectNone('/api/halo/check');
  });

  it('takes a server that cannot say for Direct mode, and says when the sign-in cannot be checked', () => {
    store.loadStatus();
    http.expectOne('/api/halo/status').flush('x', { status: 404, statusText: 'Not Found' });
    expect(store.mode()).toBe('Direct');
    http.expectOne('/api/halo/check').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.status()).toBe('Failed to check status');
    expect(snackBar.open).toHaveBeenCalledWith('Failed to check status', 'Close');
  });

  it('sets the code in the body, and says how it went', () => {
    store.setCode('abc');
    const set = http.expectOne({ method: 'POST', url: '/api/halo/auth-code' });
    expect(set.request.body).toEqual({ code: 'abc' });
    set.flush({});
    expect(snackBar.open).toHaveBeenCalledWith('Success', 'Close');

    store.setCode('abc');
    http.expectOne('/api/halo/auth-code').flush('x', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed to set code', 'Close');
  });

  it('deletes the tokens, and says how it went', () => {
    store.deleteTokens();
    http.expectOne({ method: 'POST', url: '/api/halo/tokens/delete' }).flush({});
    expect(snackBar.open).toHaveBeenCalledWith('Success', 'Close');

    store.deleteTokens();
    http.expectOne('/api/halo/tokens/delete').flush('x', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed to delete tokens', 'Close');
  });
});
