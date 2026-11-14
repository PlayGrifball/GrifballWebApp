import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { OnDemandJobStatusDto } from '@api/models';
import { EXPORT_POLL_MS, ExcelFormStore } from './excelForm.store';

const sheet = { name: 'League', spreadsheetID: 'abc', sheetName: 'Matches' };
const idle: OnDemandJobStatusDto = { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null };

describe('ExcelFormStore', () => {
  let store: ExcelFormStore;
  let http: HttpTestingController;

  beforeEach(() => {
    spyOn(console, 'log');
    TestBed.configureTestingModule({ providers: [ExcelFormStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(ExcelFormStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('appends a match to the sheet, waiting until it answers, whatever it answers', () => {
    store.appendMatch('m-1', sheet);
    expect(store.submittingMatch()).toBeTrue();
    const append = http.expectOne({ method: 'POST', url: '/api/matches/m-1/append-to-sheet' });
    expect(append.request.body).toEqual(sheet);
    append.flush({});
    expect(store.submittingMatch()).toBeFalse();

    store.appendMatch('m-2', sheet);
    http.expectOne('/api/matches/m-2/append-to-sheet').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.submittingMatch()).toBeFalse();
  });

  it('exports every match, following the export until it is done', () => {
    jasmine.clock().install();
    try {
      store.exportAll(sheet);
      const start = http.expectOne({ method: 'POST', url: '/api/sheets/export' });
      expect(start.request.body).toEqual(sheet);
      start.flush({ ...idle, state: 'Queued' });
      expect(store.exporting()).toBeTrue();
      jasmine.clock().tick(EXPORT_POLL_MS);
      http.expectOne({ method: 'GET', url: '/api/sheets/export' }).flush(idle);
      expect(store.exporting()).toBeFalse();
      jasmine.clock().tick(EXPORT_POLL_MS);
      http.expectNone('/api/sheets/export');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('stops at an export already done, and says when one cannot start or be followed', () => {
    jasmine.clock().install();
    try {
      store.exportAll(sheet);
      http.expectOne({ method: 'POST', url: '/api/sheets/export' }).flush(idle);
      expect(store.exportRun()).toEqual(idle);

      store.exportAll(sheet);
      http.expectOne({ method: 'POST', url: '/api/sheets/export' }).flush('x', { status: 500, statusText: 'Server Error' });
      expect(store.exportError()).toBe("Couldn't start the export.");

      store.exportAll(sheet);
      expect(store.exportError()).toBeNull();
      http.expectOne({ method: 'POST', url: '/api/sheets/export' }).flush({ ...idle, state: 'Running' });
      jasmine.clock().tick(EXPORT_POLL_MS);
      http.expectOne({ method: 'GET', url: '/api/sheets/export' }).flush('x', { status: 500, statusText: 'Server Error' });
      expect(store.exportError()).toBe("Couldn't tell whether the export finished.");
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
