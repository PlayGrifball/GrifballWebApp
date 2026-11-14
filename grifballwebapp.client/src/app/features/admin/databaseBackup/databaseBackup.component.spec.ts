import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BackupFormat, DatabaseBackupComponent, DownloadStatus, ExportStatus } from './databaseBackup.component';
import { BACKUP_POLL_MS } from './databaseBackup.store';

describe('DatabaseBackupComponent', () => {
  let fixture: ComponentFixture<DatabaseBackupComponent>;
  let component: DatabaseBackupComponent;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [DatabaseBackupComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DatabaseBackupComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const button = () => el().querySelector('button') as HTMLButtonElement;

  // What the server sends when there's nothing to say; each status below is these with some fields set
  const none: ExportStatus = { state: 'None', provider: null, started: null, finished: null, tables: 0, rows: 0, message: null, progress: null, progressMessage: null };
  const flushExportStatus = (status: Partial<ExportStatus> = {}) => http.expectOne('/api/database-exports/status').flush({ ...none, ...status });
  const noDownload: DownloadStatus = {
    state: 'None', format: null, started: null, finished: null, fileName: null, tables: 0, rows: 0, message: null, size: null, runningFormat: null,
    progress: null, progressMessage: null,
  };
  const flushDownloadStatus = (status: Partial<DownloadStatus> = {}) =>
    http.expectOne(r => /^\/api\/backups\/\w+\/status$/.test(r.url)).flush({ ...noDownload, ...status });
  const running = (format: BackupFormat): Partial<DownloadStatus> => ({ state: 'Running', format, tables: 0, rows: 0 });
  const made = (format: BackupFormat, fileName: string): Partial<DownloadStatus> => ({ state: 'Succeeded', format, fileName, tables: 40, rows: 12345, size: 1_300_000 });
  /** The backup of a format made here, so the page has its file name. */
  const madeHere = (format: BackupFormat, fileName: string) => {
    component.format.set(format);
    component.makeBackup();
    expectStart(format).flush({ ...noDownload, ...made(format, fileName) });
  };
  const downloadLink = () => el().querySelector('.db-ready a') as HTMLAnchorElement;

  /** Renders the page with api/backups/info answered (the formats it can download), and the last export's and backup's status. */
  async function init(exportStatus: Partial<ExportStatus> = {}, formats: BackupFormat[] = ['sqlite', 'postgres', 'sqlserver', 'bak', 'dump'], downloadStatus: Partial<DownloadStatus> = {}) {
    flushExportStatus(exportStatus);
    flushDownloadStatus(downloadStatus);
    fixture.detectChanges();
    http.expectOne('/api/backups/info').flush({ provider: 'SqlServer', latestMigration: '20261031000000_Last', formats });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const expectStart = (format: string) => {
    const req = http.expectOne(r => r.method === 'POST' && /^\/api\/backups\/\w+$/.test(r.url));
    expect(req.request.url).toBe(`/api/backups/${format}`);
    return req;
  };

  it("shows the database's server and migration", async () => {
    await init();
    expect(el().querySelector('.db-info')!.textContent).toContain('SqlServer');
    expect(el().querySelector('.db-info')!.textContent).toContain('20261031000000_Last');
  });

  it('offers the formats the server can make', async () => {
    await init();
    expect(component.formats().map(f => f.label)).toEqual(['SQLite database (.sqlite)', 'PostgreSQL script (.sql)', 'SQL Server script (.sql)', 'SQL Server backup (.bak)', 'PostgreSQL dump (.dump)']);
  });

  it('offers no native backup without a server of its kind', async () => {
    await init(none, ['sqlite', 'postgres', 'sqlserver']);
    expect(component.formats().map(f => f.value)).toEqual(['sqlite', 'postgres', 'sqlserver']);
  });

  it('says how to restore each format, with the file downloaded', async () => {
    await init();
    madeHere('postgres', 'grifball-backup-1.postgres.sql');
    expect(component.restore().command).toBe('createdb GrifballWebApp\npsql -v ON_ERROR_STOP=1 -d GrifballWebApp -f grifball-backup-1.postgres.sql');
    component.format.set('sqlserver');
    expect(component.restore().command).toContain('sqlcmd -S <server> -d GrifballWebApp -b -i grifball-backup-1.postgres.sql');
    component.format.set('bak');
    expect(component.restore().command).toBe("RESTORE DATABASE GrifballWebApp FROM DISK = N'/var/opt/mssql/backup/<file>' WITH RECOVERY");
    madeHere('bak', 'grifball-backup-1.bak');
    expect(component.restore().command).toContain("FROM DISK = N'/var/opt/mssql/backup/grifball-backup-1.bak'");
    fixture.detectChanges();
    expect(el().querySelector('.db-restore-note')!.textContent).toContain('Copy the file where SQL Server can read it');
  });

  it('says how to restore a PostgreSQL dump', async () => {
    await init();
    madeHere('dump', 'grifball-backup-1.dump');
    expect(component.restore().command).toBe('createdb GrifballWebApp\npg_restore --no-owner -d GrifballWebApp grifball-backup-1.dump');
  });

  it("gives the server's reason when it can't make the backup here", async () => {
    await init();
    component.format.set('dump');
    component.makeBackup();
    expectStart('dump').flush({ status: 409, title: 'Conflict', detail: 'A .dump needs PostgreSQL, which the site doesn\'t run on.' }, { status: 409, statusText: 'Conflict' });
    expect(snackBar.open).toHaveBeenCalledWith('A .dump needs PostgreSQL, which the site doesn\'t run on.', 'Close');
    expect(component.making()).toBeFalse();
  });

  it('says when another backup is being made', async () => {
    await init();
    component.makeBackup();
    expectStart('sqlite').flush('A backup or export is already running; try again when it\'s done.', { status: 409, statusText: 'Conflict' });
    expect(snackBar.open).toHaveBeenCalledWith('A backup or export is already running; try again when it\'s done.', 'Close');
  });

  it("says the backup failed when it couldn't start, without the server's text for other errors", async () => {
    await init();
    component.makeBackup();
    expectStart('sqlite').flush('stack trace', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('The backup failed', 'Close');
    component.makeBackup();
    expectStart('sqlite').error(new ProgressEvent('error'));
    expect(snackBar.open.calls.count()).toBe(2);
    expect(component.making()).toBeFalse();
  });

  describe('download', () => {
    beforeEach(() => jasmine.clock().install());
    afterEach(() => jasmine.clock().uninstall());

    it('makes the backup in the background, says its name and size, links its download, and says how to restore it', async () => {
      await init();
      expect(el().querySelector('.db-command')!.textContent).toContain('Data Source=/data/<file>');

      button().click();
      expectStart('sqlite').flush(running('sqlite'), { status: 202, statusText: 'Accepted' });
      fixture.detectChanges();
      expect(button().disabled).toBeTrue();
      expect(button().textContent).toContain('Making the backup');
      expect(el().querySelector('.db-progress')!.textContent).toContain('Waiting to start');

      jasmine.clock().tick(BACKUP_POLL_MS);
      flushDownloadStatus({ ...running('sqlite'), progress: 25, progressMessage: 'Copying Seasons (16 of 64)' });
      fixture.detectChanges();
      expect(el().querySelector('.db-progress')!.textContent).toContain('Copying Seasons (16 of 64)');
      expect((el().querySelector('progress[aria-label="Backup progress"]') as HTMLProgressElement).value).toBe(25);
      jasmine.clock().tick(BACKUP_POLL_MS);
      flushDownloadStatus(made('sqlite', 'grifball-backup-20261101-120000.sqlite'));
      fixture.detectChanges();

      // Made, not downloaded: what it is and how big, and the link the browser downloads it from (its own progress)
      http.expectNone(r => r.url.endsWith('/file'));
      const ready = el().querySelector('.db-ready')!.textContent!.replace(/\s+/g, ' ');
      expect(ready).toContain('grifball-backup-20261101-120000.sqlite');
      expect(ready).toContain('1.2 MB · 40 tables, 12345 rows');
      expect(el().querySelector('.db-command')!.textContent).toContain('Data Source=/data/grifball-backup-20261101-120000.sqlite');

      expect(downloadLink().getAttribute('href')).toBe('api/backups/sqlite/file');
      expect(downloadLink().hasAttribute('download')).toBeTrue();
      expect(ready).toContain('until the next sqlite backup');
      expect(button().disabled).toBeFalse();
      // Done: no more reads.
      jasmine.clock().tick(BACKUP_POLL_MS * 3);
    });

    it('shows a server\'s own backup by its size alone: its tables and rows are never counted', async () => {
      await init();
      component.format.set('bak');
      component.makeBackup();
      expectStart('bak').flush(running('bak'), { status: 202, statusText: 'Accepted' });
      jasmine.clock().tick(BACKUP_POLL_MS);
      flushDownloadStatus({ state: 'Succeeded', format: 'bak', fileName: 'grifball-backup-20261107-054559.bak', tables: null, rows: null, size: 246_693_888 });
      fixture.detectChanges();

      const ready = el().querySelector('.db-ready')!.textContent!.replace(/\s+/g, ' ');
      expect(ready).toContain('235.3 MB · kept for an hour');
      expect(ready).not.toContain('tables');
      jasmine.clock().tick(BACKUP_POLL_MS * 3);
    });

    it('says why the backup failed while it was being made', async () => {
      await init();
      component.format.set('bak');
      component.makeBackup();
      expectStart('bak').flush(running('bak'), { status: 202, statusText: 'Accepted' });
      jasmine.clock().tick(BACKUP_POLL_MS);
      flushDownloadStatus({ state: 'Failed', format: 'bak', tables: 0, rows: 0, message: "SQL Server made the backup but wouldn't read it back." });

      expect(snackBar.open).toHaveBeenCalledWith("SQL Server made the backup but wouldn't read it back.", 'Close');
      expect(component.making()).toBeFalse();
      fixture.detectChanges();
      expect(el().querySelector('.db-progress')!.textContent).toContain("The backup failed: SQL Server made the backup but wouldn't read it back.");
      jasmine.clock().tick(BACKUP_POLL_MS);
      component.makeBackup();
      expectStart('bak').flush(running('bak'), { status: 202, statusText: 'Accepted' });
      jasmine.clock().tick(BACKUP_POLL_MS);
      flushDownloadStatus({ state: 'Failed', format: 'bak', tables: 0, rows: 0 });
      expect(snackBar.open).toHaveBeenCalledWith('The backup failed', 'Close');
    });

    it('says the backup failed when its status could not be read', async () => {
      await init();
      component.makeBackup();
      expectStart('sqlite').flush(running('sqlite'), { status: 202, statusText: 'Accepted' });
      jasmine.clock().tick(BACKUP_POLL_MS);
      http.expectOne(r => /^\/api\/backups\/\w+\/status$/.test(r.url)).flush('', { status: 500, statusText: 'Server Error' });

      expect(snackBar.open).toHaveBeenCalledWith('The backup failed', 'Close');
      expect(component.making()).toBeFalse();
    });

    it('follows a backup already being made when the page opens, without downloading it, until the page closes', async () => {
      await init(none, undefined, running('sqlite'));
      expect(button().disabled).toBeTrue();
      jasmine.clock().tick(BACKUP_POLL_MS);
      flushDownloadStatus(made('sqlite', 'grifball-backup-1.sqlite'));
      fixture.detectChanges();

      // Someone else's (or an earlier visit's): offered, not fetched.
      expect(button().disabled).toBeFalse();
      expect(el().querySelector('.db-ready')!.textContent).toContain('grifball-backup-1.sqlite');
      expect(downloadLink().getAttribute('href')).toBe('api/backups/sqlite/file');
      http.expectNone(r => r.url.endsWith('/file'));

      fixture.destroy();
      jasmine.clock().tick(BACKUP_POLL_MS * 3);
    });

    it('shows the backup of the chosen format: one is kept per format, each read when its format is chosen', async () => {
      await init(none, undefined, made('sqlite', 'grifball-backup-1.sqlite'));
      expect(el().querySelector('.db-ready')!.textContent).toContain('grifball-backup-1.sqlite');

      component.selectFormat('postgres');
      const req = http.expectOne(r => /^\/api\/backups\/\w+\/status$/.test(r.url));
      expect(req.request.url).toBe('/api/backups/postgres/status');
      req.flush(made('postgres', 'grifball-backup-2.postgres.sql'));
      fixture.detectChanges();
      expect(el().querySelector('.db-ready')!.textContent).toContain('grifball-backup-2.postgres.sql');
      expect(downloadLink().getAttribute('href')).toBe('api/backups/postgres/file');

      // A late answer for a format no longer chosen is left out
      component.selectFormat('sqlserver');
      http.expectOne(r => /^\/api\/backups\/\w+\/status$/.test(r.url)).flush(made('postgres', 'grifball-backup-2.postgres.sql'));
      expect(component.downloadStatus()!.format).toBe('postgres');
    });

    it('says the last backup is gone when the server restarted since, with no download', async () => {
      await init(none, undefined, { state: 'None', format: 'sqlite', tables: 0, rows: 0, message: 'The last backup in this format is gone: the server restarted since it was made. Make a new one.' });

      expect(el().querySelector('.db-progress')!.textContent).toContain('the server restarted since it was made');
      expect(el().querySelector('.db-ready')).toBeNull();
      expect(button().disabled).toBeFalse();
    });

    it('says which backup is being made when another format is chosen, and waits for it', async () => {
      await init(none, undefined, { state: 'None', format: 'sqlite', tables: 0, rows: 0, runningFormat: 'postgres' });
      expect(el().textContent).toContain('A postgres backup is being made');
      expect(button().disabled).toBeTrue();

      jasmine.clock().tick(BACKUP_POLL_MS);
      flushDownloadStatus({ state: 'None', format: 'sqlite', tables: 0, rows: 0 });
      fixture.detectChanges();
      expect(el().textContent).not.toContain('being made');
      expect(button().disabled).toBeFalse();
    });

    it('stops reading the status when the page closes', async () => {
      await init(none, undefined, running('sqlite'));
      jasmine.clock().tick(BACKUP_POLL_MS);
      flushDownloadStatus(running('sqlite'));

      fixture.destroy();
      jasmine.clock().tick(BACKUP_POLL_MS * 3);
      expect(http.match(r => /^\/api\/backups\/\w+\/status$/.test(r.url))).withContext('status reads after closing').toEqual([]);
    });
  });

  it('says when the details are unavailable', async () => {
    http.expectOne('/api/database-exports/status').flush('', { status: 500, statusText: 'Server Error' });
    http.expectOne(r => /^\/api\/backups\/\w+\/status$/.test(r.url)).flush('', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    http.expectOne('/api/backups/info').flush('', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el().textContent).toContain("Couldn't read the database's details");
    expect(component.formats()).toHaveSize(3);
  });

  it('says a size as people read it', () => {
    flushExportStatus();
    flushDownloadStatus();
    expect([500, 820_000, 1_300_000, 12_400_000, 1_300_000_000].map(DatabaseBackupComponent.sizeText)).toEqual(['1 KB', '801 KB', '1.2 MB', '11.8 MB', '1.2 GB']);
  });

  describe('export to a server', () => {
    const exportButton = () => Array.from(el().querySelectorAll('button')).find(b => b.textContent!.includes('Export')) as HTMLButtonElement;
    const statusText = () => el().querySelector('.db-export-status')?.textContent?.trim() ?? '';

    beforeEach(() => jasmine.clock().install());
    afterEach(() => jasmine.clock().uninstall());

    it('needs a connection string', async () => {
      await init();
      expect(exportButton().disabled).toBeTrue();
      component.connectionString.set('Host=db;Database=Grif');
      fixture.detectChanges();
      expect(exportButton().disabled).toBeFalse();
      expect(statusText()).toBe('');
    });

    it('starts the export, forgets the connection string, and follows it until it is done', async () => {
      await init();
      component.exportTarget.set('SqlServer');
      component.connectionString.set('Server=db;Database=Grif;User Id=sa;Password=x');
      fixture.detectChanges();
      expect((el().querySelector('input[name="connectionString"]') as HTMLInputElement).placeholder).toContain('TrustServerCertificate');

      exportButton().click();
      const req = http.expectOne('/api/database-exports');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ provider: 'SqlServer', connectionString: 'Server=db;Database=Grif;User Id=sa;Password=x' });
      req.flush({ state: 'Running', provider: 'SqlServer', tables: 0, rows: 0 }, { status: 202, statusText: 'Accepted' });
      fixture.detectChanges();

      expect(component.connectionString()).toBe('');
      expect(exportButton().disabled).toBeTrue();
      expect(exportButton().textContent).toContain('Exporting');
      expect(statusText()).toBe('Exporting to SqlServer: waiting to start…');

      jasmine.clock().tick(BACKUP_POLL_MS);
      flushExportStatus({ state: 'Running', tables: 0, rows: 0, progress: 40, progressMessage: 'Copying Matches (26 of 64)' });
      fixture.detectChanges();
      // The job's progress, as its run reports it
      expect(statusText()).toBe('Exporting: Copying Matches (26 of 64)');
      expect((el().querySelector('progress[aria-label="Export progress"]') as HTMLProgressElement).value).toBe(40);
      jasmine.clock().tick(BACKUP_POLL_MS);
      flushExportStatus({ state: 'Succeeded', provider: 'SqlServer', tables: 40, rows: 12345 });
      fixture.detectChanges();

      expect(statusText()).toBe('Exported to SqlServer: 40 tables, 12345 rows, checked.');
      expect(exportButton().textContent).toContain('Export');
      // Done: no more reads.
      jasmine.clock().tick(BACKUP_POLL_MS * 3);
    });

    it('shows why the last export failed', async () => {
      await init({ state: 'Failed', provider: 'Postgres', tables: 0, rows: 0, message: 'The target database already has tables.' });
      expect(statusText()).toBe('The export to Postgres failed: The target database already has tables.');
    });

    it('follows an export already running when the page opens, until the page closes', async () => {
      await init({ state: 'Running', provider: 'Postgres', tables: 0, rows: 0 });
      expect(exportButton().disabled).toBeTrue();
      jasmine.clock().tick(BACKUP_POLL_MS);
      flushExportStatus({ state: 'Running', provider: 'Postgres', tables: 0, rows: 0 });

      fixture.destroy();
      jasmine.clock().tick(BACKUP_POLL_MS * 3);
    });

    it("says why the server didn't start it", async () => {
      await init();
      component.connectionString.set('x');
      component.startExport();
      http.expectOne('/api/database-exports').flush('A backup or export is already running; try again when it\'s done.', { status: 409, statusText: 'Conflict' });
      expect(snackBar.open).toHaveBeenCalledWith('A backup or export is already running; try again when it\'s done.', 'Close');
      expect(component.exporting()).toBeFalse();
      expect(component.connectionString()).toBe('x');

      component.startExport();
      http.expectOne('/api/database-exports').error(new ProgressEvent('error'));
      expect(snackBar.open).toHaveBeenCalledWith('The export failed to start', 'Close');
    });
  });
});
