import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { ProfileStatsDto } from '@api/models';
import { AccountService } from '@core/auth/account.service';
import { ProfileStore } from './profile.store';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS } from '@core/live/liveReload';

/** The live service as the store sees it: who watches what, and a message to send them. */
class FakeLive {
  readonly watching: { topics: readonly string[], listener: (message: LiveMessage) => void, stopped: boolean }[] = [];

  watch(topics: readonly string[], listener: (message: LiveMessage) => void): () => void {
    const watch = { topics, listener, stopped: false };
    this.watching.push(watch);
    return () => watch.stopped = true;
  }

  /** Sends `message` to those watching its topic; a null topic (the connection came back) to everyone. */
  send(message: LiveMessage): void {
    for (const watch of this.watching.filter(w => !w.stopped && (message.topic === null || w.topics.includes(message.topic))))
      watch.listener(message);
  }

  get active(): (readonly string[])[] {
    return this.watching.filter(w => !w.stopped).map(w => w.topics);
  }
}

describe('ProfileStore', () => {
  let store: ProfileStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let live: FakeLive;
  /** The signed-in user. */
  const me = signal(0);

  const stats = (userID: number, gamertag: string | null) => ({ userID, gamertag, displayName: 'Pete', seasons: [], championships: [] }) as unknown as ProfileStatsDto;

  beforeEach(() => {
    me.set(0);
    snackBar = jasmine.createSpyObj<MatSnackBar>('MatSnackBar', ['open']);
    live = new FakeLive();
    TestBed.configureTestingModule({
      providers: [
        ProfileStore,
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
        { provide: AccountService, useValue: { personID: me } },
        { provide: LiveService, useValue: live },
      ],
    });
    store = TestBed.inject(ProfileStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows a player\'s gamertag and stats, loading until the stats are in', () => {
    store.show(21);
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'Grif' });
    expect(store.statsLoading()).toBeTrue();
    http.expectOne('/api/users/21/stats').flush(stats(21, 'Grif'));
    expect(store.gamertag()).toBe('Grif');
    expect(store.profileStats()!.displayName).toBe('Pete');
    expect(store.statsLoading()).toBeFalse();
    expect(store.settingGamertag()).toBeFalse();
  });

  it('asks you for your gamertag on your own profile without one, never on someone else\'s', () => {
    me.set(21);
    store.show(21);
    http.expectOne('/api/users/21/gamertag').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/users/21/stats').flush(stats(21, null));
    expect(store.settingGamertag()).toBeTrue();

    store.show(22);
    http.expectOne('/api/users/22/gamertag').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/users/22/stats').flush(stats(22, null));
    expect(store.settingGamertag()).toBeFalse();
  });

  it('takes the gamertag from the stats when it has none of its own', () => {
    store.show(21);
    http.expectOne('/api/users/21/gamertag').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/users/21/stats').flush(stats(21, 'Simmons'));
    expect(store.gamertag()).toBe('Simmons');
  });

  it('sets your gamertag, then loads the stats again (it can merge accounts); says why when refused', () => {
    me.set(21);
    store.show(21);
    http.expectOne('/api/users/21/gamertag').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/users/21/stats').flush(stats(21, null));

    store.gamertag.set('Grif');
    store.setGamertag();
    const refused = http.expectOne({ method: 'POST', url: '/api/users/21/gamertag' });
    expect(refused.request.body).toEqual({ gamertag: 'Grif' });
    // A ProblemDetails, as text (the call reads its answer as text)
    refused.flush('{"title":"Request refused","detail":"Taken"}', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('Taken', 'Close');
    expect(store.settingGamertag()).toBeTrue();

    store.setGamertag();
    http.expectOne({ method: 'POST', url: '/api/users/21/gamertag' }).flush(null);
    expect(store.settingGamertag()).toBeFalse();
    http.expectOne('/api/users/21/stats').flush(stats(21, 'Grif'));
  });

  it('drops the previous player and a late answer for them', () => {
    store.show(21);
    const lateTag = http.expectOne('/api/users/21/gamertag');
    const lateStats = http.expectOne('/api/users/21/stats');
    store.show(22);
    lateTag.flush({ gamertag: 'Old' });
    lateStats.flush(stats(21, 'Old'));
    expect(store.gamertag()).toBeNull();
    expect(store.profileStats()).toBeNull();
    http.expectOne('/api/users/22/gamertag').flush({ gamertag: 'New' });
    http.expectOne('/api/users/22/stats').flush(stats(22, 'New'));
    expect(store.gamertag()).toBe('New');
  });

  describe('live', () => {
    beforeEach(() => {
      jasmine.clock().install();
      // No spread: a reload runs LIVE_RELOAD_MS after a change
      spyOn(Math, 'random').and.returnValue(0);
    });
    afterEach(() => jasmine.clock().uninstall());

    it('loads the gamertag and stats again, in place, when the player changes', () => {
      TestBed.tick();
      expect(live.active).toEqual([]);
      store.show(21);
      TestBed.tick();
      http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'Grif' });
      http.expectOne('/api/users/21/stats').flush(stats(21, 'Grif'));
      expect(live.active).toEqual([['player:21']]);

      live.send({ topic: 'player:21' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'Renamed' });
      http.expectOne('/api/users/21/stats').flush(stats(21, 'Renamed'));
      expect(store.gamertag()).toBe('Renamed');
      // Kept shown while it loaded again
      expect(store.statsLoading()).toBeFalse();
    });

    it('does not load the gamertag over one being typed into the prompt, only the stats', () => {
      me.set(21);
      store.show(21);
      TestBed.tick();
      http.expectOne('/api/users/21/gamertag').flush(null, { status: 404, statusText: 'Not Found' });
      http.expectOne('/api/users/21/stats').flush(stats(21, null));
      store.gamertag.set('Gr');

      live.send({ topic: 'player:21' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
      http.expectNone('/api/users/21/gamertag');
      http.expectOne('/api/users/21/stats').flush(stats(21, null));
      expect(store.gamertag()).toBe('Gr');
      expect(store.settingGamertag()).toBeTrue();
    });
  });
});
