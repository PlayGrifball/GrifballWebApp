import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpHeaders, HttpResponse, provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BackupFormat, DatabaseBackupComponent, ExportStatus } from './databaseBackup.component';

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

  const none: ExportStatus = { state: 'None', tables: 0, rows: 0 };
  const flushExportStatus = (status: ExportStatus = none) => http.expectOne('api/Backup/ExportStatus').flush(status);

  /** Renders the page with api/Backup/Info answered (the formats it can download), and the last export's status. */
  async function init(exportStatus: ExportStatus = none, formats: BackupFormat[] = ['sqlite', 'postgres', 'sqlserver', 'bak', 'dump']) {
    flushExportStatus(exportStatus);
    fixture.detectChanges();
    http.expectOne('api/Backup/Info').flush({ provider: 'SqlServer', latestMigration: '20261031000000_Last', formats });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const expectDownload = (format: string) => {
    const req = http.expectOne(r => r.url === 'api/Backup/Download');
    expect(req.request.params.get('format')).toBe(format);
    expect(req.request.responseType).toBe('blob');
    return req;
  };

  /** The snack bar's message, once the error's blob has been read. */
  async function snackBarMessage(): Promise<string> {
    for (let i = 0; i < 50 && !snackBar.open.calls.any(); i++)
      await new Promise(resolve => setTimeout(resolve, 10));
    return snackBar.open.calls.mostRecent().args[0];
  }

  it("shows the database's server and migration", async () => {
    await init();
    expect(el().querySelector('.db-info')!.textContent).toContain('SqlServer');
    expect(el().querySelector('.db-info')!.textContent).toContain('20261031000000_Last');
  });

  it('downloads the backup under the name the server gives it, and says how to restore it', async () => {
    await init();
    const click = spyOn(HTMLAnchorElement.prototype, 'click');
    spyOn(URL, 'createObjectURL').and.returnValue('blob:backup');
    spyOn(URL, 'revokeObjectURL');
    expect(el().querySelector('.db-command')!.textContent).toContain('Data Source=/data/<file>');

    button().click();
    fixture.detectChanges();
    expect(button().disabled).toBeTrue();
    expect(button().textContent).toContain('Making the backup');

    expectDownload('sqlite').flush(new Blob(['SQLite format 3']), {
      headers: { 'Content-Disposition': 'attachment; filename=grifball-backup-20261101-120000.sqlite; filename*=UTF-8\'\'grifball-backup-20261101-120000.sqlite' }
    });
    fixture.detectChanges();

    expect(click).toHaveBeenCalledTimes(1);
    expect((click.calls.mostRecent().object as HTMLAnchorElement).download).toBe('grifball-backup-20261101-120000.sqlite');
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:backup');
    expect(button().disabled).toBeFalse();
    expect(el().querySelector('.db-command')!.textContent).toContain('Data Source=/data/grifball-backup-20261101-120000.sqlite');
  });

  it('downloads the format chosen, and keeps its own file name when the server gives none', async () => {
    await init();
    const click = spyOn(HTMLAnchorElement.prototype, 'click');
    spyOn(URL, 'createObjectURL').and.returnValue('blob:backup');
    spyOn(URL, 'revokeObjectURL');

    component.format.set('postgres');
    component.download();
    expectDownload('postgres').flush(new Blob(['-- GrifballWebApp database export']));

    expect((click.calls.mostRecent().object as HTMLAnchorElement).download).toBe('grifball-backup.postgres');
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
    component.fileName.set('grifball-backup-1.postgres.sql');
    component.format.set('postgres');
    expect(component.restore().command).toBe('createdb GrifballWebApp\npsql -v ON_ERROR_STOP=1 -d GrifballWebApp -f grifball-backup-1.postgres.sql');
    component.format.set('sqlserver');
    expect(component.restore().command).toContain('sqlcmd -S <server> -d GrifballWebApp -b -i grifball-backup-1.postgres.sql');
    component.format.set('bak');
    expect(component.restore().command).toBe("RESTORE DATABASE GrifballWebApp FROM DISK = N'/var/opt/mssql/backup/<file>' WITH RECOVERY");
    component.fileName.set('grifball-backup-1.bak');
    expect(component.restore().command).toContain("FROM DISK = N'/var/opt/mssql/backup/grifball-backup-1.bak'");
    fixture.detectChanges();
    expect(el().querySelector('.db-restore-note')!.textContent).toContain('Copy the file where SQL Server can read it');
  });

  it('says how to restore a PostgreSQL dump', async () => {
    await init();
    component.format.set('dump');
    component.fileName.set('grifball-backup-1.dump');
    expect(component.restore().command).toBe('createdb GrifballWebApp\npg_restore --no-owner -d GrifballWebApp grifball-backup-1.dump');
  });

  it("gives the server's reason when it can't make the backup", async () => {
    await init();
    component.format.set('dump');
    component.download();
    expectDownload('dump').flush(new Blob(["The app's database login can't make or read SQL Server backups."]), { status: 422, statusText: 'Unprocessable Entity' });
    expect(await snackBarMessage()).toBe("The app's database login can't make or read SQL Server backups.");
    expect(component.downloading()).toBeFalse();
  });

  it('says when another backup is being made', async () => {
    await init();
    component.download();
    expectDownload('sqlite').flush(new Blob(['A backup or export is already running; try again when it\'s done.']), { status: 409, statusText: 'Conflict' });
    expect(await snackBarMessage()).toBe('A backup or export is already running; try again when it\'s done.');
  });

  it('says when the backup failed', async () => {
    await init();
    component.download();
    expectDownload('sqlite').flush(new Blob(['stack trace']), { status: 500, statusText: 'Server Error' });
    expect(await snackBarMessage()).toBe('The backup failed');
  });

  it('says the backup failed when the server never answered', async () => {
    await init();
    component.download();
    expectDownload('sqlite').error(new ProgressEvent('error'));
    expect(await snackBarMessage()).toBe('The backup failed');
  });

  it('says when the details are unavailable', async () => {
    http.expectOne('api/Backup/ExportStatus').flush('', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    http.expectOne('api/Backup/Info').flush('', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el().textContent).toContain("Couldn't read the database's details");
    expect(component.formats().length).toBe(3);
  });

  it('reads the file name from Content-Disposition, quoted or not', () => {
    flushExportStatus();
    const response = (header?: string) => new HttpResponse({ headers: header ? new HttpHeaders({ 'Content-Disposition': header }) : new HttpHeaders() });
    expect(DatabaseBackupComponent.fileNameOf(response('attachment; filename="a.sqlite"'))).toBe('a.sqlite');
    expect(DatabaseBackupComponent.fileNameOf(response('attachment; filename=b.sqlite; filename*=UTF-8\'\'b.sqlite'))).toBe('b.sqlite');
    expect(DatabaseBackupComponent.fileNameOf(response())).toBeNull();
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
      const req = http.expectOne('api/Backup/Export');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ provider: 'SqlServer', connectionString: 'Server=db;Database=Grif;User Id=sa;Password=x' });
      req.flush({ state: 'Running', provider: 'SqlServer', tables: 0, rows: 0 }, { status: 202, statusText: 'Accepted' });
      fixture.detectChanges();

      expect(component.connectionString()).toBe('');
      expect(exportButton().disabled).toBeTrue();
      expect(exportButton().textContent).toContain('Exporting');
      expect(statusText()).toBe('Exporting to SqlServer…');

      jasmine.clock().tick(DatabaseBackupComponent.pollInterval);
      flushExportStatus({ state: 'Running', provider: 'SqlServer', tables: 0, rows: 0 });
      jasmine.clock().tick(DatabaseBackupComponent.pollInterval);
      flushExportStatus({ state: 'Succeeded', provider: 'SqlServer', tables: 40, rows: 12345 });
      fixture.detectChanges();

      expect(statusText()).toBe('Exported to SqlServer: 40 tables, 12345 rows, checked.');
      expect(exportButton().textContent).toContain('Export');
      // Done: no more reads.
      jasmine.clock().tick(DatabaseBackupComponent.pollInterval * 3);
    });

    it('shows why the last export failed', async () => {
      await init({ state: 'Failed', provider: 'Postgres', tables: 0, rows: 0, message: 'The target database already has tables.' });
      expect(statusText()).toBe('The export to Postgres failed: The target database already has tables.');
    });

    it('follows an export already running when the page opens, until the page closes', async () => {
      await init({ state: 'Running', provider: 'Postgres', tables: 0, rows: 0 });
      expect(exportButton().disabled).toBeTrue();
      jasmine.clock().tick(DatabaseBackupComponent.pollInterval);
      flushExportStatus({ state: 'Running', provider: 'Postgres', tables: 0, rows: 0 });

      fixture.destroy();
      jasmine.clock().tick(DatabaseBackupComponent.pollInterval * 3);
    });

    it("says why the server didn't start it", async () => {
      await init();
      component.connectionString.set('x');
      component.startExport();
      http.expectOne('api/Backup/Export').flush('A backup or export is already running; try again when it\'s done.', { status: 409, statusText: 'Conflict' });
      expect(snackBar.open).toHaveBeenCalledWith('A backup or export is already running; try again when it\'s done.', 'Close');
      expect(component.exporting()).toBeFalse();
      expect(component.connectionString()).toBe('x');

      component.startExport();
      http.expectOne('api/Backup/Export').error(new ProgressEvent('error'));
      expect(snackBar.open).toHaveBeenCalledWith('The export failed to start', 'Close');
    });
  });
});
