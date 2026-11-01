import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpHeaders, HttpResponse, provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatabaseBackupComponent } from './databaseBackup.component';

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

  /** Renders the page with api/Backup/Info answered. */
  async function init() {
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
    const response = (header?: string) => new HttpResponse({ headers: header ? new HttpHeaders({ 'Content-Disposition': header }) : new HttpHeaders() });
    expect(DatabaseBackupComponent.fileNameOf(response('attachment; filename="a.sqlite"'))).toBe('a.sqlite');
    expect(DatabaseBackupComponent.fileNameOf(response('attachment; filename=b.sqlite; filename*=UTF-8\'\'b.sqlite'))).toBe('b.sqlite');
    expect(DatabaseBackupComponent.fileNameOf(response())).toBeNull();
  });
});
