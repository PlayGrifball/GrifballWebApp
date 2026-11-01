import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpHeaders, HttpResponse, provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatabaseBackupComponent, ExportStatus } from './databaseBackup.component';

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

  /** Renders the page with api/Backup/Info answered, and the last export's status. */
  async function init(exportStatus: ExportStatus = none) {
    flushExportStatus(exportStatus);
    fixture.detectChanges();
    http.expectOne('api/Backup/Info').flush({ provider: 'SqlServer', latestMigration: '20261031000000_Last' });
    http.expectOne('/api/CommitHash').flush('abc1234');
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it("shows the database's server and migration", async () => {
    await init();
    expect(el().querySelector('.db-info')!.textContent).toContain('SqlServer');
    expect(el().querySelector('.db-info')!.textContent).toContain('20261031000000_Last');
  });

  it('downloads the backup under the name the server gives it', async () => {
    await init();
    const click = spyOn(HTMLAnchorElement.prototype, 'click');
    spyOn(URL, 'createObjectURL').and.returnValue('blob:backup');
    spyOn(URL, 'revokeObjectURL');

    button().click();
    fixture.detectChanges();
    expect(button().disabled).toBeTrue();
    expect(button().textContent).toContain('Making the backup');

    const req = http.expectOne('api/Backup/Sqlite');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['SQLite format 3']), {
      headers: { 'Content-Disposition': 'attachment; filename=grifball-backup-20261101-120000.sqlite; filename*=UTF-8\'\'grifball-backup-20261101-120000.sqlite' }
    });
    fixture.detectChanges();

    expect(click).toHaveBeenCalledTimes(1);
    expect((click.calls.mostRecent().object as HTMLAnchorElement).download).toBe('grifball-backup-20261101-120000.sqlite');
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:backup');
    expect(button().disabled).toBeFalse();
    expect(el().querySelector('.db-command')!.textContent).toContain('Data Source=/backup/grifball-backup-20261101-120000.sqlite');
  });

  it('keeps its own file name when the server gives none', async () => {
    await init();
    const click = spyOn(HTMLAnchorElement.prototype, 'click');
    spyOn(URL, 'createObjectURL').and.returnValue('blob:backup');
    spyOn(URL, 'revokeObjectURL');

    component.download();
    http.expectOne('api/Backup/Sqlite').flush(new Blob(['SQLite format 3']));

    expect((click.calls.mostRecent().object as HTMLAnchorElement).download).toBe('grifball-backup.sqlite');
  });

  it('says when another backup is being made', async () => {
    await init();
    component.download();
    http.expectOne('api/Backup/Sqlite').flush(new Blob(), { status: 409, statusText: 'Conflict' });
    expect(snackBar.open).toHaveBeenCalledWith('A backup is already being made; try again shortly', 'Close');
    expect(component.downloading()).toBeFalse();
  });

  it('says when the backup failed', async () => {
    await init();
    component.download();
    http.expectOne('api/Backup/Sqlite').flush(new Blob(), { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('The backup failed', 'Close');
  });

  it('says when the details and the build are unavailable', async () => {
    http.expectOne('api/Backup/ExportStatus').flush('', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    http.expectOne('api/Backup/Info').flush('', { status: 500, statusText: 'Server Error' });
    http.expectOne('/api/CommitHash').flush('', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(el().textContent).toContain("Couldn't read the database's details");
    expect(component.restoreCommand()).toContain("grifballwebappserver:<this site's tag> ");
  });

  it('gives the restore command for each server, and none for SQLite', async () => {
    await init();
    expect(component.restoreCommand()).toContain('ghcr.io/playgrifball/grifballwebappserver:sha-abc1234 ');
    expect(component.restoreCommand()).toContain('--Target:Provider=Postgres "--Target:ConnectionString=Host=');

    component.target.set('SqlServer');
    expect(component.restoreCommand()).toContain('--Target:Provider=SqlServer "--Target:ConnectionString=Server=');

    component.target.set('Sqlite');
    fixture.detectChanges();
    expect(component.restoreCommand()).toBe('');
    expect(el().querySelector('.db-command')).toBeNull();
    expect(el().textContent).toContain('Database__Provider=Sqlite');
  });

  it('reads the file name from Content-Disposition, quoted or not', () => {
    http.expectOne('/api/CommitHash').flush('abc1234');
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
