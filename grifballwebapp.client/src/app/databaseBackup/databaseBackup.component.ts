import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { errorText } from '../shared/errorText';
import { BackupService } from '@api/services/backup.service';
import type { BackupInfoDto } from '@api/models';

export type BackupFormat = 'sqlite' | 'postgres' | 'sqlserver' | 'bak' | 'dump';

const formatLabels: Record<BackupFormat, string> = {
  sqlite: 'SQLite database (.sqlite)',
  postgres: 'PostgreSQL script (.sql)',
  sqlserver: 'SQL Server script (.sql)',
  bak: 'SQL Server backup (.bak)',
  dump: 'PostgreSQL dump (.dump)',
};
export type ExportTarget = 'Postgres' | 'SqlServer';

/** api/database-exports/status: the last export to a server, this run of the backend's. */
export interface ExportStatus {
  state: 'None' | 'Running' | 'Succeeded' | 'Failed';
  provider?: string | null;
  started?: string | null;
  finished?: string | null;
  tables: number;
  rows: number;
  message?: string | null;
  /** While it runs: how far it got (0 to 100) and what it's on. */
  progress?: number | null;
  progressMessage?: string | null;
}

/** api/backups/{format}/status: that format's backup (one kept a format), being made, made (ready to download) or failed. */
export interface DownloadStatus {
  state: 'None' | 'Running' | 'Succeeded' | 'Failed';
  format?: BackupFormat | null;
  started?: string | null;
  finished?: string | null;
  fileName?: string | null;
  /** What it holds, when counted (a script or SQLite file); null for a server's own backup (.bak, .dump), taken whole. */
  tables?: number | null;
  rows?: number | null;
  message?: string | null;
  /** Made: its size in bytes. */
  size?: number | null;
  /** The format being made now, whichever is shown (one backup or export at a time). */
  runningFormat?: BackupFormat | null;
  /** While it's made: how far it got (0 to 100) and what it's on. */
  progress?: number | null;
  progressMessage?: string | null;
}

/** What each server's connection string looks like, for the export's field. */
export const targetConnectionStrings: Record<ExportTarget, string> = {
  Postgres: 'Host=...;Database=GrifballWebApp;Username=...;Password=...',
  SqlServer: 'Server=...;Database=GrifballWebApp;User Id=...;Password=...;TrustServerCertificate=True',
};

/**
 * Sysadmin: downloads the whole database (api/backups/{format}, followed until it's made, then api/backups/{format}/file) as a SQLite file, a script for PostgreSQL or SQL Server,
 * or a native backup (.bak, .dump: the site's own server's, or the scratch server's), with how to restore
 * each; or exports it straight into a new PostgreSQL or SQL Server
 * database (api/database-exports, followed until it's done).
 */
@Component({
  selector: 'app-database-backup',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSnackBarModule],
  templateUrl: './databaseBackup.component.html',
  styleUrl: './databaseBackup.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DatabaseBackupComponent {
  private backup = inject(BackupService);
  private snackbar = inject(MatSnackBar);
  private pollTimer: ReturnType<typeof setTimeout> | undefined;
  private downloadTimer: ReturnType<typeof setTimeout> | undefined;
  /** The format of the backup this page started, so it says if that one fails. */
  private startedHere: BackupFormat | null = null;

  /** How often a running export's or backup's status is read again (ms). */
  static readonly pollInterval = 2000;

  readonly info = rxResource<BackupInfoDto, void>({ stream: () => this.backup.backupInfo() });
  readonly format = signal<BackupFormat>('sqlite');
  readonly downloadStatus = signal<DownloadStatus | null>(null);
  /** Starting or making a backup. */
  readonly making = signal(false);
  /** The last download's file name, which the restore command reads. */
  readonly fileName = signal<string | null>(null);

  /** The formats the server can make here; the three it always can until it says. */
  readonly formats = computed(() => {
    // The server only ever lists BackupFormat's own values.
    const formats: BackupFormat[] = this.info.hasValue() ? this.info.value().formats as BackupFormat[] : ['sqlite', 'postgres', 'sqlserver'];
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
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.pollTimer);
      clearTimeout(this.downloadTimer);
    });
    this.loadExportStatus();
    this.loadDownloadStatus();
  }

  /** The last export's status; again shortly while it runs. */
  loadExportStatus() {
    this.backup.backupExportStatus().subscribe({
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
    this.backup.backupExport({ body: { provider: this.exportTarget(), connectionString: this.connectionString() } }).subscribe({
      next: status => {
        this.starting.set(false);
        // Not kept in the page once the server has it.
        this.connectionString.set('');
        this.followExport(status);
      },
      error: r => {
        console.log(r);
        this.starting.set(false);
        const reason = errorText(r, 'The export failed to start');
        this.snackbar.open(reason, 'Close');
      }
    });
  }

  /** The backup being made, or the last one; again shortly while one is made. */
  loadDownloadStatus() {
    this.backup.backupDownloadStatus({ format: this.format() }).subscribe({
      // The generated model's format/runningFormat are string, not BackupFormat: the server only ever sends one of its values.
      next: status => this.followDownload(status as unknown as DownloadStatus),
      error: r => {
        console.log(r);
        if (this.startedHere) {
          this.startedHere = null;
          this.making.set(false);
          this.snackbar.open('The backup failed', 'Close');
        }
      },
    });
  }

  /** Shows the format's backup: one is kept a format, so each has its own. */
  selectFormat(format: BackupFormat) {
    this.format.set(format);
    this.loadDownloadStatus();
  }

  private followDownload(status: DownloadStatus) {
    // An answer for a format no longer chosen (it changed meanwhile): the chosen one's is on its way
    if (status.format && status.format !== this.format())
      return;
    this.downloadStatus.set(status);
    clearTimeout(this.downloadTimer);
    if (status.state === 'Running' || status.runningFormat) {
      // Made in the background, since a large one takes longer than a proxy waits for a response.
      this.making.set(true);
      this.downloadTimer = setTimeout(() => this.loadDownloadStatus(), DatabaseBackupComponent.pollInterval);
      return;
    }
    this.making.set(false);
    if (this.startedHere === status.format && status.state === 'Failed')
      this.snackbar.open(status.message || 'The backup failed', 'Close');
    if (this.startedHere === status.format)
      this.startedHere = null;
    // Made: its name for the restore command, and its size, before it's downloaded
    if (status.state === 'Succeeded' && status.fileName)
      this.fileName.set(status.fileName);
  }

  /** Makes a backup in the chosen format on the server (api/backups/{format}), followed until it's made; downloading it is a second step. */
  makeBackup() {
    this.making.set(true);
    this.startedHere = this.format();
    this.backup.backupStartDownload({ format: this.format() }).subscribe({
      next: status => this.followDownload(status as unknown as DownloadStatus),
      error: (r: HttpErrorResponse) => {
        console.log(r);
        this.startedHere = null;
        this.making.set(false);
        // 409: another is running, or the native backup can't be made here: the reason.
        this.snackbar.open(r.status === 409 ? errorText(r, 'The backup failed') : 'The backup failed', 'Close');
      }
    });
  }

  /** A size in bytes as people read it: "820 KB", "12.4 MB", "1.2 GB". */
  static sizeText(bytes: number): string {
    if (bytes < 1024 * 1024)
      return `${Math.max(1, Math.round(bytes / 1024))} KB`;
    if (bytes < 1024 * 1024 * 1024)
      return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${(bytes / 1024 / 1024 / 1024).toFixed(1)} GB`;
  }
  readonly sizeText = DatabaseBackupComponent.sizeText;
}
