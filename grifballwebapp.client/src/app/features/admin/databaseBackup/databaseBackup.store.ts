import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { BackupDownloadStatus, BackupExportStatus, BackupInfoDto } from '@api/models';
import { BackupService } from '@api/services/backup.service';
import { errorText } from '@shared/errorText';
import { StoreInput } from '@shared/storeInput';

export type BackupFormat = 'sqlite' | 'postgres' | 'sqlserver' | 'bak' | 'dump';
export type ExportTarget = 'Postgres' | 'SqlServer';

/** api/database-exports/status: the last export to a server, this run of the backend's. */
export type ExportStatus = BackupExportStatus;

/** api/backups/{format}/status: that format's backup (one kept a format), being made, made (ready to download) or failed. */
export type DownloadStatus = BackupDownloadStatus;

/** How often a running export's or backup's status is read again (ms). */
export const BACKUP_POLL_MS = 2000;

/**
 * The database's backups and exports: the database's details, the chosen format's backup (made on the server, followed
 * until it's made), and the export to another server (followed until it's done). One per page (the component provides it).
 */
@Injectable()
export class DatabaseBackupStore {
  private readonly backup = inject(BackupService);
  private readonly snackbar = inject(MatSnackBar);
  private pollTimer: ReturnType<typeof setTimeout> | undefined;
  private downloadTimer: ReturnType<typeof setTimeout> | undefined;
  /** The format of the backup this page started, so it says if that one fails. */
  private startedHere: BackupFormat | null = null;

  /** The format chosen: the component's select. */
  readonly format = new StoreInput<BackupFormat>();

  private readonly info = rxResource<BackupInfoDto, void>({ stream: () => this.backup.backupInfo() });
  /** The database's details; null until they arrive. */
  readonly infoValue = computed(() => this.info.hasValue() ? this.info.value() : null);
  readonly infoFailed = computed(() => !!this.info.error());

  private readonly _downloadStatus = signal<DownloadStatus | null>(null);
  readonly downloadStatus = this._downloadStatus.asReadonly();
  /** Starting or making a backup. */
  private readonly _making = signal(false);
  readonly making = this._making.asReadonly();
  /** The last backup's file name, which the restore command reads. */
  private readonly _fileName = signal<string | null>(null);
  readonly fileName = this._fileName.asReadonly();

  private readonly _exportStatus = signal<ExportStatus | null>(null);
  readonly exportStatus = this._exportStatus.asReadonly();
  private readonly _starting = signal(false);
  readonly starting = this._starting.asReadonly();
  readonly exporting = computed(() => this.starting() || this.exportStatus()?.state === 'Running');

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.pollTimer);
      clearTimeout(this.downloadTimer);
    });
  }

  /** The last export's status; again shortly while it runs. */
  loadExportStatus(): void {
    this.backup.backupExportStatus().subscribe({
      next: status => this.followExport(status),
      error: r => console.log(r),
    });
  }

  /** Exports the database into another server's; `started` runs once the server has the connection string. */
  startExport(provider: ExportTarget, connectionString: string, started: () => void): void {
    this._starting.set(true);
    this.backup.backupExport({ body: { provider, connectionString } }).subscribe({
      next: status => {
        this._starting.set(false);
        started();
        this.followExport(status);
      },
      error: r => {
        console.log(r);
        this._starting.set(false);
        this.snackbar.open(errorText(r, 'The export failed to start'), 'Close');
      },
    });
  }

  /** The chosen format's backup being made, or the last one; again shortly while one is made. */
  loadDownloadStatus(): void {
    const format = this.format.value();
    if (!format)
      return;
    this.backup.backupDownloadStatus({ format }).subscribe({
      next: status => this.followDownload(status),
      error: r => {
        console.log(r);
        if (this.startedHere) {
          this.startedHere = null;
          this._making.set(false);
          this.snackbar.open('The backup failed', 'Close');
        }
      },
    });
  }

  /** Makes a backup in the chosen format on the server, followed until it's made; downloading it is a second step. */
  makeBackup(): void {
    const format = this.format.value();
    if (!format)
      return;
    this._making.set(true);
    this.startedHere = format;
    this.backup.backupStartDownload({ format }).subscribe({
      next: status => this.followDownload(status),
      error: (r: HttpErrorResponse) => {
        console.log(r);
        this.startedHere = null;
        this._making.set(false);
        // 409: another is running, or the native backup can't be made here: the reason.
        this.snackbar.open(r.status === 409 ? errorText(r, 'The backup failed') : 'The backup failed', 'Close');
      },
    });
  }

  private followExport(status: ExportStatus): void {
    this._exportStatus.set(status);
    clearTimeout(this.pollTimer);
    if (status.state === 'Running')
      this.pollTimer = setTimeout(() => this.loadExportStatus(), BACKUP_POLL_MS);
  }

  private followDownload(status: DownloadStatus): void {
    // An answer for a format no longer chosen (it changed meanwhile): the chosen one's is on its way
    if (status.format && status.format !== this.format.value())
      return;
    this._downloadStatus.set(status);
    clearTimeout(this.downloadTimer);
    if (status.state === 'Running' || status.runningFormat) {
      // Made in the background, since a large one takes longer than a proxy waits for a response.
      this._making.set(true);
      this.downloadTimer = setTimeout(() => this.loadDownloadStatus(), BACKUP_POLL_MS);
      return;
    }
    this._making.set(false);
    if (this.startedHere === status.format && status.state === 'Failed')
      this.snackbar.open(status.message || 'The backup failed', 'Close');
    if (this.startedHere === status.format)
      this.startedHere = null;
    // Made: its name for the restore command, and its size, before it's downloaded
    if (status.state === 'Succeeded' && status.fileName)
      this._fileName.set(status.fileName);
  }
}
