import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { UserMergeDto } from '@api/models';
import { MergesStore } from './merges.store';

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
  providers: [MergesStore],
})
export class MergesComponent {
  private readonly store = inject(MergesStore);

  readonly page = this.store.page;
  readonly rows = this.store.rows;
  readonly total = this.store.total;
  readonly pages = this.store.pages;
  readonly loading = this.store.loading;
  readonly failed = this.store.failed;
  readonly empty = this.store.empty;

  /** The merge being undone: its button waits for the answer. */
  readonly undoing = this.store.undoing;
  /** Why the server refused to undo a merge, by merge. */
  readonly refusals = this.store.refusals;

  triggerLabel(trigger: string): string {
    return TRIGGER_LABELS[trigger] ?? trigger;
  }

  previous(): void {
    this.store.previous();
  }

  next(): void {
    this.store.next();
  }

  undo(merge: UserMergeDto): void {
    this.store.undo(merge);
  }
}
