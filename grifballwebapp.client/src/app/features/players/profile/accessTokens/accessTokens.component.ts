import { DOCUMENT, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
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
  imports: [DatePipe, MatCheckboxModule, ReactiveFormsModule],
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
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true }),
    days: new FormControl<number>(90, { nonNullable: true }),
    scope: new FormControl<AccessTokenScope>('Mcp', { nonNullable: true }),
    readOnly: new FormControl(false, { nonNullable: true }),
  });
  /** The name field's own value as a signal: the only form value the template reads reactively (the submit button's disabled state). */
  private readonly name = toSignal(this.form.controls.name.valueChanges, { initialValue: this.form.controls.name.value });

  readonly creating = this.store.creating;
  /** The token just made, shown this once. */
  readonly created = this.store.created;
  /** The just-made token's scope, so the Claude Code command only shows when it opens MCP. */
  readonly createdScope = this.store.createdScope;
  readonly showsMcpCommand = computed(() => this.createdScope() !== 'Api');
  readonly canCreate = computed(() => !this.creating() && this.name().trim().length > 0);

  /** The MCP server's address on this site. */
  readonly endpoint = computed(() => `${this.document.location.origin}/api/mcp`);
  readonly command = computed(() => `claude mcp add --transport http grifball ${this.endpoint()} --header "Authorization: Bearer ${this.created()}"`);

  readonly loaded = this.store.loaded;
  readonly rows = this.store.rows;
  readonly failed = this.store.failed;

  isExpired(token: AccessTokenDto): boolean {
    return new Date(token.expiresAt).getTime() <= Date.now();
  }

  /** How the token's scope reads on the page (TOKEN_SCOPES' label), not its raw value. */
  scopeLabel(scope: AccessTokenScope): string {
    return this.scopes.find(s => s.value === scope)?.label ?? scope;
  }

  create(): void {
    const { name, days, scope, readOnly } = this.form.getRawValue();
    const trimmed = name.trim();
    if (trimmed)
      this.store.create(trimmed, days, scope, readOnly, () => this.form.controls.name.setValue(''));
  }

  revoke(token: AccessTokenDto): void {
    this.store.revoke(token);
  }
}
