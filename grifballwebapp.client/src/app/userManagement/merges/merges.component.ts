import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouterLink } from '@angular/router';
import { UserMergesService } from '@api/services/user-merges.service';
import type { UserMergeDto } from '@api/models';
import { problemText } from '../../shared/errorText';

const PAGE_SIZE = 25;

/** How each trigger reads in the list. */
const TRIGGER_LABELS: Record<string, string> = {
  Manual: 'Manual merge',
  ClaimApproval: 'Claim approved',
  SetGamertag: 'Set gamertag',
  XuidLookup: 'Xbox lookup claim',
  UserClaim: 'User claim',
  XboxPlaceholder: 'Xbox placeholder',
  DiscordMatch: 'Discord match',
  Import: 'Import',
  DuplicateSuggestion: 'Possible duplicate',
};

/**
 * The merges' audit trail (sysadmins): every merge, newest first, whatever made it (the merge page, a claim, a gamertag,
 * the import), and undoing one. An undo the server refuses says why on its row.
 */
@Component({
  selector: 'app-merges',
  imports: [RouterLink, DatePipe],
  templateUrl: './merges.component.html',
  styleUrl: './merges.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MergesComponent {
  private api = inject(UserMergesService);
  private snackBar = inject(MatSnackBar);

  readonly page = signal(1);
  readonly merges = rxResource({
    params: () => this.page(),
    stream: ({ params }) => this.api.getUserMerges({ pageNumber: params, pageSize: PAGE_SIZE }),
  });

  readonly rows = computed<UserMergeDto[]>(() => this.merges.hasValue() ? this.merges.value().results : []);
  readonly total = computed(() => this.merges.hasValue() ? this.merges.value().totalCount : 0);
  readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));
  readonly failed = computed(() => this.merges.status() === 'error');
  readonly empty = computed(() => this.merges.status() === 'resolved' && this.rows().length === 0);

  /** The merge being undone: its button waits for the answer. */
  readonly undoing = signal<number | null>(null);
  /** Why the server refused to undo a merge, by merge. */
  readonly refusals = signal<Record<number, string>>({});

  triggerLabel(trigger: string): string {
    return TRIGGER_LABELS[trigger] ?? trigger;
  }

  previous(): void {
    this.page.update(page => Math.max(1, page - 1));
  }

  next(): void {
    this.page.update(page => Math.min(this.pages(), page + 1));
  }

  undo(merge: UserMergeDto): void {
    this.undoing.set(merge.userMergeID);
    this.api.undoUserMerge({ mergeID: merge.userMergeID, body: { note: null } }).subscribe({
      next: () => {
        this.undoing.set(null);
        this.refusals.update(refusals => ({ ...refusals, [merge.userMergeID]: '' }));
        this.snackBar.open(`Undid the merge of ${merge.sourceUserName ?? 'user ' + merge.sourceUserID}.`, 'Close');
        this.merges.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.undoing.set(null);
        const reason = problemText(error, "Couldn't undo the merge.");
        this.refusals.update(refusals => ({ ...refusals, [merge.userMergeID]: reason }));
        this.snackBar.open(reason, 'Close');
      },
    });
  }
}
