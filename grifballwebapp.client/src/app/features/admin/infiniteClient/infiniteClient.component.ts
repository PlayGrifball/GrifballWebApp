import { Component, OnInit, ChangeDetectionStrategy, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { ActivatedRoute } from '@angular/router';
import type { HaloInfiniteMode, HaloStatusDto } from '@api/models';
import { InfiniteClientStore } from './infiniteClient.store';

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
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [InfiniteClientStore],
})
export class InfiniteClientComponent implements OnInit {
  private readonly store = inject(InfiniteClientStore);
  private route = inject(ActivatedRoute);

  /** Where the gateway's Halo account is signed in, when the server doesn't say. */
  static readonly defaultGatewayAdminUrl = 'https://grunt.grifball.xyz/admin';

  code: string = "";
  /** Direct mode: what api/halo/check said ("" while checking). */
  readonly status = this.store.status;
  /** The server's mode; null until it answers. */
  readonly mode = this.store.mode;
  /** Gateway mode: the gateway's status. */
  readonly gateway = this.store.gateway;
  readonly checking = this.store.checking;

  ngOnInit(): void {
    this.loadStatus();

    const c = String(this.route.snapshot.queryParamMap.get('code'));
    if (c !== "null")
      this.code = c;
  }

  /** Which mode is active, and its status. */
  loadStatus() {
    this.store.loadStatus();
  }

  gatewayAdminUrl(): string {
    return this.gateway()?.gatewayAdminUrl || InfiniteClientComponent.defaultGatewayAdminUrl;
  }

  setCode() {
    this.store.setCode(this.code);
  }

  deleteTokens() {
    this.store.deleteTokens();
  }
}
