import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { BACKUP_POLL_MS, BackupFormat, DatabaseBackupStore, ExportTarget } from './databaseBackup.store';

export type { BackupFormat, DownloadStatus, ExportStatus, ExportTarget } from './databaseBackup.store';

const formatLabels: Record<BackupFormat, string> = {
  sqlite: 'SQLite database (.sqlite)',
  postgres: 'PostgreSQL script (.sql)',
  sqlserver: 'SQL Server script (.sql)',
  bak: 'SQL Server backup (.bak)',
  dump: 'PostgreSQL dump (.dump)',
};
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
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DatabaseBackupStore],
})
export class DatabaseBackupComponent {
  private readonly store = inject(DatabaseBackupStore);

  /** How often a running export's or backup's status is read again (ms). */
  static readonly pollInterval = BACKUP_POLL_MS;

  readonly info = this.store.infoValue;
  readonly infoFailed = this.store.infoFailed;
  readonly format = signal<BackupFormat>('sqlite');
  readonly downloadStatus = this.store.downloadStatus;
  /** Starting or making a backup. */
  readonly making = this.store.making;
  /** The last download's file name, which the restore command reads. */
  readonly fileName = linkedSignal(() => this.store.fileName());

  /** The formats the server can make here; the three it always can until it says. */
  readonly formats = computed(() => {
    // The server only ever lists BackupFormat's own values.
    const formats: BackupFormat[] = (this.info()?.formats as BackupFormat[] | undefined) ?? ['sqlite', 'postgres', 'sqlserver'];
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
  readonly exportStatus = this.store.exportStatus;
  readonly exporting = this.store.exporting;
  readonly connectionStringPlaceholder = computed(() => targetConnectionStrings[this.exportTarget()]);

  constructor() {
    this.store.format.use(this.format);
    this.store.loadExportStatus();
    this.store.loadDownloadStatus();
  }

  startExport() {
    // Not kept in the page once the server has it.
    this.store.startExport(this.exportTarget(), this.connectionString(), () => this.connectionString.set(''));
  }

  /** Shows a format's backup: one is kept a format, so each has its own. */
  selectFormat(format: BackupFormat) {
    this.format.set(format);
    this.store.loadDownloadStatus();
  }

  /** Makes a backup in the chosen format on the server (api/backups/{format}), followed until it's made; downloading it is a second step. */
  makeBackup() {
    this.store.makeBackup();
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
