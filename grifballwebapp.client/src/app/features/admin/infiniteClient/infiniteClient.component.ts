import { Component, OnInit, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { ActivatedRoute } from '@angular/router';
import { AdminService } from '@api/services/admin.service';
import type { HaloInfiniteMode, HaloStatusDto } from '@api/models';

export type HaloMode = HaloInfiniteMode;

/** api/halo/status: the active mode and, in Gateway mode, the gateway's status. */
export type HaloStatus = HaloStatusDto;

@Component({
    selector: 'app-infinite-client',
    imports: [
        MatSnackBarModule,
        MatInputModule,
        MatFormFieldModule,
        FormsModule,
        MatButtonModule,
        ErrorMessageComponent
    ],
    templateUrl: './infiniteClient.component.html',
    styleUrl: './infiniteClient.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class InfiniteClientComponent implements OnInit {
  private admin = inject(AdminService);
  private snackbar = inject(MatSnackBar);
  private route = inject(ActivatedRoute);

  /** Where the gateway's Halo account is signed in, when the server doesn't say. */
  static readonly defaultGatewayAdminUrl = 'https://grunt.grifball.xyz/admin';

  code: string = "";
  /** Direct mode: what api/halo/check said ("" while checking). */
  readonly status = signal("");
  /** The server's mode; null until it answers. */
  readonly mode = signal<HaloMode | null>(null);
  /** Gateway mode: the gateway's status. */
  readonly gateway = signal<HaloStatus | null>(null);
  readonly checking = signal(false);

  ngOnInit(): void {
    this.loadStatus();

    const c = String(this.route.snapshot.queryParamMap.get('code'));
    if (c !== "null")
      this.code = c;
  }

  /**
   * Which mode is active. Gateway: the gateway's status (a fresh probe each time). Direct, or a server that can't
   * say: the page as it always was, with the app's own sign-in.
   */
  loadStatus() {
    this.checking.set(true);
    this.admin.haloStatus().subscribe({
      next: r => {
        this.checking.set(false);
        this.mode.set(r.mode);
        if (r.mode === 'Gateway') {
          this.gateway.set(r);
        } else if (this.status() === '') {
          this.checkStatus();
        }
      },
      error: r => {
        console.log(r);
        this.checking.set(false);
        if (this.mode() === 'Gateway') {
          this.snackbar.open('Failed to check the Halo gateway', "Close");
          return;
        }
        this.mode.set('Direct');
        this.checkStatus();
      }
    })
  }

  gatewayAdminUrl(): string {
    return this.gateway()?.gatewayAdminUrl || InfiniteClientComponent.defaultGatewayAdminUrl;
  }

  setCode() {
    // In the body: a code in the URL would be kept by the access logs and traces
    this.admin.setCode({ body: { code: this.code } }).subscribe({
      next: r => this.snackbar.open('Success', "Close"),
      error: r => {
        console.log(r);
        this.snackbar.open('Failed to set code', "Close");
      }
    })
  }

  checkStatus() {
    this.admin.checkStatus().subscribe({
      next: r => this.status.set(r),
      error: r => {
        console.log(r);
        this.status.set("Failed to check status");
        this.snackbar.open('Failed to check status', "Close");
      }
    })
  }

  deleteTokens() {
    this.admin.deleteTokens().subscribe({
      next: r => this.snackbar.open('Success', "Close"),
      error: r => {
        console.log(r);
        this.snackbar.open('Failed to delete tokens', "Close");
      }
    })
  }
}
