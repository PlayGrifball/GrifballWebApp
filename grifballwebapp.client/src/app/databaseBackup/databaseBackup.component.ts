import { HttpClient, HttpResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiClientService } from '../api/apiClient.service';

/** api/Backup/Info: the server the site's database is on, and the build's last migration. */
export interface BackupInfo {
  provider: string;
  latestMigration: string;
}

export type RestoreTarget = 'Postgres' | 'SqlServer' | 'Sqlite';

/** What each server's connection string looks like, for the restore command. */
const targetConnectionStrings: Record<Exclude<RestoreTarget, 'Sqlite'>, string> = {
  Postgres: 'Host=...;Database=GrifballWebApp;Username=...;Password=...',
  SqlServer: 'Server=...;Database=GrifballWebApp;User Id=...;Password=...;TrustServerCertificate=True',
};

/**
 * Sysadmin: downloads the whole database as a SQLite file (api/Backup/Sqlite), and shows how to load it into
 * PostgreSQL or SQL Server with the converter in the site's image.
 */
@Component({
  selector: 'app-database-backup',
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatSelectModule, MatSnackBarModule],
  templateUrl: './databaseBackup.component.html',
  styleUrl: './databaseBackup.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DatabaseBackupComponent {
  private http = inject(HttpClient);
  private snackbar = inject(MatSnackBar);

  static readonly image = 'ghcr.io/playgrifball/grifballwebappserver';

  readonly info = httpResource<BackupInfo>(() => 'api/Backup/Info');
  /** The build's short hash: its image is tagged sha-<hash>, and its converter takes its backups. */
  readonly commitHash = toSignal(inject(ApiClientService).commitHash().pipe(catchError(() => of(null))), { initialValue: null });
  readonly imageTag = computed(() => this.commitHash() ? `sha-${this.commitHash()}` : "<this site's tag>");
  readonly downloading = signal(false);
  /** The last download's file name, which the restore command reads. */
  readonly fileName = signal('grifball-backup.sqlite');
  readonly target = signal<RestoreTarget>('Postgres');

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
