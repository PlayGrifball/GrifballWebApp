import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { VodReviewDto } from '@api/models';
import { VodReviewStore } from './vodReview.store';

const vod = (vodID: number) => ({ vodID, title: `VOD ${vodID}`, teams: [] }) as unknown as VodReviewDto;

describe('VodReviewStore', () => {
  let store: VodReviewStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [VodReviewStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(VodReviewStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  const review = () => {
    TestBed.tick();
    return http.expectOne('/api/vods/review');
  };

  it('loads the VODs to review', async () => {
    review().flush([vod(1)]);
    await settle();
    expect(store.loaded()).toBeTrue();
    expect(store.rows()).toHaveSize(1);
    expect(store.failed()).toBeFalse();
  });

  it('says when the VODs cannot be loaded', async () => {
    review().flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.failed()).toBeTrue();
    expect(store.rows()).toEqual([]);
  });

  it("loads the seasons once an editor is open, and a season's matches and teams once one is chosen", async () => {
    const editing = signal(false);
    const seasonID = signal<number | null>(null);
    store.editing.use(editing);
    store.seasonID.use(seasonID);
    review().flush([]);
    await settle();
    http.expectNone('/api/vods/link-seasons');

    editing.set(true);
    TestBed.tick();
    http.expectOne('/api/vods/link-seasons').flush([{ seasonID: 4, seasonName: 'Spring' }]);
    seasonID.set(4);
    TestBed.tick();
    http.expectOne('/api/seasons/4/vod-link-options').flush({ matches: [], teams: [] });
    await settle();
    expect(store.seasonRows()).toHaveSize(1);
    expect(store.optionsValue()).toEqual({ matches: [], teams: [] } as never);
  });

  it('links a VOD, then loads the list again', async () => {
    review().flush([vod(1)]);
    await settle();
    const done = jasmine.createSpy('done');
    store.link(1, { seasonID: 4, seasonMatchID: null, teamIDs: [10] }, done);
    expect(store.busy()).toBeTrue();
    const link = http.expectOne({ method: 'POST', url: '/api/vods/1/link' });
    expect(link.request.body).toEqual({ seasonID: 4, seasonMatchID: null, teamIDs: [10] });
    link.flush(null);
    expect(store.busy()).toBeFalse();
    expect(done).toHaveBeenCalledTimes(1);
    review().flush([]);
    await settle();
    expect(store.rows()).toEqual([]);
  });

  it('dismisses a VOD, and says why one could not be', async () => {
    review().flush([vod(1)]);
    await settle();
    const done = jasmine.createSpy('done');
    store.dismiss(1, done);
    http.expectOne({ method: 'POST', url: '/api/vods/1/dismiss' }).flush({ title: 'Refused', detail: 'Already linked.' },
      { status: 400, statusText: 'Bad Request', headers: { 'Content-Type': 'application/problem+json' } });
    expect(store.busy()).toBeFalse();
    expect(done).not.toHaveBeenCalled();
    expect(snackBar.open).toHaveBeenCalledWith('Already linked.', 'Close');

    store.dismiss(1, done);
    http.expectOne('/api/vods/1/dismiss').flush(null);
    expect(done).toHaveBeenCalledTimes(1);
    review().flush([]);
    await settle();
  });
});
