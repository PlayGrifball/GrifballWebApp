import { HttpClient, HttpResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiClientService } from '../api/apiClient.service';

/** api/Backup/Info: the server the site's database is on, and the build's last migration. */
export interface BackupInfo {
  provider: string;
  latestMigration: string;
}

export type RestoreTarget = 'Postgres' | 'SqlServer' | 'Sqlite';
export type ExportTarget = Exclude<RestoreTarget, 'Sqlite'>;

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

/** What each server's connection string looks like, for the restore command and the export's field. */
export const targetConnectionStrings: Record<ExportTarget, string> = {
  Postgres: 'Host=...;Database=GrifballWebApp;Username=...;Password=...',
  SqlServer: 'Server=...;Database=GrifballWebApp;User Id=...;Password=...;TrustServerCertificate=True',
};

/**
 * Sysadmin: downloads the whole database as a SQLite file (api/Backup/Sqlite), exports it straight into a new
 * PostgreSQL or SQL Server database (api/Backup/Export, followed until it's done), and shows how to load a download
 * into either with the converter in the site's image.
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

  static readonly image = 'ghcr.io/playgrifball/grifballwebappserver';

  readonly info = httpResource<BackupInfo>(() => 'api/Backup/Info');
  /** The build's short hash: its image is tagged sha-<hash>, and its converter takes its backups. */
  readonly commitHash = toSignal(inject(ApiClientService).commitHash().pipe(catchError(() => of(null))), { initialValue: null });
  readonly imageTag = computed(() => this.commitHash() ? `sha-${this.commitHash()}` : "<this site's tag>");
  readonly downloading = signal(false);
  /** The last download's file name, which the restore command reads. */
  readonly fileName = signal('grifball-backup.sqlite');
  readonly target = signal<RestoreTarget>('Postgres');

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

  readonly restoreCommand = computed(() => {
    const target = this.target();
    if (target === 'Sqlite')
      return '';
    return [
      `docker run --rm -u "$(id -u):$(id -g)" -v "$PWD:/backup" --entrypoint dotnet ${DatabaseBackupComponent.image}:${this.imageTag()} \\`,
      `  /app/convert/GrifballWebApp.Convert.dll \\`,
      `  --Source:Provider=Sqlite "--Source:ConnectionString=Data Source=/backup/${this.fileName()}" \\`,
      `  --Target:Provider=${target} "--Target:ConnectionString=${targetConnectionStrings[target]}"`,
    ].join('\n');
  });

  download() {
    this.downloading.set(true);
    this.http.get('api/Backup/Sqlite', { responseType: 'blob', observe: 'response' }).subscribe({
      next: r => {
        this.downloading.set(false);
        const name = DatabaseBackupComponent.fileNameOf(r) ?? this.fileName();
        this.fileName.set(name);
        this.save(r.body!, name);
      },
      error: r => {
        console.log(r);
        this.downloading.set(false);
        this.snackbar.open(r.status === 409 ? 'A backup is already being made; try again shortly' : 'The backup failed', 'Close');
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
