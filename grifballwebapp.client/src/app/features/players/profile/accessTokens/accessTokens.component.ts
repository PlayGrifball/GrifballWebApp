import { DOCUMENT, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import type { AccessTokenDto, AccessTokenScope } from '@api/models';
import { AccessTokensStore } from './accessTokens.store';

/** How long a new token may last, in days: the server allows 1 to 365. */
export const TOKEN_LIFETIMES = [30, 90, 365] as const;

/** A scope to offer when making a token, with how it reads on the page. */
export const TOKEN_SCOPES: ReadonlyArray<{ value: AccessTokenScope; label: string }> = [
  { value: 'Mcp', label: 'MCP server (AI agents)' },
  { value: 'Api', label: 'REST API' },
  { value: 'Both', label: 'Both' },
];

/**
 * The signed-in user's personal access tokens, for AI agents (Claude Code, say) over the MCP server, or a script over the
 * REST API (docs: api-tokens.md). A new token is shown once, with the command that adds it to Claude Code when its scope
 * allows MCP; the list shows each one's name, dates, scope and whether it's read-only, never the token itself.
 */
@Component({
  selector: 'app-access-tokens',
  imports: [DatePipe, MatCheckboxModule],
  templateUrl: './accessTokens.component.html',
  styleUrl: './accessTokens.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [AccessTokensStore],
})
export class AccessTokensComponent {
  private readonly store = inject(AccessTokensStore);
  private document = inject(DOCUMENT);

  readonly lifetimes = TOKEN_LIFETIMES;
  readonly scopes = TOKEN_SCOPES;
  readonly name = signal('');
  readonly days = signal<number>(90);
  readonly scope = signal<AccessTokenScope>('Mcp');
  readonly readOnly = signal(false);
  readonly creating = this.store.creating;
  /** The token just made, shown this once. */
  readonly created = this.store.created;
  /** The just-made token's scope, so the Claude Code command only shows when it opens MCP. */
  readonly createdScope = this.store.createdScope;
  readonly showsMcpCommand = computed(() => this.createdScope() !== 'Api');

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
      this.store.create(name, this.days(), this.scope(), this.readOnly(), () => this.name.set(''));
  }

  revoke(token: AccessTokenDto): void {
    this.store.revoke(token);
  }
}
