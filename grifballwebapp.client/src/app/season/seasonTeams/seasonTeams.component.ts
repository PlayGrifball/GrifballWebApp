import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { catchError, EMPTY, merge, Subject, switchMap, tap } from 'rxjs';
import { ApiClientService } from '../../api/apiClient.service';
import { AccountService } from '../../account.service';
import { SeasonSummaryService } from '../seasonSummary';
import { SeasonTabsComponent } from '../seasonTabs/seasonTabs.component';
import { BackfilledBannerComponent } from '../../shared/dataNotice/dataNotice.component';
import type { SeasonSummaryDto } from '../../api/dtos/seasonSummaryDto';
import type { SeasonTeamDto } from '../../api/dtos/seasonTeamDto';

@Component({
  selector: 'app-season-teams',
  templateUrl: './seasonTeams.component.html',
  styleUrl: './seasonTeams.component.scss',
  imports: [
    RouterLink,
    FormsModule,
    SeasonTabsComponent,
    BackfilledBannerComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonTeamsComponent {
  private route = inject(ActivatedRoute);
  private api = inject(ApiClientService);
  private summaries = inject(SeasonSummaryService);
  readonly accountService = inject(AccountService);

  readonly seasonID = signal<number>(0);
  readonly summary = signal<SeasonSummaryDto | null>(null);
  readonly teams = signal<SeasonTeamDto[]>([]);
  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);
  readonly searchFilter = signal<string>('');

  /** The season to load; switchMap drops the previous season's responses when another one is asked for. */
  private readonly load = new Subject<number>();

  readonly isHistorical = computed(() => this.summary()?.isHistorical ?? false);

  readonly isCaptainOfAnyTeam = computed(() => {
    const personID = this.accountService.personID();
    if (!personID) return false;
    return this.teams().some(team => team.players.some(p => p.userID === personID && p.isCaptain));
  });

  /** Whether the user should see the shortcut button to open the draft board. */
  readonly canAccessDraft = computed(() => {
    if (this.accountService.isEventOrganizer()) return true;
    if (this.isCaptainOfAnyTeam()) return true;
    const s = this.summary();
    return s != null && !s.isHistorical && s.signupCount > 0;
  });

  readonly filteredTeams = computed(() => {
    const query = this.searchFilter().trim().toLowerCase();
    const all = this.teams();
    if (!query) return all;
    return all.filter(team =>
      team.teamName.toLowerCase().includes(query) ||
      team.players.some(p => p.gamertag.toLowerCase().includes(query))
    );
  });

  readonly totalTeamsCount = computed(() => this.teams().length);
  readonly totalPlayersCount = computed(() =>
    this.teams().reduce((sum, t) => sum + t.players.length, 0)
  );

  readonly filteredPlayerCount = computed(() =>
    this.filteredTeams().reduce((sum, t) => sum + t.players.length, 0)
  );

  constructor() {
    this.load.pipe(
      switchMap(seasonID => merge(
        // The summary only adds the banner and the draft shortcut: the rosters show without it
        this.summaries.get(seasonID).pipe(
          tap(s => this.summary.set(s)),
          catchError(() => EMPTY),
        ),
        this.api.getSeasonTeams(seasonID).pipe(
          tap(teams => {
            this.teams.set(teams);
            this.loading.set(false);
          }),
          catchError((err: HttpErrorResponse) => {
            this.error.set(err.status === 404 ? 'Season not found' : 'Failed to load season teams');
            this.loading.set(false);
            return EMPTY;
          }),
        ),
      )),
      takeUntilDestroyed(),
    ).subscribe();

    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(params => {
      const id = Number(params.get('seasonID'));
      if (id > 0) this.loadData(id);
    });
  }

  /** Loads a season, forgetting the one shown before so none of it is left on the new season's page. */
  loadData(seasonID: number): void {
    this.seasonID.set(seasonID);
    this.summary.set(null);
    this.teams.set([]);
    this.loading.set(true);
    this.error.set(null);
    this.load.next(seasonID);
  }

  clearSearch(): void {
    this.searchFilter.set('');
  }
}
