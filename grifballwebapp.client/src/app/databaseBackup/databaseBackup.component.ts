import { HttpClient, HttpErrorResponse, HttpResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

export type BackupFormat = 'sqlite' | 'postgres' | 'sqlserver' | 'bak' | 'dump';

/** api/Backup/Info: the server the site's database is on, the build's last migration, and the formats it can download here. */
export interface BackupInfo {
  provider: string;
  latestMigration: string;
  formats: BackupFormat[];
}

const formatLabels: Record<BackupFormat, string> = {
  sqlite: 'SQLite database (.sqlite)',
  postgres: 'PostgreSQL script (.sql)',
  sqlserver: 'SQL Server script (.sql)',
  bak: 'SQL Server backup (.bak)',
  dump: 'PostgreSQL dump (.dump)',
};
export type ExportTarget = 'Postgres' | 'SqlServer';

/** api/Backup/ExportStatus: the last export to a server, this run of the backend's. */
export interface ExportStatus {
  state: 'None' | 'Running' | 'Succeeded' | 'Failed';
  provider?: string | null;
  started?: string | null;
  finished?: string | null;
  tables: number;
  rows: number;
  message?: string | null;
}

/** What each server's connection string looks like, for the export's field. */
export const targetConnectionStrings: Record<ExportTarget, string> = {
  Postgres: 'Host=...;Database=GrifballWebApp;Username=...;Password=...',
  SqlServer: 'Server=...;Database=GrifballWebApp;User Id=...;Password=...;TrustServerCertificate=True',
};

/**
 * Sysadmin: downloads the whole database (api/Backup/Download) as a SQLite file, a script for PostgreSQL or SQL Server,
 * or a native backup (.bak, .dump: the site's own server's, or the scratch server's), with how to restore
 * each; or exports it straight into a new PostgreSQL or SQL Server
 * database (api/Backup/Export, followed until it's done).
 */
@Component({
  selector: 'app-database-backup',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSnackBarModule],
  templateUrl: './databaseBackup.component.html',
  styleUrl: './databaseBackup.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DatabaseBackupComponent {
  private http = inject(HttpClient);
  private snackbar = inject(MatSnackBar);
  private pollTimer: ReturnType<typeof setTimeout> | undefined;

  /** How often a running export's status is read again (ms). */
  static pollInterval = 2000;

  readonly info = httpResource<BackupInfo>(() => 'api/Backup/Info');
  readonly format = signal<BackupFormat>('sqlite');
  readonly downloading = signal(false);
  /** The last download's file name, which the restore command reads. */
  readonly fileName = signal<string | null>(null);

  /** The formats the server can make here; the three it always can until it says. */
  readonly formats = computed(() => {
    const formats: BackupFormat[] = this.info.hasValue() ? this.info.value().formats : ['sqlite', 'postgres', 'sqlserver'];
    return formats.map(value => ({ value, label: formatLabels[value] }));
  });

  /** How to restore the chosen format: a note and the command. */
  readonly restore = computed(() => {
    const format = this.format();
    const extension = format === 'postgres' || format === 'sqlserver' ? 'sql' : format;
    const file = this.fileName()?.endsWith(`.${extension}`) ? this.fileName()! : '<file>';
    switch (format) {
      case 'postgres':
        return { note: 'Into a new, empty database; it all runs in one transaction.', command: `createdb GrifballWebApp\npsql -v ON_ERROR_STOP=1 -d GrifballWebApp -f ${file}` };
      case 'sqlserver':
        return { note: 'Into a new, empty database; it all runs in one transaction.', command: `sqlcmd -S <server> -Q "CREATE DATABASE GrifballWebApp"\nsqlcmd -S <server> -d GrifballWebApp -b -i ${file}` };
      case 'bak':
        return { note: 'Copy the file where SQL Server can read it, then restore it (MOVE its files if the folders differ).', command: `RESTORE DATABASE GrifballWebApp FROM DISK = N'/var/opt/mssql/backup/${file}' WITH RECOVERY` };
      case 'dump':
        return { note: 'Into a new database; owners and grants are left out, so any role can restore it.', command: `createdb GrifballWebApp\npg_restore --no-owner -d GrifballWebApp ${file}` };
      default:
        return { note: 'A working SQLite database: point the site at it.', command: `Database__Provider=Sqlite\nConnectionStrings__GrifballWebApp="Data Source=/data/${file}"` };
    }
  });

  readonly exportTarget = signal<ExportTarget>('Postgres');
  readonly connectionString = signal('');
  readonly exportStatus = signal<ExportStatus | null>(null);
  readonly starting = signal(false);
  readonly exporting = computed(() => this.starting() || this.exportStatus()?.state === 'Running');
  readonly connectionStringPlaceholder = computed(() => targetConnectionStrings[this.exportTarget()]);

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.pollTimer));
    this.loadExportStatus();
  }

  /** The last export's status; again shortly while it runs. */
  loadExportStatus() {
    this.http.get<ExportStatus>('api/Backup/ExportStatus').subscribe({
      next: status => this.followExport(status),
      error: r => console.log(r),
    });
  }

  private followExport(status: ExportStatus) {
    this.exportStatus.set(status);
    clearTimeout(this.pollTimer);
    if (status.state === 'Running')
      this.pollTimer = setTimeout(() => this.loadExportStatus(), DatabaseBackupComponent.pollInterval);
  }

  startExport() {
    this.starting.set(true);
    this.http.post<ExportStatus>('api/Backup/Export', { provider: this.exportTarget(), connectionString: this.connectionString() }).subscribe({
      next: status => {
        this.starting.set(false);
        // Not kept in the page once the server has it.
        this.connectionString.set('');
        this.followExport(status);
      },
      error: r => {
        console.log(r);
        this.starting.set(false);
        const reason = typeof r.error === 'string' && r.error ? r.error : 'The export failed to start';
        this.snackbar.open(reason, 'Close');
      }
    });
  }

  download() {
    this.downloading.set(true);
    this.http.get('api/Backup/Download', { params: { format: this.format() }, responseType: 'blob', observe: 'response' }).subscribe({
      next: r => {
        this.downloading.set(false);
        const name = DatabaseBackupComponent.fileNameOf(r) ?? `grifball-backup.${this.format()}`;
        this.fileName.set(name);
        this.save(r.body!, name);
      },
      error: async (r: HttpErrorResponse) => {
        console.log(r);
        this.downloading.set(false);
        // The reason, which comes as a blob too: 409 another is running, 422 the native backup can't be made here.
        const reason = r.error instanceof Blob ? (await r.error.text()).trim() : '';
        this.snackbar.open(reason && (r.status === 409 || r.status === 422) ? reason : 'The backup failed', 'Close');
      }
    });
  }

  /** The file name in Content-Disposition (filename=, as ASP.NET writes it), if there is one. */
  static fileNameOf(response: HttpResponse<unknown>): string | null {
    const header = response.headers.get('Content-Disposition') ?? '';
    const match = /filename="?([^";]+)"?/i.exec(header);
    return match ? match[1] : null;
  }

  private save(blob: Blob, name: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.click();
    URL.revokeObjectURL(url);
  }
}
