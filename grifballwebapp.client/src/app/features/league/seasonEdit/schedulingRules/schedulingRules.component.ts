import { ChangeDetectionStrategy, Component, effect, inject, input, linkedSignal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import type { SeasonSchedulingRulesDto } from '@api/models';
import { SchedulingRulesStore } from './schedulingRules.store';

/**
 * A season's scheduling rules, which its matches' times are checked against (the season scheduler, a time set by hand, a
 * reschedule): how many of a team's players make one of its timeslots the team's, how many matches a team plays a night and a
 * week, and how many may run at once.
 */
@Component({
  selector: 'app-scheduling-rules',
  imports: [FormsModule, MatButtonModule, MatInputModule],
  templateUrl: './schedulingRules.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [SchedulingRulesStore],
})
export class SchedulingRulesComponent {
  private readonly store = inject(SchedulingRulesStore);

  readonly seasonID = input.required<number>();

  readonly saving = this.store.saving;
  readonly saved = this.store.saved;
  readonly error = this.store.error;
  /** The rules being edited: the season's once they arrive. */
  readonly model = linkedSignal(() => this.store.rules());

  constructor() {
    effect(() => {
      const seasonID = this.seasonID();
      untracked(() => this.store.load(seasonID));
    });
  }

  set(field: keyof SeasonSchedulingRulesDto, value: number | null): void {
    this.model.update(rules => rules && { ...rules, [field]: value });
  }

  save(): void {
    const rules = this.model();
    // An empty cap is none
    if (rules) this.store.save(this.seasonID(), { ...rules, maxConcurrentMatches: rules.maxConcurrentMatches ?? null });
  }
}
