import { Component, ChangeDetectionStrategy, computed, inject, input } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { TeamStandingsStore } from './teamStandings.store';
import { MatTableModule } from '@angular/material/table';

@Component({
    selector: 'app-team-standings',
    imports: [
        MatTableModule,
        RouterModule
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

  displayedColumns: string[] = ['rank', 'teamName', 'wins', 'losses'];

  constructor() {
    this.store.seasonID.use(this.seasonID);
  }
}
