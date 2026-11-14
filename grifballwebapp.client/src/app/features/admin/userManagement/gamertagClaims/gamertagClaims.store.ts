import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { DuplicateSuggestionDto, GamertagClaimDto, GamertagClaimStatus, XuidLookupStatusDto } from '@api/models';
import { GamertagClaimsService } from '@api/services/gamertag-claims.service';
import { problemText } from '@shared/errorText';
import { StoreInput } from '@shared/storeInput';
import { Observable } from 'rxjs';

type Decision = 'approve' | 'reject';

/** What the snack bar says for each decision. */
const DECISIONS: Record<Decision, { done: string, failed: string }> = {
  approve: { done: 'Approved', failed: "Couldn't approve" },
  reject: { done: 'Rejected', failed: "Couldn't reject" },
};

/** A pair of possible duplicates, as the page keys it. */
export function pairKey(pair: DuplicateSuggestionDto): string {
  return `${pair.sourceUserID}-${pair.targetUserID}`;
}

/**
 * Gamertag claims with a status, deciding them, the background lookup of placeholders' Xbox gamertags, and possible
 * duplicates (found on demand, merged or dismissed). One per page (the component provides it).
 */
@Injectable()
export class GamertagClaimsStore {
  private readonly api = inject(GamertagClaimsService);
  private readonly snackBar = inject(MatSnackBar);

  /** The claims' status shown: the component's tab. */
  readonly status = new StoreInput<GamertagClaimStatus>();

  private readonly claims = rxResource({
    params: () => this.status.value(),
    stream: ({ params }) => this.api.getGamertagClaims({ status: params }),
  });
  /** The claims with the chosen status (none while they load, or if they could not be loaded). */
  readonly rows = computed<GamertagClaimDto[]>(() => this.claims.hasValue() ? this.claims.value() : []);
  readonly failed = computed(() => this.claims.status() === 'error');
  /** The first load of a status (a reload after a decision keeps the list shown). */
  readonly waiting = computed(() => this.claims.status() === 'loading');
  readonly empty = computed(() => this.claims.status() === 'resolved' && this.rows().length === 0);
  /** The claim being approved or rejected: its buttons wait for the answer. */
  private readonly _acting = signal<number | null>(null);
  readonly acting = this._acting.asReadonly();

  /** How the background lookup of placeholders' current Xbox gamertags is going (loaded once; reloaded after a lookup). */
  private readonly xuidStatus = rxResource({ params: () => true, stream: () => this.api.getXuidLookupStatus() });
  readonly xuidStatusValue = computed<XuidLookupStatusDto | null>(() => this.xuidStatus.hasValue() ? this.xuidStatus.value() : null);
  private readonly _lookingUp = signal(false);
  readonly lookingUp = this._lookingUp.asReadonly();

  /** Possible duplicates: computed on demand (it compares every placeholder's gamertag), never merged on their own. */
  private readonly duplicatesShown = signal(false);
  private readonly duplicates = rxResource({
    params: () => this.duplicatesShown() || undefined,
    stream: () => this.api.getDuplicateSuggestions(),
  });
  readonly duplicateRows = computed<DuplicateSuggestionDto[]>(() => this.duplicates.hasValue() ? this.duplicates.value() : []);
  readonly duplicatesLoaded = computed(() => this.duplicates.hasValue());
  readonly duplicatesLoading = computed(() => this.duplicates.isLoading());
  readonly duplicatesFailed = computed(() => this.duplicates.status() === 'error');
  /** The pair being merged or dismissed: its buttons wait for the answer. */
  private readonly _actingPair = signal<string | null>(null);
  readonly actingPair = this._actingPair.asReadonly();

  lookUpXuids(): void {
    this._lookingUp.set(true);
    this.api.lookUpXuids().subscribe({
      next: () => {
        this._lookingUp.set(false);
        this.xuidStatus.reload();
      },
      error: (error: HttpErrorResponse) => {
        this._lookingUp.set(false);
        this.snackBar.open(problemText(error, "Couldn't start the lookup."), 'Close');
      },
    });
  }

  approve(claim: GamertagClaimDto): void {
    this.decide(claim, 'approve', this.api.approveGamertagClaim({ claimID: claim.gamertagClaimID }));
  }

  /** Rejects a claim, with the reason shown to the claimant (null: none). */
  reject(claim: GamertagClaimDto, reason: string | null): void {
    this.decide(claim, 'reject', this.api.rejectGamertagClaim({ claimID: claim.gamertagClaimID, body: { reason } }));
  }

  /** Finds the possible duplicates, again when they are shown already. */
  findDuplicates(): void {
    if (this.duplicatesShown())
      this.duplicates.reload();
    else
      this.duplicatesShown.set(true);
  }

  mergeDuplicate(pair: DuplicateSuggestionDto): void {
    this.decidePair(pair, this.api.mergeDuplicate({ body: { sourceUserID: pair.sourceUserID, targetUserID: pair.targetUserID } }),
      `Merged ${pair.sourceGamertag} into ${pair.targetGamertag}.`, `Couldn't merge ${pair.sourceGamertag}.`);
  }

  dismissDuplicate(pair: DuplicateSuggestionDto): void {
    this.decidePair(pair, this.api.dismissDuplicate({ body: { sourceUserID: pair.sourceUserID, targetUserID: pair.targetUserID } }),
      `Dismissed ${pair.sourceGamertag} and ${pair.targetGamertag}.`, `Couldn't dismiss ${pair.sourceGamertag}.`);
  }

  private decidePair(pair: DuplicateSuggestionDto, request: Observable<void>, done: string, failed: string): void {
    this._actingPair.set(pairKey(pair));
    request.subscribe({
      next: () => {
        this._actingPair.set(null);
        this.snackBar.open(done, 'Close');
        this.duplicates.reload();
      },
      error: (error: HttpErrorResponse) => {
        this._actingPair.set(null);
        this.snackBar.open(problemText(error, failed), 'Close');
      },
    });
  }

  private decide(claim: GamertagClaimDto, decision: Decision, request: Observable<void>): void {
    this._acting.set(claim.gamertagClaimID);
    const words = DECISIONS[decision];
    request.subscribe({
      next: () => {
        this._acting.set(null);
        this.snackBar.open(`${words.done} the claim to ${claim.gamertag}.`, 'Close');
        this.claims.reload();
      },
      error: (error: HttpErrorResponse) => {
        this._acting.set(null);
        this.snackBar.open(problemText(error, `${words.failed} the claim to ${claim.gamertag}.`), 'Close');
        // Someone else decided it first: show the list as it is now
        if (error.status === 409)
          this.claims.reload();
      },
    });
  }
}
