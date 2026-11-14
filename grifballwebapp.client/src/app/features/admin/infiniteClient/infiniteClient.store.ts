import { inject, Injectable, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { HaloInfiniteMode, HaloStatusDto } from '@api/models';
import { AdminService } from '@api/services/admin.service';

/**
 * The server's Halo Infinite client: which mode is active, the gateway's status (Gateway mode) or the app's own
 * sign-in (Direct mode), setting its sign-in code and deleting its tokens. One per page (the component provides it).
 */
@Injectable()
export class InfiniteClientStore {
  private readonly admin = inject(AdminService);
  private readonly snackbar = inject(MatSnackBar);

  /** Direct mode: what api/halo/check said ("" while checking). */
  private readonly _status = signal('');
  readonly status = this._status.asReadonly();
  /** The server's mode; null until it answers. */
  private readonly _mode = signal<HaloInfiniteMode | null>(null);
  readonly mode = this._mode.asReadonly();
  /** Gateway mode: the gateway's status. */
  private readonly _gateway = signal<HaloStatusDto | null>(null);
  readonly gateway = this._gateway.asReadonly();
  private readonly _checking = signal(false);
  readonly checking = this._checking.asReadonly();

  /**
   * Which mode is active. Gateway: the gateway's status (a fresh probe each time). Direct, or a server that can't
   * say: the app's own sign-in is checked.
   */
  loadStatus(): void {
    this._checking.set(true);
    this.admin.haloStatus().subscribe({
      next: r => {
        this._checking.set(false);
        this._mode.set(r.mode);
        if (r.mode === 'Gateway') {
          this._gateway.set(r);
        } else if (this.status() === '') {
          this.checkStatus();
        }
      },
      error: r => {
        console.log(r);
        this._checking.set(false);
        if (this.mode() === 'Gateway') {
          this.snackbar.open('Failed to check the Halo gateway', 'Close');
          return;
        }
        this._mode.set('Direct');
        this.checkStatus();
      },
    });
  }

  /** Signs the app's client in with a code from Microsoft's sign-in. */
  setCode(code: string): void {
    // In the body: a code in the URL would be kept by the access logs and traces
    this.admin.setCode({ body: { code } }).subscribe({
      next: () => this.snackbar.open('Success', 'Close'),
      error: r => {
        console.log(r);
        this.snackbar.open('Failed to set code', 'Close');
      },
    });
  }

  checkStatus(): void {
    this.admin.checkStatus().subscribe({
      next: r => this._status.set(r),
      error: r => {
        console.log(r);
        this._status.set('Failed to check status');
        this.snackbar.open('Failed to check status', 'Close');
      },
    });
  }

  deleteTokens(): void {
    this.admin.deleteTokens().subscribe({
      next: () => this.snackbar.open('Success', 'Close'),
      error: r => {
        console.log(r);
        this.snackbar.open('Failed to delete tokens', 'Close');
      },
    });
  }
}
