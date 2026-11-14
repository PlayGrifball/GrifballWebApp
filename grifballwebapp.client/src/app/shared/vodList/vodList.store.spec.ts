import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { VodDto } from '@api/models';
import { VodListStore } from './vodList.store';
import type { VodScope } from './vods';

const vod = (vodID: number) => ({ vodID, youTubeVideoID: 'dQw4w9WgXcQ' }) as VodDto;

describe('VodListStore', () => {
  let store: VodListStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [VodListStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(VodListStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it knows what the VODs are of', () => {
    store.scope.use(signal<VodScope>('season'));
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.vods()).toEqual([]);
  });

  it('loads the VODs of each scope, and has none when they fail', async () => {
    const scope = signal<VodScope>('seasonMatch');
    store.scope.use(scope);
    store.id.use(signal(7));
    for (const [each, url] of [['seasonMatch', '/api/season-matches/7/vods'], ['season', '/api/seasons/7/vods'], ['team', '/api/teams/7/vods'], ['player', '/api/users/7/vods']] as const) {
      scope.set(each);
      TestBed.tick();
      http.expectOne(url).flush([vod(1)]);
      await settle();
      expect(store.vods()).toHaveSize(1);
    }

    scope.set('season');
    TestBed.tick();
    http.expectOne('/api/seasons/7/vods').flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.vods()).toEqual([]);
  });
});
