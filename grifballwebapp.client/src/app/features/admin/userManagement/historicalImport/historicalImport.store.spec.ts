import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HistoricalImportStore, isActive, JOB_POLL_MS } from './historicalImport.store';

const job = (jobID: number, status: string) => ({ jobID, status, kind: 'Preview' });

describe('HistoricalImportStore', () => {
  let store: HistoricalImportStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [HistoricalImportStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(HistoricalImportStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Lets an answered resource take its value (a microtask after the answer). */
  const settle = () => TestBed.inject(ApplicationRef).whenStable();
  /** Answers the page's three lists. */
  const lists = (fail = false) => {
    TestBed.tick();
    const answer = (url: string, body: unknown) => fail
      ? http.expectOne(url).flush('x', { status: 500, statusText: 'Server Error' })
      : http.expectOne(url).flush(body);
    answer('/api/historical-sources', [{ sourceID: 1 }]);
    answer('/api/historical-imports', [{ jobID: 3 }]);
    answer('/api/historical-imports/schedule', { paused: false });
  };

  it('loads the sources, jobs and schedule', async () => {
    lists();
    await settle();
    expect(store.sourceRows()).toHaveSize(1);
    expect(store.jobRows()).toHaveSize(1);
    expect(store.jobsLoaded()).toBeTrue();
    expect(store.scheduleValue()).toEqual({ paused: false } as never);
  });

  it('says which of them could not be loaded', async () => {
    lists(true);
    await settle();
    expect(store.sourcesFailed()).toBeTrue();
    expect(store.jobsFailed()).toBeTrue();
    expect(store.scheduleFailed()).toBeTrue();
    expect(store.scheduleValue()).toBeNull();
  });

  it('queues a refresh and shows its job, looked at again until it finishes', async () => {
    jasmine.clock().install();
    try {
      lists();
      await settle();
      store.refresh();
      expect(store.busy()).toBeTrue();
      http.expectOne({ method: 'POST', url: '/api/historical-sources/refresh' }).flush({ jobID: 9 });
      expect(store.busy()).toBeFalse();
      expect(store.selectedJobID()).toBe(9);
      TestBed.tick();
      http.expectOne('/api/historical-imports').flush([]);
      http.expectOne('/api/historical-imports/9').flush(job(9, 'Running'));
      jasmine.clock().tick(JOB_POLL_MS);
      http.expectOne('/api/historical-imports/9').flush(job(9, 'Succeeded'));
      jasmine.clock().tick(JOB_POLL_MS);
      http.expectNone('/api/historical-imports/9');
    } finally {
      jasmine.clock().uninstall();
    }
    await settle();
    expect(store.selectedJob()!.status).toBe('Succeeded');
  });

  it('sends each action to its endpoint', async () => {
    lists();
    await settle();
    store.preview();
    http.expectOne({ method: 'POST', url: '/api/historical-imports/preview' }).flush('x', { status: 409, statusText: 'Conflict' });
    expect(snackBar.open).toHaveBeenCalledWith("Couldn't start the preview.", 'Close');

    store.apply(9);
    http.expectOne({ method: 'POST', url: '/api/historical-imports/9/apply' }).flush('x', { status: 409, statusText: 'Conflict' });
    store.approve(4);
    http.expectOne({ method: 'POST', url: '/api/historical-reviews/4/approve' }).flush('x', { status: 409, statusText: 'Conflict' });

    store.skip(4);
    http.expectOne({ method: 'POST', url: '/api/historical-reviews/4/skip' }).flush(null);
    TestBed.tick();
    http.expectOne('/api/historical-imports').flush([]);

    store.setEnabled(1, false);
    const enabled = http.expectOne({ method: 'POST', url: '/api/historical-sources/1/enabled' });
    expect(enabled.request.body).toEqual({ enabled: false });
    enabled.flush(null);
    TestBed.tick();
    http.expectOne('/api/historical-sources').flush([]);

    store.setPaused(true);
    const paused = http.expectOne({ method: 'POST', url: '/api/historical-imports/schedule/paused' });
    expect(paused.request.body).toEqual({ paused: true });
    paused.flush(null);
    TestBed.tick();
    http.expectOne('/api/historical-imports/schedule').flush({ paused: true });
    await settle();
    expect(store.scheduleValue()!.paused).toBeTrue();
  });

  it('polls only a queued or running job', () => {
    expect(isActive({ status: 'Queued' })).toBeTrue();
    expect(isActive({ status: 'Running' })).toBeTrue();
    expect(isActive({ status: 'Failed' })).toBeFalse();
  });
});
