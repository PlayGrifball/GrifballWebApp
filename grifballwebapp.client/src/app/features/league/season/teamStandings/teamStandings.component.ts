import { Component, ChangeDetectionStrategy, computed, inject, input } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { TeamStandingsStore } from './teamStandings.store';
import { MatTableModule } from '@angular/material/table';
import { SkeletonComponent } from '@shared/skeleton/skeleton.component';

@Component({
    selector: 'app-team-standings',
    imports: [
        MatTableModule,
        RouterModule,
        SkeletonComponent,
    ],
    templateUrl: './teamStandings.component.html',
    styleUrl: './teamStandings.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [TeamStandingsStore],
})
export class TeamStandingsComponent {
  private readonly store = inject(TeamStandingsStore);
  private router = inject(Router);

  /** The season page's season (0: none yet). A new one loads its standings in place of the ones shown. */
  readonly seasonID = input.required<number>();

  readonly teamStandings = this.store.teamStandings;
  /** The standings have loaded (or failed to, or there is no season). */
  readonly loaded = this.store.loaded;
  readonly fromSource = computed(() => this.teamStandings().some(t => t.recordFromSource));
  /** The source spreadsheet gives a team's final place (the playoffs' result): the table has a Finish column. */
  readonly hasFinish = computed(() => this.teamStandings().some(t => t.finish != null));

  readonly displayedColumns = computed(() => ['rank', 'teamName', 'wins', 'losses', ...(this.hasFinish() ? ['finish'] : [])]);

  /** A place as an ordinal: 1st, 2nd, 3rd, 11th, 22nd. */
  static ordinal(place: number): string {
    const tens = place % 100;
    const suffix = tens >= 11 && tens <= 13 ? 'th' : ['th', 'st', 'nd', 'rd'][place % 10] ?? 'th';
    return `${place}${suffix}`;
  }

  readonly ordinal = TeamStandingsComponent.ordinal;

  constructor() {
    this.store.seasonID.use(this.seasonID);
  }
}
