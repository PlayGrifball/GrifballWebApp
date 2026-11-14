import { Component, ChangeDetectionStrategy, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { tap } from 'rxjs';
import { Router, RouterModule } from '@angular/router';
import { TeamStandingsService } from '@api/services/team-standings.service';
import { TeamStandingDto } from './teamStandingDto';
import { MatTableModule } from '@angular/material/table';

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
  private api = inject(TeamStandingsService);
  private router = inject(Router);

  /** The season page's season (0: none yet). A new one loads its standings in place of the ones shown. */
  readonly seasonID = input.required<number>();

  /** The season's standings, loaded again for each season (none for 0): a new one drops the request still on its way. */
  private readonly standings = rxResource({
    params: () => this.seasonID() || undefined,
    stream: ({ params: seasonID }) => this.api.getTeamStandings({ seasonID }).pipe(tap({ error: e => console.log(e) })),
  });
  readonly teamStandings = computed<TeamStandingDto[]>(() => this.standings.hasValue() ? this.standings.value() : []);
  /** The standings have loaded (or failed to, or there is no season). */
  readonly loaded = computed(() => !this.standings.isLoading());
  readonly fromSource = computed(() => this.teamStandings().some(t => t.recordFromSource));

  displayedColumns: string[] = ['rank', 'teamName', 'wins', 'losses'];
}
