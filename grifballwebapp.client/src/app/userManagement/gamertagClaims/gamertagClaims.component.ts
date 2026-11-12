import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../api/apiClient.service';
import { GAMERTAG_CLAIM_STATUS } from '@api/models/gamertag-claim-status-array';
import type { DuplicateSuggestionDto, GamertagClaimDto, GamertagClaimStatus } from '@api/models';
import type { GamertagClaimKind } from '../../api/gamertagClaims';
import { problemText } from '../../shared/errorText';
import { SeasonLinksComponent } from '../../shared/seasonLinks/seasonLinks.component';

// Re-exported for this page's own spec, which imports it from here (kept, to not churn an unrelated import).
export { problemText };

type Decision = 'approve' | 'reject';

const KIND_LABELS: Record<GamertagClaimKind, string> = {
  RosterMatch: 'Roster match',
  SetGamertag: 'Set gamertag',
  XuidLookup: 'Xbox lookup',
  UserClaim: 'User claim',
  DiscordMatch: 'Discord match',
};

/** What the snack bar says for each decision. */
const DECISIONS: Record<Decision, { done: string, failed: string }> = {
  approve: { done: 'Approved', failed: "Couldn't approve" },
  reject: { done: 'Rejected', failed: "Couldn't reject" },
};

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
})
export class GamertagClaimsComponent {
  private api = inject(ApiClientService);
  private snackBar = inject(MatSnackBar);

  readonly statuses = GAMERTAG_CLAIM_STATUS;
  readonly kindLabels = KIND_LABELS;
  readonly status = signal<GamertagClaimStatus>('Pending');

  readonly claims = rxResource({
    params: () => this.status(),
    stream: ({ params }) => this.api.getGamertagClaims(params),
  });

  /** The claims with the chosen status (none while they load, or if they could not be loaded). */
  readonly rows = computed<GamertagClaimDto[]>(() => this.claims.hasValue() ? this.claims.value() : []);
  readonly failed = computed(() => this.claims.status() === 'error');
  /** The first load of a status: laid out but hidden (a reload after a decision keeps the list shown). */
  readonly waiting = computed(() => this.claims.status() === 'loading');
  readonly empty = computed(() => this.claims.status() === 'resolved' && this.rows().length === 0);
  readonly emptyText = computed(() => `No ${this.status().toLowerCase()} claims.`);
  /** Only a pending claim can be decided, and never an informational Xbox lookup note. */
  readonly decidable = computed(() => this.status() === 'Pending');
  canDecide(claim: GamertagClaimDto): boolean {
    // An XUID lookup is decided like any claim: approving links the placeholder to the XUID or merges it into its user
    return this.decidable() && claim.status === 'Pending';
  }
  /** The claim being approved or rejected: its buttons wait for the answer. */
  readonly acting = signal<number | null>(null);

  /** How the background lookup of placeholders' current Xbox gamertags is going (loaded once; reloaded after a lookup). */
  readonly xuidStatus = rxResource({
    params: () => true,
    stream: () => this.api.getXuidLookupStatus(),
  });
  readonly lookingUp = signal(false);

  lookUpXuids(): void {
    this.lookingUp.set(true);
    this.api.lookUpXuids().subscribe({
      next: () => {
        this.lookingUp.set(false);
        this.xuidStatus.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.lookingUp.set(false);
        this.snackBar.open(problemText(error, "Couldn't start the lookup."), 'Close');
      },
    });
  }

  approve(claim: GamertagClaimDto): void {
    this.decide(claim, 'approve', this.api.approveGamertagClaim(claim.gamertagClaimID));
  }

  /** Each pending claim's rejection reason as typed (shown to the claimant); empty sends none. */
  readonly reasons = signal<Record<number, string>>({});
  setReason(claim: GamertagClaimDto, reason: string): void {
    this.reasons.update(reasons => ({ ...reasons, [claim.gamertagClaimID]: reason }));
  }

  reject(claim: GamertagClaimDto): void {
    const reason = this.reasons()[claim.gamertagClaimID]?.trim() || null;
    this.decide(claim, 'reject', this.api.rejectGamertagClaim(claim.gamertagClaimID, reason));
  }

  /** The claim is about a user (their own claim, or a Discord match) rather than an Xbox account. */
  aboutUser(claim: GamertagClaimDto): boolean {
    return claim.kind === 'UserClaim' || claim.kind === 'DiscordMatch';
  }

  /** Possible duplicates: computed on demand (it compares every placeholder's gamertag), never merged on their own. */
  readonly duplicatesShown = signal(false);
  readonly duplicates = rxResource({
    params: () => this.duplicatesShown() || undefined,
    stream: () => this.api.getDuplicateSuggestions(),
  });
  readonly duplicateRows = computed<DuplicateSuggestionDto[]>(() => this.duplicates.hasValue() ? this.duplicates.value() : []);
  readonly duplicatesFailed = computed(() => this.duplicates.status() === 'error');
  /** The pair being merged or dismissed: its buttons wait for the answer. */
  readonly actingPair = signal<string | null>(null);
  pairKey(pair: DuplicateSuggestionDto): string {
    return `${pair.sourceUserID}-${pair.targetUserID}`;
  }
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
    if (this.duplicatesShown())
      this.duplicates.reload();
    else
      this.duplicatesShown.set(true);
  }

  mergeDuplicate(pair: DuplicateSuggestionDto): void {
    this.decidePair(pair, this.api.mergeDuplicate(pair.sourceUserID, pair.targetUserID),
      `Merged ${pair.sourceGamertag} into ${pair.targetGamertag}.`, `Couldn't merge ${pair.sourceGamertag}.`);
  }

  dismissDuplicate(pair: DuplicateSuggestionDto): void {
    this.decidePair(pair, this.api.dismissDuplicate(pair.sourceUserID, pair.targetUserID),
      `Dismissed ${pair.sourceGamertag} and ${pair.targetGamertag}.`, `Couldn't dismiss ${pair.sourceGamertag}.`);
  }

  private decidePair(pair: DuplicateSuggestionDto, request: Observable<void>, done: string, failed: string): void {
    this.actingPair.set(this.pairKey(pair));
    request.subscribe({
      next: () => {
        this.actingPair.set(null);
        this.snackBar.open(done, 'Close');
        this.duplicates.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.actingPair.set(null);
        this.snackBar.open(problemText(error, failed), 'Close');
      },
    });
  }

  private decide(claim: GamertagClaimDto, decision: Decision, request: Observable<void>): void {
    this.acting.set(claim.gamertagClaimID);
    const words = DECISIONS[decision];
    request.subscribe({
      next: () => {
        this.acting.set(null);
        this.snackBar.open(`${words.done} the claim to ${claim.gamertag}.`, 'Close');
        this.claims.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.acting.set(null);
        this.snackBar.open(problemText(error, `${words.failed} the claim to ${claim.gamertag}.`), 'Close');
        // Someone else decided it first: show the list as it is now
        if (error.status === 409)
          this.claims.reload();
      },
    });
  }
}
