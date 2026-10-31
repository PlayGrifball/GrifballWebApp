import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiClientService } from '../../api/apiClient.service';
import {
  GAMERTAG_CLAIM_STATUSES, type GamertagClaimDto, type GamertagClaimKind, type GamertagClaimStatus,
} from '../../api/dtos/gamertagClaimDto';
import { errorText } from '../../shared/errorText';

type Decision = 'approve' | 'reject';

const KIND_LABELS: Record<GamertagClaimKind, string> = {
  RosterMatch: 'Roster match',
  SetGamertag: 'Set gamertag',
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
  imports: [RouterLink, DatePipe],
  templateUrl: './gamertagClaims.component.html',
  styleUrl: './gamertagClaims.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GamertagClaimsComponent {
  private api = inject(ApiClientService);
  private snackBar = inject(MatSnackBar);

  readonly statuses = GAMERTAG_CLAIM_STATUSES;
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
  /** Only a pending claim can be decided. */
  readonly decidable = computed(() => this.status() === 'Pending');
  /** The claim being approved or rejected: its buttons wait for the answer. */
  readonly acting = signal<number | null>(null);

  approve(claim: GamertagClaimDto): void {
    this.decide(claim, 'approve', this.api.approveGamertagClaim(claim.gamertagClaimID));
  }

  reject(claim: GamertagClaimDto): void {
    this.decide(claim, 'reject', this.api.rejectGamertagClaim(claim.gamertagClaimID));
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

/** A refusal's reason: the ProblemDetails' detail or title, a plain-text message, or the fallback. */
export function problemText(error: HttpErrorResponse, fallback: string): string {
  const body = error.error;
  if (body && typeof body === 'object') {
    const problem = body as { detail?: string | null, title?: string | null };
    return problem.detail || problem.title || fallback;
  }
  return errorText(error, fallback);
}
