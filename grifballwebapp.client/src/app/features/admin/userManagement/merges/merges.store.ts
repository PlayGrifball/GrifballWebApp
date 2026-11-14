import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { UserMergeDto } from '@api/models';
import { UserMergesService } from '@api/services/user-merges.service';
import { problemText } from '@shared/errorText';

export const PAGE_SIZE = 25;

/** The merges' audit trail, a page at a time, and undoing a merge. One per page (the component provides it). */
@Injectable()
export class MergesStore {
  private readonly api = inject(UserMergesService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly _page = signal(1);
  readonly page = this._page.asReadonly();
  private readonly merges = rxResource({
    params: () => this.page(),
    stream: ({ params }) => this.api.getUserMerges({ pageNumber: params, pageSize: PAGE_SIZE }),
  });

  readonly rows = computed<UserMergeDto[]>(() => this.merges.hasValue() ? this.merges.value().results : []);
  readonly total = computed(() => this.merges.hasValue() ? this.merges.value().totalCount : 0);
  readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));
  readonly loading = computed(() => this.merges.isLoading());
  readonly failed = computed(() => this.merges.status() === 'error');
  readonly empty = computed(() => this.merges.status() === 'resolved' && this.rows().length === 0);

  /** The merge being undone: its button waits for the answer. */
  private readonly _undoing = signal<number | null>(null);
  readonly undoing = this._undoing.asReadonly();
  /** Why the server refused to undo a merge, by merge. */
  private readonly _refusals = signal<Record<number, string>>({});
  readonly refusals = this._refusals.asReadonly();

  previous(): void {
    this._page.update(page => Math.max(1, page - 1));
  }

  next(): void {
    this._page.update(page => Math.min(this.pages(), page + 1));
  }

  /** Undoes a merge, says how it went, and loads the page again once it is undone. */
  undo(merge: UserMergeDto): void {
    this._undoing.set(merge.userMergeID);
    this.api.undoUserMerge({ mergeID: merge.userMergeID, body: { note: null } }).subscribe({
      next: () => {
        this._undoing.set(null);
        this._refusals.update(refusals => ({ ...refusals, [merge.userMergeID]: '' }));
        this.snackBar.open(`Undid the merge of ${merge.sourceUserName ?? 'user ' + merge.sourceUserID}.`, 'Close');
        this.merges.reload();
      },
      error: (error: HttpErrorResponse) => {
        this._undoing.set(null);
        const reason = problemText(error, "Couldn't undo the merge.");
        this._refusals.update(refusals => ({ ...refusals, [merge.userMergeID]: reason }));
        this.snackBar.open(reason, 'Close');
      },
    });
  }
}
