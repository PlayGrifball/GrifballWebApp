import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DateTime } from 'luxon';
import { ScheduledMatchDto } from '../scheduleList/scheduledMatchDto';
import { ToDatePipe } from '@shared/toDate.pipe';
import { SeasonThisWeekStore } from './seasonThisWeek.store';

export type WeekMatchStatus = 'final' | 'upcoming' | 'awaiting';

export interface WeekMatch {
  match: ScheduledMatchDto;
  status: WeekMatchStatus;
}

/**
 * The matches to show under "This week": those in the current week; between weeks, the next
 * three to be played; once the season is over, the last three played.
 */
export function pickThisWeek(matches: ScheduledMatchDto[], now: DateTime): WeekMatch[] {
  const byTime = [...matches].sort((a, b) => a.time.toMillis() - b.time.toMillis());
  let picked = byTime.filter(m => m.time.localWeekYear === now.localWeekYear && m.time.localWeekNumber === now.localWeekNumber);
  if (picked.length === 0)
    picked = byTime.filter(m => !m.complete && m.time >= now).slice(0, 3);
  if (picked.length === 0)
    picked = byTime.slice(-3);
  return picked.map(match => ({
    match,
    status: weekStatus(match, now),
  }));
}

/** A match's state on its card: played, still to come, or past its time with no result yet. */
function weekStatus(match: ScheduledMatchDto, now: DateTime): WeekMatchStatus {
  if (match.complete)
    return 'final';
  return match.time > now ? 'upcoming' : 'awaiting';
}

/** A few cards for the season overview: what is being played this week. */
@Component({
  selector: 'app-season-this-week',
  imports: [RouterLink, DatePipe, ToDatePipe],
  template: `
    @for (item of thisWeek(); track item.match.seasonMatchID) {
      <a class="week-card" [class.week-card--final]="item.status === 'final'" [routerLink]="['/seasonmatch', item.match.seasonMatchID]">
        @switch (item.status) {
          @case ('final') { <span class="hud-label">Final · {{item.match.time | toDate | date:'EEE MMM d'}}</span> }
          @case ('upcoming') { <span class="hud-label hud-label--accent">{{item.match.time | toDate | date:'EEE h:mm a'}} · Upcoming</span> }
          @default { <span class="hud-label hud-label--gold">{{item.match.time | toDate | date:'EEE h:mm a'}} · Awaiting result</span> }
        }
        <span class="hud-vs"><span>{{item.match.homeTeam}}</span><span class="hud-vs__mid">VS</span><span>{{item.match.awayTeam}}</span></span>
      </a>
    } @empty {
      <p class="hud-empty">No matches scheduled yet.</p>
    }
    <a class="hud-more" [routerLink]="['/season', seasonID()]" [queryParams]="{ tab: 'schedule' }">Full schedule &gt;</a>
  `,
  styles: `
    :host { display: flex; flex-direction: column; gap: 12px; }
    .week-card {
      padding: 16px 18px;
      background: var(--hud-panel-soft);
      border: 1px solid var(--hud-border);
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .week-card:hover { border-color: var(--hud-accent); }
    .week-card--final { background: var(--hud-well); border-color: var(--hud-hairline); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [SeasonThisWeekStore],
})
export class SeasonThisWeekComponent {
  private readonly store = inject(SeasonThisWeekStore);

  readonly seasonID = input.required<number>();
  /** When the page was opened. */
  readonly now = signal<DateTime>(DateTime.now());
  /** The season's scheduled matches (none for 0, or when they can't be read). */
  readonly matches = this.store.matches;
  readonly thisWeek = computed(() => pickThisWeek(this.matches(), this.now()));

  constructor() {
    this.store.seasonID.use(this.seasonID);
  }
}
