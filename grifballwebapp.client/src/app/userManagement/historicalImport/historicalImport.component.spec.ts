import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Component } from '@angular/core';
import { VodReviewComponent } from './vodReview/vodReview.component';
import { HistoricalImportComponent, interval, isActive, JOB_POLL_MS, removalText, size } from './historicalImport.component';
import { HistoricalImportJobDto, HistoricalImportJobSummaryDto, HistoricalImportReport, HistoricalImportScheduleDto, HistoricalSourceDto } from '@api/models';

/** The VOD review (tested on its own): here only where it sits, without its requests. */
@Component({ selector: 'app-vod-review', template: '' })
class StubVodReview {}

function source(id: string, changes: Partial<HistoricalSourceDto> = {}): HistoricalSourceDto {
  return {
    sourceID: id, category: 'AGLA-2019', year: 2019, title: 'WL19 Schedule & Stats', url: 'https://docs.google.com/spreadsheets/d/x/edit', kind: 'GoogleSheet',
    league: 'AGLA', gameVersion: 'halo3', format: 'LeagueSeason', enabled: true, isDead: false, imported: true, lastFetchedAt: '2026-10-31T06:00:00Z',
    lastFetchStatus: 'Unchanged', lastChangedAt: '2026-10-20T06:00:00Z', snapshotCount: 2, latestSnapshotSize: 1318215, ...changes,
  };
}

function report(changes: Partial<HistoricalImportReport> = {}): HistoricalImportReport {
  return {
    seasonsCreated: 0, seasonsUpdated: 2, teamsCreated: 0, teamsRemoved: 1, statsRemoved: 3, championshipsRemoved: 0, statsInserted: 0, statsUpdated: 10,
    recordedGamesImported: 4, recordedGamesRemoved: 0, championshipsInserted: 0, championshipsUpdated: 0, dummyUsersCreated: 0, waypointMatchesPulled: 0,
    skipped: [], warnings: [], errors: [], seasonRemovals: [], sourcesChanged: [], sourcesFailed: [], sourcesUnchanged: 0, importJobID: null, ...changes,
  };
}

function job(id: number, changes: Partial<HistoricalImportJobDto> = {}): HistoricalImportJobDto {
  return {
    jobID: id, kind: 'Preview', trigger: 'Manual', guarded: false, status: 'Succeeded', stage: 'Done', seasonsDone: 2, seasonsTotal: 2,
    report: report(), error: null, createdAt: '2026-10-31T06:00:00Z', startedAt: '2026-10-31T06:00:01Z', finishedAt: '2026-10-31T06:01:00Z', reviews: [], ...changes,
  };
}

const summary = (id: number, changes: Partial<HistoricalImportJobSummaryDto> = {}): HistoricalImportJobSummaryDto => ({
  jobID: id, kind: 'Refresh', trigger: 'Scheduled', status: 'Succeeded', stage: 'Done', createdAt: '2026-10-31T06:00:00Z', finishedAt: '2026-10-31T06:02:00Z', needsReview: 0, ...changes,
});

const schedule = (changes: Partial<HistoricalImportScheduleDto> = {}): HistoricalImportScheduleDto => ({
  paused: false, interval: '1.00:00:00', lastRunAt: '2026-10-31T06:00:00Z', nextRunAt: '2026-11-01T06:00:00Z', lastJobID: 7, lastRunNote: null, ...changes,
});

describe('HistoricalImportComponent', () => {
  let fixture: ComponentFixture<HistoricalImportComponent>;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), { provide: MatSnackBar, useValue: snackBar }],
    });
    TestBed.overrideComponent(HistoricalImportComponent, { remove: { imports: [VodReviewComponent] }, add: { imports: [StubVodReview] } });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(HistoricalImportComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const text = (e: Element | null) => (e?.textContent ?? '').replace(/\s+/g, ' ').trim();
  const button = (label: string, root: Element = el()) =>
    Array.from(root.querySelectorAll<HTMLButtonElement>('button')).find(b => text(b) === label || b.getAttribute('aria-label') === label)!;
  const region = (label: string) => el().querySelector(`[role=region][aria-label="${label}"]`)!;

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function load(sources = [source('2019-wl19-schedule-stats'), source('2018-fl18-schedule', { isDead: true, enabled: false, imported: false, title: 'FL18 Schedule DEAD', lastFetchedAt: null, lastFetchStatus: null, lastChangedAt: null, snapshotCount: 0, latestSnapshotSize: null })],
    jobs = [summary(7), summary(8, { kind: 'Import', status: 'Succeeded', needsReview: 1, finishedAt: null })], scheduled = schedule()): Promise<void> {
    http.expectOne('/api/historical-sources').flush(sources);
    http.expectOne('/api/historical-imports').flush(jobs);
    http.expectOne('/api/historical-imports/schedule').flush(scheduled);
    await settle();
  }

  async function showJob(dto: HistoricalImportJobDto, jobButton = 'Show job ' + dto.jobID): Promise<void> {
    button(jobButton).click();
    fixture.detectChanges();
    http.expectOne(`/api/historical-imports/${dto.jobID}`).flush(dto);
    await settle();
  }

  it('lists the sources, the jobs and the schedule', async () => {
    await load();

    const sourceRows = Array.from(region('Sources').querySelectorAll('tbody tr'));
    expect(Array.from(el().querySelectorAll('th')).every(th => th.getAttribute('scope') === 'col')).toBeTrue();
    expect(text(sourceRows[0])).toContain('WL19 Schedule & Stats');
    expect(text(sourceRows[0])).toContain('1.3 MB');
    expect(sourceRows[0].querySelector('a')!.getAttribute('href')).toBe('https://docs.google.com/spreadsheets/d/x/edit');
    expect(text(sourceRows[1])).toContain('Gone');
    expect(text(sourceRows[1])).toContain('backup only');
    expect(text(sourceRows[1])).toContain('never');
    expect(sourceRows[1].classList).toContain('hi-dead');
    const jobRows = Array.from(region('Jobs').querySelectorAll('tbody tr'));
    expect(text(jobRows[0])).toContain('Refresh');
    expect(text(jobRows[1])).toContain('1 to decide');
    expect(text(el().querySelector('.hi-schedule'))).toContain('Every 1 day');
    expect(button('Pause schedule')).toBeTruthy();
    expect(el().querySelector('app-vod-review')).withContext('The VOD review list, between the jobs and the sources').not.toBeNull();
  });

  it('says when nothing could be loaded, or there are no jobs', async () => {
    http.expectOne('/api/historical-sources').flush('no', { status: 500, statusText: 'Error' });
    http.expectOne('/api/historical-imports').flush([]);
    http.expectOne('/api/historical-imports/schedule').flush('no', { status: 500, statusText: 'Error' });
    await settle();

    expect(text(el())).toContain("The sources couldn't be loaded.");
    expect(text(el())).toContain("The schedule couldn't be loaded.");
    expect(text(el())).toContain('No jobs yet.');
  });

  it("says when the jobs couldn't be loaded, and shows a paused schedule that never ran", async () => {
    http.expectOne('/api/historical-sources').flush([]);
    http.expectOne('/api/historical-imports').flush('no', { status: 500, statusText: 'Error' });
    http.expectOne('/api/historical-imports/schedule').flush(schedule({ paused: true, lastRunAt: null, lastJobID: null, lastRunNote: 'Waiting for a queued or running job to finish' }));
    await settle();

    expect(text(el())).toContain("The jobs couldn't be loaded.");
    expect(text(el().querySelector('.hi-schedule'))).toContain('Paused. Last run never.');
    expect(text(el().querySelector('.hi-schedule'))).toContain('Waiting for a queued or running job');
    expect(button("Last run's job")).toBeUndefined();

    button('Resume schedule').click();
    const resume = http.expectOne('/api/historical-imports/schedule/paused');
    expect(resume.request.body).toEqual({ paused: false });
    resume.flush(schedule());
    fixture.detectChanges();
    http.expectOne('/api/historical-imports/schedule').flush(schedule());
    await settle();
    expect(button('Pause schedule')).toBeTruthy();
  });

  it('refreshes the sources and shows the refresh job with the import it queued', async () => {
    await load();

    button('Refresh sources').click();
    http.expectOne('/api/historical-sources/refresh').flush({ jobID: 9 }, { status: 202, statusText: 'Accepted' });
    fixture.detectChanges();
    http.expectOne('/api/historical-imports').flush([summary(9, { status: 'Queued', trigger: 'Manual' })]);
    http.expectOne('/api/historical-imports/9').flush(job(9, {
      kind: 'Refresh', report: report({ sourcesChanged: ['2019-wl19-schedule-stats'], sourcesFailed: ['late-league-website: answered 404'], sourcesUnchanged: 40, importJobID: 10 }),
    }));
    await settle();

    const panel = el().querySelector('.hi-job')!;
    expect(text(panel)).toContain('Job 9: Refresh (Manual)');
    expect(text(panel)).toContain('1 changed · 40 unchanged · 1 failed');
    expect(text(panel)).toContain('Changed: 2019-wl19-schedule-stats');
    expect(text(panel)).toContain('late-league-website: answered 404');

    button('The import it queued', panel).click();
    fixture.detectChanges();
    http.expectOne('/api/historical-imports/10').flush(job(10, { kind: 'Import', trigger: 'Scheduled', guarded: true }));
    await settle();
    expect(text(el().querySelector('.hi-job'))).toContain('Job 10: Import (Scheduled, guarded)');
    expect(text(el().querySelector('.hi-job'))).toContain('2 updated');
  });

  it('previews an import, shows what each season would lose, and applies it', async () => {
    await load();

    button('Import').click();
    http.expectOne('/api/historical-imports/preview').flush({ jobID: 11 }, { status: 202, statusText: 'Accepted' });
    fixture.detectChanges();
    http.expectOne('/api/historical-imports').flush([]);
    // Still running: looked at again after a while
    jasmine.clock().install();
    try {
      http.expectOne('/api/historical-imports/11').flush(job(11, { status: 'Running', stage: 'Importing Winter League 2019', seasonsDone: 1, finishedAt: null, report: null }));
      await settle();
      expect(text(el().querySelector('.hi-job'))).toContain('Running · Importing Winter League 2019 · 1/2 seasons');
      jasmine.clock().tick(JOB_POLL_MS);
      http.expectOne('/api/historical-imports/11').flush(job(11, {
        reviews: [{ reviewID: 3, seasonKey: '2019-wl19-schedule-stats/Team and Player Stats', seasonName: 'Winter League 2019', status: 'Preview',
          statsBefore: 5, statsRemoved: 3, teamsBefore: 2, teamsRemoved: 1, titlesBefore: 0, titlesRemoved: 0, approvalJobID: null, decidedAt: null }],
      }));
    } finally {
      jasmine.clock().uninstall();
    }
    await settle();

    const reviews = region('Season removals');
    expect(text(reviews.querySelector('tbody tr'))).toContain('3 of 5 stat rows, 1 of 2 teams, 0 of 0 titles');
    expect(text(reviews.querySelector('tbody tr'))).toContain('Would remove');

    button('Apply this import').click();
    http.expectOne('/api/historical-imports/11/apply').flush({ jobID: 12 }, { status: 202, statusText: 'Accepted' });
    fixture.detectChanges();
    http.expectOne('/api/historical-imports').flush([]);
    http.expectOne('/api/historical-imports/12').flush(job(12, { kind: 'Import' }));
    await settle();
    expect(button('Apply this import')).toBeUndefined();
  });

  it('says when a preview removes nothing, and shows a failed job', async () => {
    await load();
    await showJob(job(7, { reviews: [] }));
    expect(text(el().querySelector('.hi-job'))).toContain('No season loses anything.');

    await showJob(job(8, { kind: 'Import', status: 'Failed', error: 'boom', report: null, seasonsTotal: 0 }));
    expect(el().querySelector('.hi-job [role=alert]')!.textContent).toContain('boom');
  });

  it('approves or skips a season the guardrail stopped', async () => {
    await load();
    const stopped = (id: number, name: string) => ({
      reviewID: id, seasonKey: name + '/Stats', seasonName: name, status: 'NeedsReview' as const, statsBefore: 5, statsRemoved: 4, teamsBefore: 2,
      teamsRemoved: 0, titlesBefore: 1, titlesRemoved: 1, approvalJobID: null, decidedAt: null,
    });
    await showJob(job(8, { kind: 'Import', guarded: true, reviews: [stopped(1, 'A'), stopped(2, 'B'), { ...stopped(3, 'C'), status: 'Approved', approvalJobID: 20 }] }));

    button('Apply A').click();
    http.expectOne('/api/historical-reviews/1/approve').flush({ jobID: 21 }, { status: 202, statusText: 'Accepted' });
    fixture.detectChanges();
    http.expectOne('/api/historical-imports').flush([summary(7), summary(8, { kind: 'Import' })]);
    http.expectOne('/api/historical-imports/21').flush(job(21, { kind: 'Import' }));
    await settle();

    await showJob(job(8, { kind: 'Import', guarded: true, reviews: [stopped(2, 'B')] }), 'Show job 8');
    button('Skip B').click();
    http.expectOne('/api/historical-reviews/2/skip').flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    http.expectOne('/api/historical-imports/8').flush(job(8, { kind: 'Import', reviews: [{ ...stopped(2, 'B'), status: 'Skipped' }] }));
    http.expectOne('/api/historical-imports').flush([]);
    await settle();
    expect(text(region('Season removals'))).toContain('Skipped');
  });

  it("shows an approved season's import", async () => {
    await load();
    await showJob(job(8, { kind: 'Import', reviews: [{ reviewID: 3, seasonKey: 'c/Stats', seasonName: null, status: 'Approved', statsBefore: 1, statsRemoved: 1, teamsBefore: 1, teamsRemoved: 0, titlesBefore: 0, titlesRemoved: 0, approvalJobID: 20, decidedAt: '2026-10-31T07:00:00Z' }] }));

    expect(text(region('Season removals'))).toContain('c/Stats');
    button('Its import').click();
    fixture.detectChanges();
    http.expectOne('/api/historical-imports/20').flush(job(20, { kind: 'Import' }));
    await settle();
    expect(text(el().querySelector('.hi-job'))).toContain('Job 20');
  });

  it("shows the last scheduled run's job, and disables a source", async () => {
    await load();

    button("Last run's job").click();
    fixture.detectChanges();
    http.expectOne('/api/historical-imports/7').flush(job(7, { kind: 'Refresh', trigger: 'Scheduled', report: report() }));
    await settle();
    expect(text(el().querySelector('.hi-job'))).toContain('0 changed');

    button('Download WL19 Schedule & Stats').click();
    const set = http.expectOne('/api/historical-sources/2019-wl19-schedule-stats/enabled');
    expect(set.request.body).toEqual({ enabled: false });
    set.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    http.expectOne('/api/historical-sources').flush([source('2019-wl19-schedule-stats', { enabled: false })]);
    await settle();
    expect(button('Download WL19 Schedule & Stats').getAttribute('aria-pressed')).toBe('false');
  });

  it('says why an action failed', async () => {
    await load();

    button('Refresh sources').click();
    http.expectOne('/api/historical-sources/refresh').flush({ title: 'Not now' }, { status: 409, statusText: 'Conflict' });
    button('Import').click();
    http.expectOne('/api/historical-imports/preview').flush('no', { status: 500, statusText: 'Error' });
    await settle();

    expect(snackBar.open).toHaveBeenCalledWith('Not now', 'Close');
    // problemText returns the server's plain-text body ("no") rather than the fallback here.
    expect(snackBar.open).toHaveBeenCalledWith('no', 'Close');
    expect(button('Refresh sources').disabled).toBeFalse();
  });

  it('formats sizes, intervals and removals', () => {
    // Flush the sources/jobs/schedule GETs the beforeEach's detectChanges() already triggered.
    http.expectOne('/api/historical-sources').flush([]);
    http.expectOne('/api/historical-imports').flush([]);
    http.expectOne('/api/historical-imports/schedule').flush(schedule());
    expect([size(null), size(512), size(2048), size(3 * 1024 * 1024)]).toEqual(['-', '512 B', '2.0 KB', '3.0 MB']);
    expect([interval('1.00:00:00'), interval('2.06:30:00'), interval('06:00:00'), interval('00:00:00'), interval('weekly')]).toEqual(['1 day', '2 days 6 h 30 min', '6 h', '00:00:00', 'weekly']);
    expect(removalText({ statsRemoved: 1, statsBefore: 2, teamsRemoved: 0, teamsBefore: 1, titlesRemoved: 0, titlesBefore: 0 })).toBe('1 of 2 stat rows, 0 of 1 teams, 0 of 0 titles');
    expect([isActive({ status: 'Queued' }), isActive({ status: 'Running' }), isActive({ status: 'Failed' })]).toEqual([true, true, false]);
  });
});
