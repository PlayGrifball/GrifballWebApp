import { Injectable, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LiveMessage, LiveService } from './live.service';
import { LIVE_RELOAD_MS, LIVE_SPREAD_MS, liveData, liveReload } from './liveReload';

/** The live service as liveReload sees it: who watches what, and a change to send them. */
class FakeLive {
  readonly watching: { topics: readonly string[], listener: (message: LiveMessage) => void, stopped: boolean }[] = [];

  watch(topics: readonly string[], listener: (message: LiveMessage) => void): () => void {
    const watch = { topics, listener, stopped: false };
    this.watching.push(watch);
    return () => watch.stopped = true;
  }

  change(message: LiveMessage = { topic: 'season:1' }): void {
    for (const watch of this.watching.filter(w => !w.stopped))
      watch.listener(message);
  }

  get active(): (readonly string[])[] {
    return this.watching.filter(w => !w.stopped).map(w => w.topics);
  }
}

@Injectable()
class WatchingStore {
  readonly topics = signal<readonly string[]>(['season:1']);
  reloads = 0;

  constructor() {
    liveReload(() => this.topics(), () => this.reloads++);
  }
}

/** A store whose topic comes with its data: it takes a count, and refuses a negative one (then it reads). */
@Injectable()
class DataStore {
  taken: number[] = [];
  reloads = 0;

  constructor() {
    liveData<{ count: number }>(() => ['queue'], data => {
      if (data.count < 0)
        return false;
      this.taken.push(data.count);
      return true;
    }, () => this.reloads++);
  }
}

describe('liveReload', () => {
  let live: FakeLive;
  let store: WatchingStore;

  beforeEach(() => {
    jasmine.clock().install();
    spyOn(Math, 'random').and.returnValue(0);
    live = new FakeLive();
    TestBed.configureTestingModule({ providers: [WatchingStore, { provide: LiveService, useValue: live }] });
    store = TestBed.inject(WatchingStore);
    TestBed.tick();
  });

  afterEach(() => jasmine.clock().uninstall());

  it('watches the store\'s topics', () => {
    expect(live.active).toEqual([['season:1']]);
  });

  it('reloads once a burst of changes settles, once', () => {
    live.change();
    jasmine.clock().tick(LIVE_RELOAD_MS - 1);
    live.change();
    jasmine.clock().tick(LIVE_RELOAD_MS - 1);
    expect(store.reloads).toBe(0);

    jasmine.clock().tick(1);
    expect(store.reloads).toBe(1);
  });

  it('watches the new topics when they change, and none when there are none', () => {
    store.topics.set(['season:2']);
    TestBed.tick();
    expect(live.active).toEqual([['season:2']]);

    store.topics.set([]);
    TestBed.tick();
    expect(live.active).toEqual([]);
  });

  it('stops watching, and drops a reload still waiting, when the store goes', () => {
    live.change();
    TestBed.resetTestingModule();
    jasmine.clock().tick(LIVE_RELOAD_MS);

    expect(live.active).toEqual([]);
    expect(store.reloads).toBe(0);
  });
});

describe('liveReload\'s spread', () => {
  it('reads a random while after the burst settles, up to LIVE_SPREAD_MS, so the pages don\'t all ask at once', () => {
    jasmine.clock().install();
    spyOn(Math, 'random').and.returnValue(0.5);
    const live = new FakeLive();
    TestBed.configureTestingModule({ providers: [WatchingStore, { provide: LiveService, useValue: live }] });
    const store = TestBed.inject(WatchingStore);
    TestBed.tick();

    live.change();
    jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS / 2 - 1);
    const before = store.reloads;
    jasmine.clock().tick(1);

    expect([before, store.reloads]).toEqual([0, 1]);
    jasmine.clock().uninstall();
  });
});

describe('liveData', () => {
  let live: FakeLive;
  let store: DataStore;

  beforeEach(() => {
    jasmine.clock().install();
    spyOn(Math, 'random').and.returnValue(0);
    live = new FakeLive();
    TestBed.configureTestingModule({ providers: [DataStore, { provide: LiveService, useValue: live }] });
    store = TestBed.inject(DataStore);
    TestBed.tick();
  });

  afterEach(() => jasmine.clock().uninstall());

  it('takes a change\'s data at once, with no read', () => {
    live.change({ topic: 'queue', data: { count: 3 } });
    jasmine.clock().tick(LIVE_RELOAD_MS);

    expect(store.taken).toEqual([3]);
    expect(store.reloads).toBe(0);
  });

  it('reads instead when the change has no data, the connection came back, or the store refuses the data', () => {
    live.change({ topic: 'queue' });
    jasmine.clock().tick(LIVE_RELOAD_MS);
    live.change({ topic: null });
    jasmine.clock().tick(LIVE_RELOAD_MS);
    live.change({ topic: 'queue', data: { count: -1 } });
    jasmine.clock().tick(LIVE_RELOAD_MS);

    expect(store.taken).toEqual([]);
    expect(store.reloads).toBe(3);
  });

  it('drops a read still waiting when data comes', () => {
    live.change({ topic: 'queue' });
    live.change({ topic: 'queue', data: { count: 4 } });
    jasmine.clock().tick(LIVE_RELOAD_MS);

    expect(store.taken).toEqual([4]);
    expect(store.reloads).toBe(0);
  });
});
