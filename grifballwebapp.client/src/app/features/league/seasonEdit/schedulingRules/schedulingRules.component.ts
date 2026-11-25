import { ChangeDetectionStrategy, Component, effect, inject, input, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { SkeletonComponent } from '@shared/skeleton/skeleton.component';
import { SchedulingRulesStore } from './schedulingRules.store';

/** A rule of at least 1: required, unless an empty one means none (the cap on matches at once). */
const atLeastOne = (required: boolean) => new FormControl<number | null>(null, required ? [Validators.required, Validators.min(1)] : [Validators.min(1)]);

/**
 * A season's scheduling rules, which its matches' times are checked against (the season scheduler, a time set by hand, a
 * reschedule): how many of a team's players make one of its timeslots the team's, how many matches a team plays a night and a
 * week, and how many may run at once.
 */
@Component({
  selector: 'app-scheduling-rules',
  imports: [ReactiveFormsModule, MatButtonModule, MatInputModule, SkeletonComponent],
  templateUrl: './schedulingRules.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [SchedulingRulesStore],
})
export class SchedulingRulesComponent {
  private readonly store = inject(SchedulingRulesStore);

  readonly seasonID = input.required<number>();

  readonly rules = this.store.rules;
  readonly firstLoad = this.store.firstLoad;
  readonly failed = this.store.failed;
  readonly saving = this.store.saving;
  readonly saved = this.store.saved;
  readonly error = this.store.error;
  readonly form = new FormGroup({
    minPlayersAvailable: atLeastOne(true),
    maxMatchesPerTeamPerNight: atLeastOne(true),
    maxMatchesPerTeamPerWeek: atLeastOne(true),
    maxConcurrentMatches: atLeastOne(false),
  });
  /** The form's status: VALID once every rule (but the cap) is filled in, each at least 1. */
  readonly status = toSignal(this.form.statusChanges, { initialValue: this.form.status });

  constructor() {
    this.store.seasonID.use(this.seasonID);
    // The season's rules, once they arrive, are what is edited
    effect(() => {
      const rules = this.rules();
      if (rules) untracked(() => this.form.reset(rules));
    });
  }

  save(): void {
    if (this.form.invalid) return;
    const rules = this.form.getRawValue();
    this.store.save(this.seasonID(), {
      minPlayersAvailable: rules.minPlayersAvailable!,
      maxMatchesPerTeamPerNight: rules.maxMatchesPerTeamPerNight!,
      maxMatchesPerTeamPerWeek: rules.maxMatchesPerTeamPerWeek!,
      // An empty cap is none
      maxConcurrentMatches: rules.maxConcurrentMatches ?? null,
    });
  }
}
