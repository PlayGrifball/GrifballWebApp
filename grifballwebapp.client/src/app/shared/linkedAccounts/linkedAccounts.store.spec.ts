import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ExternalLoginDto, ExternalProviderDto } from '@api/models';
import { BROWSER_NAVIGATE, LinkedAccountsStore } from './linkedAccounts.store';

const discord = { loginProvider: 'Discord', providerKey: 'k1', providerDisplayName: 'Discord' } as ExternalLoginDto;
const provider = { name: 'Discord', displayName: 'Discord' } as ExternalProviderDto;
const logins = { hasPassword: true, logins: [discord], providers: null };
const refused = [{ title: 'Refused', detail: 'The only way to sign in.' }, { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } }] as const;

describe('LinkedAccountsStore', () => {
  let store: LinkedAccountsStore;
  let http: HttpTestingController;
  let navigate: jasmine.Spy;
  const userID = signal<number | null>(null);

  beforeEach(() => {
    userID.set(null);
    navigate = jasmine.createSpy('navigate');
    TestBed.configureTestingModule({
      providers: [LinkedAccountsStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: BROWSER_NAVIGATE, useValue: navigate }],
    });
    store = TestBed.inject(LinkedAccountsStore);
    http = TestBed.inject(HttpTestingController);
    store.userID.use(userID);
  });

  afterEach(() => http.verify());

  it("loads the signed-in user's logins, or another user's, and says when it cannot", () => {
    store.load();
    http.expectOne('/api/me/external-logins').flush(logins);
    expect(store.data()!.providers).toEqual([]);

    userID.set(21);
    store.load();
    http.expectOne('/api/users/21/external-logins').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.message()).toBe('Failed to load linked accounts');
  });

  it("goes to the provider to link a login, with the server's token, and says when it cannot", () => {
    store.link(provider);
    expect(store.busy()).toBeTrue();
    http.expectOne({ method: 'POST', url: '/api/me/external-logins/link-token' }).flush({ token: 'a b' });
    expect(navigate).toHaveBeenCalledWith('/api/external-logins/link?provider=Discord&token=a%20b');

    store.link(provider);
    http.expectOne('/api/me/external-logins/link-token').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.busy()).toBeFalse();
    expect(store.message()).toBe('Failed to link Discord');
  });

  it("unlinks the user's own login, or removes another user's, loading the list again", () => {
    store.unlink(discord);
    const unlink = http.expectOne({ method: 'POST', url: '/api/me/external-logins/remove' });
    expect(unlink.request.body).toEqual({ loginProvider: 'Discord', providerKey: 'k1' });
    unlink.flush(null);
    expect(store.message()).toBe('Unlinked Discord');
    http.expectOne('/api/me/external-logins').flush(logins);

    userID.set(21);
    store.unlink(discord);
    http.expectOne({ method: 'POST', url: '/api/users/21/external-logins/remove' }).flush(...refused);
    expect(store.busy()).toBeFalse();
    expect(store.message()).toBe('The only way to sign in.');
  });

  it("adds the login the provider returned, and loads the list whatever the answer", () => {
    store.finishLink();
    http.expectOne({ method: 'POST', url: '/api/me/external-logins' }).flush({ providerDisplayName: 'Discord' });
    expect(store.message()).toBe('Linked Discord');
    http.expectOne('/api/me/external-logins').flush(logins);

    store.finishLink();
    http.expectOne({ method: 'POST', url: '/api/me/external-logins' }).flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.message()).toBe('Failed to link the account');
    http.expectOne('/api/me/external-logins').flush(logins);
  });
});
