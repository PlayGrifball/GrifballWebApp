import { DOCUMENT, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccessTokensService } from '@api/services/access-tokens.service';
import type { AccessTokenDto } from '@api/models';
import { problemText } from '@shared/errorText';

/** How long a new token may last, in days: the server allows 1 to 365. */
export const TOKEN_LIFETIMES = [30, 90, 365] as const;

/**
 * The signed-in user's personal access tokens, for AI agents (Claude Code, say) to use the MCP server as them. A new token
 * is shown once, with the command that adds the server to Claude Code; the list shows names and dates only.
 */
@Component({
  selector: 'app-access-tokens',
  imports: [DatePipe],
  templateUrl: './accessTokens.component.html',
  styleUrl: './accessTokens.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccessTokensComponent {
  private api = inject(AccessTokensService);
  private snackBar = inject(MatSnackBar);
  private document = inject(DOCUMENT);

  readonly lifetimes = TOKEN_LIFETIMES;
  readonly name = signal('');
  readonly days = signal<number>(90);
  readonly creating = signal(false);
  /** The token just made, shown this once. */
  readonly created = signal<string | null>(null);

  /** The MCP server's address on this site. */
  readonly endpoint = computed(() => `${this.document.location.origin}/api/mcp`);
  readonly command = computed(() => `claude mcp add --transport http grifball ${this.endpoint()} --header "Authorization: Bearer ${this.created()}"`);

  readonly tokens = rxResource({
    params: () => true,
    stream: () => this.api.getAccessTokens(),
  });
  readonly rows = computed<AccessTokenDto[]>(() => this.tokens.hasValue() ? this.tokens.value() : []);
  readonly failed = computed(() => this.tokens.status() === 'error');

  isExpired(token: AccessTokenDto): boolean {
    return new Date(token.expiresAt).getTime() <= Date.now();
  }

  create(): void {
    const name = this.name().trim();
    if (!name || this.creating())
      return;
    this.creating.set(true);
    this.api.createAccessToken({ body: { name, days: this.days() } }).subscribe({
      next: made => {
        this.creating.set(false);
        this.name.set('');
        this.created.set(made.token);
        this.tokens.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.creating.set(false);
        this.snackBar.open(problemText(error, "Couldn't make the token."), 'Close');
      },
    });
  }

  revoke(token: AccessTokenDto): void {
    this.api.revokeAccessToken({ id: token.id }).subscribe({
      next: () => {
        this.snackBar.open(`Revoked ${token.name}.`, 'Close');
        this.tokens.reload();
      },
      error: (error: HttpErrorResponse) => this.snackBar.open(problemText(error, "Couldn't revoke the token."), 'Close'),
    });
  }
}
