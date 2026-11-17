import { ChangeDetectionStrategy, Component, computed, effect, inject, input, linkedSignal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import type { TeamSlot } from '@api/models';
import { TeamAvailabilityStore } from './teamAvailability.store';

/**
 * A team's timeslots, the ones scheduling checks its matches against: each of its season's, whether it's the team's and how
 * many of its roster can play it. Worked out from the roster's signups, unless its captain set the team's own: the captain
 * (and staff: the server says who, canChange) tick them here, or reset them to the roster's.
 */
@Component({
  selector: 'app-team-availability',
  imports: [MatButtonModule, MatCheckboxModule],
  templateUrl: './teamAvailability.component.html',
  styleUrl: './teamAvailability.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [TeamAvailabilityStore],
})
export class TeamAvailabilityComponent {
  private readonly store = inject(TeamAvailabilityStore);

  readonly teamID = input.required<number>();
  readonly slots = this.store.slots;
  /** The viewer captains the team, or is staff: they may change its timeslots (the server says). */
  readonly canEdit = computed(() => this.slots()?.canChange ?? false);
  readonly saving = this.store.saving;
  readonly error = this.store.error;
  /** The timeslots ticked: the team's, until the captain changes them. */
  readonly ticked = linkedSignal(() => new Set((this.slots()?.slots ?? []).filter(s => s.available).map(s => s.availabilityOptionID)));

  constructor() {
    effect(() => {
      const teamID = this.teamID();
      untracked(() => this.store.load(teamID));
    });
  }

  /** "Monday 20:00": a timeslot in league time, as the season's are offered. */
  label(slot: TeamSlot): string {
    return `${slot.dayOfWeek} ${slot.time.slice(0, 5)}`;
  }

  toggle(slot: TeamSlot, checked: boolean): void {
    const next = new Set(this.ticked());
    if (checked) next.add(slot.availabilityOptionID);
    else next.delete(slot.availabilityOptionID);
    this.ticked.set(next);
  }

  save(): void {
    this.store.save(this.teamID(), [...this.ticked()].sort((a, b) => a - b));
  }

  reset(): void {
    this.store.reset(this.teamID());
  }
}
