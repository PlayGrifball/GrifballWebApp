import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { durationText, intervalText, JOBS_POLL_MS, JobsComponent, splitInterval } from './jobs.component';
import type { JobDto, JobRunDto, JobsOverviewDto, JobStatusDto } from '../../api/dtos/jobsDto';

function status(changes: Partial<JobStatusDto> = {}): JobStatusDto {
  return {
    lastResult: 'Succeeded', lastRunAt: '2026-10-31T06:00:00Z', lastDurationMs: 42, lastSucceededAt: '2026-10-31T06:00:00Z', lastFailedAt: null,
    lastFailureMessage: null, consecutiveFailures: 0, runCount: 6, failureCount: 0, ...changes,
  };
}

function job(name: string, changes: Partial<JobDto> = {}): JobDto {
  return {
    group: 'recurring', name, description: `What ${name} does`, recurring: true, enabled: true, state: 'Scheduled', intervalSeconds: 3600,
    defaultIntervalSeconds: 3600, nextRunAt: '2026-10-31T07:00:00Z', previousRunAt: '2026-10-31T06:00:00Z', pausedReason: null, pausedBy: null,
    pausedAt: null, running: [], status: status(), ...changes,
  };
}

const overview = (jobs: JobDto[]): JobsOverviewDto => ({
  jobs, nodes: [{ instanceID: 'worker-1', lastCheckInAt: '2026-10-31T06:59:58Z', state: 'Alive' }],
});

const run = (changes: Partial<JobRunDto> = {}): JobRunDto => ({
  firedAt: '2026-10-31T06:00:00Z', durationMs: 180, result: 'Succeeded', error: null, node: 'worker-1', manual: false, trigger: 'historical-refresh', ...changes,
});

describe('jobs page helpers', () => {
  it('words an interval', () => {
    expect([10, 300, 3600, 5400, 86400, 172800, 90061, null, 0].map(intervalText))
      .toEqual(['10 s', '5 min', '1 h', '1 h 30 min', '1 day', '2 days', '1 day 1 h 1 min 1 s', '-', '0 s']);
  });

  it('splits an interval into the largest whole unit', () => {
    expect([86400, 7200, 300, 90, 5].map(splitInterval)).toEqual([
      { value: 1, unit: 'days' }, { value: 2, unit: 'hours' }, { value: 5, unit: 'minutes' }, { value: 90, unit: 'seconds' }, { value: 5, unit: 'seconds' },
    ]);
  });

  it('words a duration', () => {
    expect([12.4, 3400, 125000].map(durationText)).toEqual(['12 ms', '3.4 s', '2 min 5 s']);
  });
});

describe('JobsComponent', () => {
  let fixture: ComponentFixture<JobsComponent>;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), { provide: MatSnackBar, useValue: snackBar }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(JobsComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
    http.verify();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const text = (e: Element | null) => (e?.textContent ?? '').replace(/\s+/g, ' ').trim();
  const button = (label: string) =>
    Array.from(el().querySelectorAll<HTMLButtonElement>('button')).find(b => text(b) === label || b.getAttribute('aria-label') === label)!;
  const rows = () => Array.from(el().querySelectorAll('[aria-label="Jobs"] tbody tr'));

  /** After the last request is answered: the requests a change makes are checked straight after fixture.detectChanges() (a resource still loading keeps the fixture from being stable). */
  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  /** The jobs the list was last answered with: an action's reload answers the same. */
  let shown: JobDto[] = [];

  async function load(jobs: JobDto[]): Promise<void> {
    shown = jobs;
    fixture.detectChanges();
    http.expectOne('/api/Jobs/List').flush(overview(jobs));
    await settle();
  }

  async function details(name: string, runs: JobRunDto[] = [run()]): Promise<void> {
    button(`Details of ${name}`).click();
    fixture.detectChanges();
    const history = http.expectOne(r => r.url === '/api/Jobs/History');
    expect(history.request.params.get('group')).toBe('recurring');
    expect(history.request.params.get('name')).toBe(name);
    history.flush(runs);
    await settle();
  }

  /** The action's request, answered; then the list is read again. */
  async function answer(url: string, body: unknown): Promise<void> {
    fixture.detectChanges();
    const request = http.expectOne(url);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    http.expectOne('/api/Jobs/List').flush(overview(shown));
    fixture.detectChanges();
  }

  it('lists the jobs: how often, their state, next and last run, and the scheduler', async () => {
    await load([
      job('historical-refresh', { intervalSeconds: 86400 }),
      job('xbox-placeholders', { state: 'Paused', pausedBy: 'Commander Grif', pausedReason: 'Paused on the Jobs page', nextRunAt: null }),
      job('matchmaking-queue', { intervalSeconds: 10, running: [{ fireInstanceID: 'f-1', startedAt: '2026-10-31T06:59:58Z', node: 'worker-1' }],
        status: status({ lastResult: 'Failed', failureCount: 3 }) }),
      job('season-events', { enabled: false, state: 'Off', intervalSeconds: null, nextRunAt: null, status: null }),
    ]);

    const cells = rows().map(r => Array.from(r.querySelectorAll('td')).map(text));
    expect(cells.map(c => c[0])).toEqual([
      'historical-refreshWhat historical-refresh does', 'xbox-placeholdersWhat xbox-placeholders does',
      'matchmaking-queueWhat matchmaking-queue does', 'season-eventsWhat season-events does',
    ]);
    expect(cells.map(c => c[1])).toEqual(['1 day', '1 h', '10 s', '-']);
    expect(cells.map(c => c[2])).toEqual(['Scheduled', 'Pausedby Commander Grif', 'ScheduledRunning', 'OffDiscord is off']);
    expect(cells[1][3]).toBe('-');
    expect(cells[2][5]).toBe('6 3 failed');
    expect(cells[3][4]).toBe('-');
    expect(text(el().querySelector('.jb-nodes'))).toContain('worker-1 (Alive');
    expect(button('Run now season-events').disabled).toBeTrue();
    expect(button('Pause season-events').disabled).toBeTrue();
    expect(button('Resume xbox-placeholders')).toBeTruthy();
  });

  it('shows a job’s runs and its schedule, filled in with its interval', async () => {
    await load([job('historical-refresh', { intervalSeconds: 86400, defaultIntervalSeconds: 86400 })]);
    await details('historical-refresh', [run(), run({ manual: true, result: 'Failed', error: 'The sheet went private' })]);

    const panel = el().querySelector('.jb-detail')!;
    const runRows = Array.from(panel.querySelectorAll('[aria-label="Recent runs"] tbody tr')).map(r => Array.from(r.querySelectorAll('td')).map(text));
    expect(runRows.map(r => [r[1], r[2], r[4]])).toEqual([['180 ms', 'Succeeded', 'Schedule'], ['180 ms', 'FailedThe sheet went private', 'Run now']]);
    expect((panel.querySelector<HTMLInputElement>('input[aria-label="Interval"]')!).value).toBe('1');
    expect((panel.querySelector<HTMLSelectElement>('select[aria-label="Interval unit"]')!).value).toBe('days');
    expect(button('Save').disabled).toBeTrue();
    expect(button('Reset to default (every 1 day)')).toBeTruthy();
  });

  it('runs a job now, pauses and resumes it', async () => {
    await load([job('historical-refresh'), job('xbox-placeholders', { state: 'Paused' })]);

    button('Run now historical-refresh').click();
    await answer('/api/Jobs/Run', { group: 'recurring', name: 'historical-refresh' });
    expect(snackBar.open).toHaveBeenCalledWith('historical-refresh will run in a few seconds.', 'Close', { duration: 4000 });
    await settle();

    button('Pause historical-refresh').click();
    await answer('/api/Jobs/SetPaused', { group: 'recurring', name: 'historical-refresh', paused: true });
    expect(snackBar.open).toHaveBeenCalledWith('historical-refresh is paused.', 'Close', { duration: 4000 });
  });

  it('resumes a paused job', async () => {
    await load([job('xbox-placeholders', { state: 'Paused' })]);
    button('Resume xbox-placeholders').click();
    await answer('/api/Jobs/SetPaused', { group: 'recurring', name: 'xbox-placeholders', paused: false });
    expect(snackBar.open).toHaveBeenCalledWith('xbox-placeholders is resumed.', 'Close', { duration: 4000 });
  });

  it('changes how often a job runs, and resets it', async () => {
    await load([job('historical-refresh', { intervalSeconds: 86400, defaultIntervalSeconds: 86400 })]);
    await details('historical-refresh');

    const input = el().querySelector<HTMLInputElement>('input[aria-label="Interval"]')!;
    input.value = '12';
    input.dispatchEvent(new Event('input'));
    const unit = el().querySelector<HTMLSelectElement>('select[aria-label="Interval unit"]')!;
    unit.value = 'hours';
    unit.dispatchEvent(new Event('change'));
    await settle();
    button('Save').click();
    await answer('/api/Jobs/SetInterval', { group: 'recurring', name: 'historical-refresh', intervalSeconds: 43200 });
    expect(snackBar.open).toHaveBeenCalledWith('historical-refresh runs every 12 h.', 'Close', { duration: 4000 });
    // The runs are read again with the list
    http.expectOne(r => r.url === '/api/Jobs/History').flush([run()]);
    await settle();

    button('Reset to default (every 1 day)').click();
    await answer('/api/Jobs/ResetSchedule', { group: 'recurring', name: 'historical-refresh' });
    http.expectOne(r => r.url === '/api/Jobs/History').flush([run()]);
    await settle();
    expect(el().querySelector<HTMLInputElement>('input[aria-label="Interval"]')!.value).toBe('1');
  });

  it('refuses an interval under 5 seconds', async () => {
    await load([job('historical-refresh')]);
    await details('historical-refresh');
    const unit = el().querySelector<HTMLSelectElement>('select[aria-label="Interval unit"]')!;
    unit.value = 'seconds';
    unit.dispatchEvent(new Event('change'));
    const input = el().querySelector<HTMLInputElement>('input[aria-label="Interval"]')!;
    input.value = '2';
    input.dispatchEvent(new Event('input'));
    await settle();
    expect(button('Save').disabled).toBeTrue();
  });

  it('stops a running job', async () => {
    await load([job('matchmaking-queue', { running: [{ fireInstanceID: 'f-1', startedAt: '2026-10-31T06:59:58Z', node: 'worker-1' }] })]);
    await details('matchmaking-queue');
    button('Stop').click();
    await answer('/api/Jobs/Interrupt', { fireInstanceID: 'f-1' });
    http.expectOne(r => r.url === '/api/Jobs/History').flush([]);
    await settle();
    expect(text(el().querySelector('.jb-detail'))).toContain('No runs in the last week.');
  });

  it('says why an action failed', async () => {
    await load([job('historical-refresh')]);
    button('Run now historical-refresh').click();
    fixture.detectChanges();
    http.expectOne('/api/Jobs/Run').flush({ title: 'Conflict', detail: 'The job is off' }, { status: 409, statusText: 'Conflict' });
    await settle();
    expect(snackBar.open).toHaveBeenCalledWith(jasmine.any(String), 'Close');
    expect(button('Run now historical-refresh').disabled).toBeFalse();
  });

  it('says when the jobs couldn’t be loaded, and when there are none', async () => {
    fixture.detectChanges();
    http.expectOne('/api/Jobs/List').flush(null, { status: 500, statusText: 'Server Error' });
    await settle();
    expect(text(el().querySelector('[role=alert]'))).toBe("The jobs couldn't be loaded.");
  });

  it('reads the jobs again a poll after a failed read', async () => {
    jasmine.clock().install();
    try {
      fixture.detectChanges();
      http.expectOne('/api/Jobs/List').flush(null, { status: 500, statusText: 'Server Error' });
      await Promise.resolve();
      fixture.detectChanges();
      expect(text(el().querySelector('[role=alert]'))).toBe("The jobs couldn't be loaded.");

      jasmine.clock().tick(JOBS_POLL_MS - 1);
      http.expectNone('/api/Jobs/List');
      jasmine.clock().tick(1);
      fixture.detectChanges();
      http.expectOne('/api/Jobs/List').flush(overview([job('historical-refresh')]));
      await Promise.resolve();
      fixture.detectChanges();
      expect(rows().length).toBe(1);
      expect(el().querySelector('[role=alert]')).toBeNull();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('says there are no jobs before a worker has started', async () => {
    await load([]);
    expect(text(rows()[0])).toBe('No jobs yet: the worker makes them as it starts.');
    expect(el().querySelector('.jb-detail')).toBeNull();
  });
});
