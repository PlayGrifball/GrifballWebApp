import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { JobDto } from '@api/models';
import { JobKey } from '../../api/jobs';
import { intervalText, JOBS_POLL_MS, JobsStore } from './jobs.store';

const job = { group: 'Halo', name: 'Pull', displayName: 'Pull games', defaultIntervalSeconds: 60 } as JobDto;
const overview = { jobs: [job], nodes: [] };
const refused = [{ title: 'Refused', detail: 'Busy.' }, { status: 409, statusText: 'Conflict', headers: { 'Content-Type': 'application/problem+json' } }] as const;

describe('JobsStore', () => {
  let store: JobsStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    jasmine.clock().install();
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [JobsStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(JobsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    jasmine.clock().uninstall();
  });

  /** Lets an answered resource take its value (a microtask after the answer), then runs the effects it wakes. The clock is
   * mocked, so this waits on the microtask rather than for the app to be stable. */
  const settle = async () => {
    await Promise.resolve();
    TestBed.tick();
  };
  const jobs = () => {
    TestBed.tick();
    return http.expectOne('/api/jobs');
  };
  const runs = () => {
    TestBed.tick();
    return http.expectOne(r => r.url === '/api/jobs/Halo/Pull/runs' && r.params.get('take') === '50');
  };

  it('reads the jobs every few seconds', async () => {
    jobs().flush(overview);
    await settle();
    expect(store.loaded()).toBeTrue();
    expect(store.jobs()).toHaveSize(1);
    jasmine.clock().tick(JOBS_POLL_MS);
    http.expectOne('/api/jobs').flush({ jobs: [], nodes: [] });
    await settle();
    expect(store.jobs()).toEqual([]);
  });

  it('reads the jobs again a poll after a failed read', async () => {
    jobs().flush('x', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.failed()).toBeTrue();
    jasmine.clock().tick(JOBS_POLL_MS);
    jobs().flush(overview);
    await settle();
    expect(store.failed()).toBeFalse();
  });

  it("reads the chosen job's runs once one is chosen", async () => {
    const key = signal<JobKey | null>(null);
    store.selectedKey.use(key);
    jobs().flush(overview);
    await settle();
    expect(store.runs()).toBeNull();

    key.set({ group: 'Halo', name: 'Pull' });
    runs().flush([]);
    await settle();
    expect(store.runs()).toEqual([]);
  });

  it('runs, pauses, reschedules, resets and interrupts, reading the jobs again after each', async () => {
    store.selectedKey.use(signal(null));
    jobs().flush(overview);
    await settle();
    const actions: [() => void, string, string, string][] = [
      [() => store.run(job), '/api/jobs/Halo/Pull/run', 'Pull games will run in a few seconds.', ''],
      [() => store.setPaused(job, true), '/api/jobs/Halo/Pull/paused', 'Pull games is paused.', '{"paused":true}'],
      [() => store.setPaused(job, false), '/api/jobs/Halo/Pull/paused', 'Pull games is resumed.', '{"paused":false}'],
      [() => store.setInterval(job, 300), '/api/jobs/Halo/Pull/interval', 'Pull games runs every 5 min.', '{"intervalSeconds":300}'],
      [() => store.interrupt('fire-1'), '/api/job-runs/fire-1/interrupt', 'Asked the run to stop.', ''],
    ];
    for (const [act, url, said, body] of actions) {
      act();
      expect(store.busy()).toBeTrue();
      const request = http.expectOne({ method: 'POST', url });
      if (body)
        expect(JSON.stringify(request.request.body)).toBe(body);
      request.flush(null);
      expect(store.busy()).toBeFalse();
      expect(snackBar.open).toHaveBeenCalledWith(said, 'Close', { duration: 4000 });
      jobs().flush(overview);
      await settle();
    }

    const done = jasmine.createSpy('done');
    store.resetSchedule(job, done);
    http.expectOne({ method: 'POST', url: '/api/jobs/Halo/Pull/schedule/reset' }).flush(null);
    expect(done).toHaveBeenCalledTimes(1);
    jobs().flush(overview);
    await settle();
  });

  it('says why an action was refused', async () => {
    jobs().flush(overview);
    await settle();
    store.run(job);
    http.expectOne('/api/jobs/Halo/Pull/run').flush(...refused);
    expect(store.busy()).toBeFalse();
    expect(snackBar.open).toHaveBeenCalledWith('Busy.', 'Close');
  });

  it('says an interval in words', () => {
    expect(intervalText(null)).toBe('-');
    expect(intervalText(5400)).toBe('1 h 30 min');
    expect(intervalText(0)).toBe('0 s');
  });
});
