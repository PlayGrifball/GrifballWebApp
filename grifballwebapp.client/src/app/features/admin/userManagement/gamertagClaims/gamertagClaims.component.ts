import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GAMERTAG_CLAIM_STATUS } from '@api/models/gamertag-claim-status-array';
import type { DuplicateSuggestionDto, GamertagClaimDto, GamertagClaimStatus } from '@api/models';
import type { GamertagClaimKind } from '../../api/gamertagClaims';
import { problemText } from '@shared/errorText';
import { SeasonLinksComponent } from '@shared/seasonLinks/seasonLinks.component';
import { GamertagClaimsStore, pairKey } from './gamertagClaims.store';

// Re-exported for this page's own spec, which imports it from here (kept, to not churn an unrelated import).
export { problemText };

// Every GamertagClaimKind labelled; looked up by the API's kind (a string)
const KIND_LABELS: Readonly<Record<string, string>> = {
  RosterMatch: 'Roster match',
  SetGamertag: 'Set gamertag',
  XuidLookup: 'Xbox lookup',
  UserClaim: 'User claim',
  DiscordMatch: 'Discord match',
} satisfies Record<GamertagClaimKind, string>;

/**
 * Gamertag claims (sysadmins): a historical player from the archive claimed as a user's Xbox account, by a roster
 * name matching it or the user setting their gamertag to it. Pending ones are approved or rejected here.
 */
@Component({
  selector: 'app-gamertag-claims',
  imports: [RouterLink, DatePipe, SeasonLinksComponent],
  templateUrl: './gamertagClaims.component.html',
  styleUrl: './gamertagClaims.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [GamertagClaimsStore],
})
export class GamertagClaimsComponent {
  private readonly store = inject(GamertagClaimsStore);

  readonly statuses = GAMERTAG_CLAIM_STATUS;
  readonly kindLabels = KIND_LABELS;
  readonly status = signal<GamertagClaimStatus>('Pending');

  /** The claims with the chosen status (none while they load, or if they could not be loaded). */
  readonly rows = this.store.rows;
  readonly failed = this.store.failed;
  /** The first load of a status: laid out but hidden (a reload after a decision keeps the list shown). */
  readonly waiting = this.store.waiting;
  readonly empty = this.store.empty;
  readonly emptyText = computed(() => `No ${this.status().toLowerCase()} claims.`);
  /** Only a pending claim can be decided, and never an informational Xbox lookup note. */
  readonly decidable = computed(() => this.status() === 'Pending');
  canDecide(claim: GamertagClaimDto): boolean {
    // An XUID lookup is decided like any claim: approving links the placeholder to the XUID or merges it into its user
    return this.decidable() && claim.status === 'Pending';
  }
  /** The claim being approved or rejected: its buttons wait for the answer. */
  readonly acting = this.store.acting;

  /** How the background lookup of placeholders' current Xbox gamertags is going (loaded once; reloaded after a lookup). */
  readonly xuidStatus = this.store.xuidStatusValue;
  readonly lookingUp = this.store.lookingUp;

  constructor() {
    this.store.status.use(this.status);
  }

  lookUpXuids(): void {
    this.store.lookUpXuids();
  }

  approve(claim: GamertagClaimDto): void {
    this.store.approve(claim);
  }

  /** Each pending claim's rejection reason as typed (shown to the claimant); empty sends none. */
  readonly reasons = signal<Record<number, string>>({});
  setReason(claim: GamertagClaimDto, reason: string): void {
    this.reasons.update(reasons => ({ ...reasons, [claim.gamertagClaimID]: reason }));
  }

  reject(claim: GamertagClaimDto): void {
    const reason = this.reasons()[claim.gamertagClaimID]?.trim() || null;
    this.store.reject(claim, reason);
  }

  /** The claim is about a user (their own claim, or a Discord match) rather than an Xbox account. */
  aboutUser(claim: GamertagClaimDto): boolean {
    return claim.kind === 'UserClaim' || claim.kind === 'DiscordMatch';
  }

  /** Possible duplicates: computed on demand (it compares every placeholder's gamertag), never merged on their own. */
  readonly duplicateRows = this.store.duplicateRows;
  readonly duplicatesLoaded = this.store.duplicatesLoaded;
  readonly duplicatesLoading = this.store.duplicatesLoading;
  readonly duplicatesFailed = this.store.duplicatesFailed;
  /** The pair being merged or dismissed: its buttons wait for the answer. */
  readonly actingPair = this.store.actingPair;
  readonly pairKey = pairKey;
  /** Why the pair was suggested: only an edit match is a number of letters apart. */
  matchNote(pair: DuplicateSuggestionDto): string {
    switch (pair.match) {
      case 'Framing': return 'same name inside decoration';
      case 'Token': return 'name is a word of the other';
      case 'Edits': return `${pair.distance} letter${pair.distance === 1 ? '' : 's'} apart`;
      default: return 'same gamertag';
    }
  }

  findDuplicates(): void {
    this.store.findDuplicates();
  }

  mergeDuplicate(pair: DuplicateSuggestionDto): void {
    this.store.mergeDuplicate(pair);
  }

  dismissDuplicate(pair: DuplicateSuggestionDto): void {
    this.store.dismissDuplicate(pair);
  }
}
