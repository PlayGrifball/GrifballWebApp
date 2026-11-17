import { ChangeDetectionStrategy, Component, effect, inject, input, untracked } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import type { DraftTimeslot } from '@api/models';
import { DraftHelperStore } from './draftHelper.store';

/**
 * The draft helper beside the board: the picks suggested for the team on deck (each with its score and why) and the pool in
 * detail (region, whether they'd captain, the timeslots they signed up for, their career). Reloads as the pool changes.
 */
@Component({
  selector: 'app-draft-helper',
  imports: [DecimalPipe, PercentPipe],
  templateUrl: './draftHelper.component.html',
  styleUrl: './draftHelper.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DraftHelperStore],
})
export class DraftHelperComponent {
  private readonly store = inject(DraftHelperStore);

  readonly seasonID = input.required<number>();
  /** The pool's players (their IDs): a change (a pick made, undone or moved) loads the helper again. */
  readonly poolKey = input('');

  readonly pool = this.store.pool;
  readonly suggestions = this.store.suggestions;

  constructor() {
    effect(() => {
      const seasonID = this.seasonID();
      this.poolKey();
      untracked(() => this.store.load(seasonID));
    });
  }

  /** "Mon 20:00, Tue 21:30": the timeslots in league time, as the season offers them. */
  timeslots(slots: DraftTimeslot[]): string {
    return slots.map(s => `${s.dayOfWeek.slice(0, 3)} ${s.time.slice(0, 5)}`).join(', ');
  }
}
