import { DOCUMENT, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { AccessTokenDto } from '@api/models';
import { AccessTokensStore } from './accessTokens.store';

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
  providers: [AccessTokensStore],
})
export class AccessTokensComponent {
  private readonly store = inject(AccessTokensStore);
  private document = inject(DOCUMENT);

  readonly lifetimes = TOKEN_LIFETIMES;
  readonly name = signal('');
  readonly days = signal<number>(90);
  readonly creating = this.store.creating;
  /** The token just made, shown this once. */
  readonly created = this.store.created;

  /** The MCP server's address on this site. */
  readonly endpoint = computed(() => `${this.document.location.origin}/api/mcp`);
  readonly command = computed(() => `claude mcp add --transport http grifball ${this.endpoint()} --header "Authorization: Bearer ${this.created()}"`);

  readonly loaded = this.store.loaded;
  readonly rows = this.store.rows;
  readonly failed = this.store.failed;

  isExpired(token: AccessTokenDto): boolean {
    return new Date(token.expiresAt).getTime() <= Date.now();
  }

  create(): void {
    const name = this.name().trim();
    if (name)
      this.store.create(name, this.days(), () => this.name.set(''));
  }

  revoke(token: AccessTokenDto): void {
    this.store.revoke(token);
  }
}
