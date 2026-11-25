import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** A page of the sysadmins' tools, linked from the SysAdmin page. */
export interface SysAdminTool {
  title: string;
  path: string;
  description: string;
}

/** Sysadmin: the site's own running (imports, jobs, telemetry, the Halo client, backups), apart from its users. */
@Component({
  selector: 'app-sys-admin',
  imports: [RouterLink],
  templateUrl: './sysAdmin.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SysAdminComponent {
  readonly tools: readonly SysAdminTool[] = [
    { title: 'Historical Import', path: '/sysadmin/historicalimport', description: 'Import past seasons from the historical archive.' },
    { title: 'Jobs', path: '/sysadmin/jobs', description: 'The recurring jobs: when they run, their state and their runs.' },
    { title: 'Telemetry', path: '/sysadmin/telemetry', description: "The app's own logs, metrics and traces." },
    { title: 'Infinite Client', path: '/sysadmin/infiniteclient', description: "The Halo Infinite client's mode, status and sign-in." },
    { title: 'Database Backup', path: '/sysadmin/databasebackup', description: 'Download the database, or export it to another server.' },
  ];
}
