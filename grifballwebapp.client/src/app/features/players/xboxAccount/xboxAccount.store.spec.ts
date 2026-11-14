import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { OnDemandJobStatusDto, XboxAccountDto } from '@api/models';
import { PULL_POLL_MS, XboxAccountStore } from './xboxAccount.store';

const XUID = '2535400000000042';
const idle: OnDemandJobStatusDto = { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null };

function account(changes: Partial<XboxAccountDto> = {}): XboxAccountDto {
  return { xboxUserID: Number(XUID), gamertag: 'Pickup Pete', userID: null, gameCount: 1, page: 1, pageSize: 25, games: [], ...changes };
}

describe('XboxAccountStore', () => {
  let store: XboxAccountStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [XboxAccountStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(XboxAccountStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const request = (page = 1) => http.expectOne(r => r.url === `/api/xbox-accounts/${XUID}` && r.params.get('page') === String(page));
  const pullStatus = () => http.expectOne(r => r.method === 'GET' && r.url === `/api/xbox-accounts/${XUID}/pull`);
  /** Shows the account (30 games: two pages), its pull idle. */
  const show = () => {
    store.xuid.use(signal(XUID));
    TestBed.tick();
    pullStatus().flush(idle);
    request().flush(account({ gameCount: 30 }));
  };

  it('asks for nothing until it has the account', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.state()).toBe('loading');
  });

  it('loads the account and its pull, and pages through its games', () => {
    show();
    expect(store.state()).toBe('loaded');
    expect(store.account()!.gamertag).toBe('Pickup Pete');
    expect(store.pull()).toEqual(idle);
    expect(store.hasOlder()).toBeTrue();
    expect(store.hasNewer()).toBeFalse();

    store.older();
    TestBed.tick();
    request(2).flush(account({ gameCount: 30, page: 2 }));
    expect(store.hasOlder()).toBeFalse();
    expect(store.hasNewer()).toBeTrue();

    store.newer();
    store.newer();
    TestBed.tick();
    expect(store.page()).toBe(1);
    request(1).flush(account({ gameCount: 30 }));
  });

  it('cancels the request for a page left before it answers', () => {
    show();
    store.older();
    TestBed.tick();
    const left = request(2);
    store.newer();
    TestBed.tick();
    expect(left.cancelled).toBeTrue();
    request(1).flush(account({ gameCount: 30 }));
  });

  it('tells no such account from a failed request', () => {
    store.xuid.use(signal(XUID));
    TestBed.tick();
    pullStatus().flush('down', { status: 500, statusText: 'Server Error' });
    request().flush(null, { status: 404, statusText: 'Not Found' });
    expect(store.state()).toBe('notFound');
    expect(store.account()).toBeNull();
    expect(store.pull()).toBeNull();

    store.older();
    TestBed.tick();
    request(2).flush(null, { status: 500, statusText: 'Server Error' });
    expect(store.state()).toBe('error');
  });

  it('pulls the games, watches the pull until done, then reads the newest games again', () => {
    jasmine.clock().install();
    try {
      show();
      store.pullGames();
      http.expectOne({ method: 'POST', url: `/api/xbox-accounts/${XUID}/pull` }).flush({ ...idle, state: 'Queued' });
      expect(store.pulling()).toBeTrue();

      jasmine.clock().tick(PULL_POLL_MS);
      pullStatus().flush({ ...idle, state: 'Running' });
      expect(store.pulling()).toBeTrue();
      jasmine.clock().tick(PULL_POLL_MS);
      pullStatus().flush(idle);
      expect(store.pulling()).toBeFalse();
      request(1).flush(account({ gameCount: 31 }));
      expect(store.account()!.gameCount).toBe(31);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('says why a pull could not start, and when it cannot follow one', () => {
    jasmine.clock().install();
    try {
      show();
      store.pullGames();
      http.expectOne(r => r.method === 'POST').flush({ title: 'Forbidden', detail: 'Not allowed' },
        { status: 403, statusText: 'Forbidden', headers: { 'Content-Type': 'application/problem+json' } });
      expect(store.pullError()).toBe('Not allowed');

      store.pullGames();
      expect(store.pullError()).toBeNull();
      http.expectOne(r => r.method === 'POST').flush({ ...idle, state: 'Running' });
      jasmine.clock().tick(PULL_POLL_MS);
      pullStatus().flush('down', { status: 500, statusText: 'Server Error' });
      expect(store.pullError()).toContain("Couldn't tell whether the pull finished");
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
