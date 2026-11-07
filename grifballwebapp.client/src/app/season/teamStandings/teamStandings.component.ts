import { HttpClient } from '@angular/common/http';
import { Component, ChangeDetectionStrategy, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { TeamStandingDto } from './teamStandingDto';
import { MatTableModule } from '@angular/material/table';
import { whileCurrent } from '../../shared/whileCurrent';

@Component({
    selector: 'app-team-standings',
    imports: [
        MatTableModule,
        RouterModule
    ],
    templateUrl: './teamStandings.component.html',
    styleUrl: './teamStandings.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class TeamStandingsComponent {
  private http = inject(HttpClient);
  private router = inject(Router);

  /** The season page's season (0: none yet). A new one loads its standings in place of the ones shown. */
  readonly seasonID = input.required<number>();

  readonly teamStandings = signal<TeamStandingDto[]>([]);
  /** The standings have loaded (or failed to, or there is no season). */
  readonly loaded = signal(false);
  /** Some records are the source spreadsheet's (a backfilled season without matches). */
  readonly fromSource = computed(() => this.teamStandings().some(t => t.recordFromSource));

  displayedColumns: string[] = ['rank', 'teamName', 'wins', 'losses'];

  constructor() {
    effect(() => {
      const seasonID = this.seasonID();
      untracked(() => this.load(seasonID));
    });
  }

  private load(seasonID: number): void {
    this.teamStandings.set([]);
    if (seasonID === 0) {
      this.loaded.set(true);
      return;
    }
    this.loaded.set(false);

    this.http.get<TeamStandingDto[]>("api/TeamStandings/GetTeamStandings/" + seasonID)
      .pipe(whileCurrent(seasonID, this.seasonID))
      .subscribe(
        {
          next: r => {
            this.teamStandings.set(r);
            this.loaded.set(true);
          },
          error: e => {
            console.log(e);
            this.loaded.set(true);
          }
        });
  }
}
