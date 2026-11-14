import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal, WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BACKUP_POLL_MS, BackupFormat, DatabaseBackupStore, DownloadStatus, ExportStatus } from './databaseBackup.store';

const download = (state: string, changes: Partial<DownloadStatus> = {}) => ({ format: 'sqlite', state, ...changes }) as DownloadStatus;
const exported = (state: string) => ({ state }) as ExportStatus;
const refused = [{ title: 'Refused', detail: 'Another backup is running.' }, { status: 409, statusText: 'Conflict', headers: { 'Content-Type': 'application/problem+json' } }] as const;

describe('DatabaseBackupStore', () => {
  let store: DatabaseBackupStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let format: WritableSignal<BackupFormat>;

  beforeEach(() => {
    spyOn(console, 'log');
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [DatabaseBackupStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(DatabaseBackupStore);
    http = TestBed.inject(HttpTestingController);
    format = signal<BackupFormat>('sqlite');
    store.format.use(format);
  });

  afterEach(() => {
    TestBed.tick();
    http.match('/api/backups/info').forEach(r => r.flush({ provider: 'Sqlite', latestMigration: 'x', formats: ['sqlite'] }));
    http.verify();
  });

  it('asks for no backup until it has the format', () => {
    const fresh = TestBed.runInInjectionContext(() => new DatabaseBackupStore());
    fresh.loadDownloadStatus();
    fresh.makeBackup();
    http.expectNone(r => r.url.startsWith('/api/backups/') && r.url !== '/api/backups/info');
    expect(fresh.making()).toBeFalse();
  });

  it("reads the database's details, and says when it cannot", async () => {
    TestBed.tick();
    http.expectOne('/api/backups/info').flush('x', { status: 500, statusText: 'Server Error' });
    await TestBed.inject(ApplicationRef).whenStable();
    expect(store.infoFailed()).toBeTrue();
    expect(store.infoValue()).toBeNull();
  });

  it("makes the chosen format's backup, following it until it's made, and keeps its file name", () => {
    jasmine.clock().install();
    try {
      store.makeBackup();
      expect(store.making()).toBeTrue();
      http.expectOne({ method: 'POST', url: '/api/backups/sqlite' }).flush(download('Running'));
      jasmine.clock().tick(BACKUP_POLL_MS);
      http.expectOne('/api/backups/sqlite/status').flush(download('Succeeded', { fileName: 'grif.sqlite' }));
      expect(store.making()).toBeFalse();
      expect(store.fileName()).toBe('grif.sqlite');
      expect(store.downloadStatus()!.state).toBe('Succeeded');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('says why a backup failed, and ignores an answer for a format no longer chosen', () => {
    store.makeBackup();
    http.expectOne('/api/backups/sqlite').flush(download('Failed', { message: 'Disk full' }));
    expect(snackBar.open).toHaveBeenCalledWith('Disk full', 'Close');

    store.makeBackup();
    http.expectOne('/api/backups/sqlite').flush(...refused);
    expect(store.making()).toBeFalse();
    expect(snackBar.open).toHaveBeenCalledWith('Another backup is running.', 'Close');

    store.makeBackup();
    http.expectOne('/api/backups/sqlite').flush('x', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('The backup failed', 'Close');

    store.loadDownloadStatus();
    format.set('postgres');
    http.expectOne('/api/backups/sqlite/status').flush(download('Succeeded', { fileName: 'old.sqlite' }));
    expect(store.fileName()).toBeNull();
  });

  it("says the backup failed when its status can't be read, only for one started here", () => {
    store.loadDownloadStatus();
    http.expectOne('/api/backups/sqlite/status').flush('x', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).not.toHaveBeenCalled();

    jasmine.clock().install();
    try {
      store.makeBackup();
      http.expectOne('/api/backups/sqlite').flush(download('Running'));
      jasmine.clock().tick(BACKUP_POLL_MS);
      http.expectOne('/api/backups/sqlite/status').flush('x', { status: 500, statusText: 'Server Error' });
      expect(store.making()).toBeFalse();
      expect(snackBar.open).toHaveBeenCalledWith('The backup failed', 'Close');
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('exports to another server, following it until it is done, and says why one would not start', () => {
    jasmine.clock().install();
    try {
      const started = jasmine.createSpy('started');
      store.startExport('Postgres', 'Host=x', started);
      expect(store.exporting()).toBeTrue();
      const start = http.expectOne({ method: 'POST', url: '/api/database-exports' });
      expect(start.request.body).toEqual({ provider: 'Postgres', connectionString: 'Host=x' });
      start.flush(exported('Running'));
      expect(started).toHaveBeenCalledTimes(1);
      expect(store.exporting()).toBeTrue();
      jasmine.clock().tick(BACKUP_POLL_MS);
      http.expectOne('/api/database-exports/status').flush(exported('Succeeded'));
      expect(store.exporting()).toBeFalse();

      store.startExport('SqlServer', 'Server=x', started);
      http.expectOne('/api/database-exports').flush('Cannot connect', { status: 400, statusText: 'Bad Request' });
      expect(store.starting()).toBeFalse();
      expect(started).toHaveBeenCalledTimes(1);
      expect(snackBar.open).toHaveBeenCalledWith('Cannot connect', 'Close');

      store.loadExportStatus();
      http.expectOne('/api/database-exports/status').flush('x', { status: 500, statusText: 'Server Error' });
      expect(store.exportStatus()!.state).toBe('Succeeded');
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
