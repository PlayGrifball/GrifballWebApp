import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AccountService } from '@core/auth/account.service';
import { SeasonTabsComponent } from '../seasonTabs/seasonTabs.component';
import { BackfilledBannerComponent } from '@shared/dataNotice/dataNotice.component';
import { SeasonTeamsStore } from './seasonTeams.store';

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
  providers: [SeasonTeamsStore],
})
export class SeasonTeamsComponent {
  private route = inject(ActivatedRoute);
  private readonly store = inject(SeasonTeamsStore);
  readonly accountService = inject(AccountService);

  readonly seasonID = this.store.seasonID;
  readonly summary = this.store.summary;
  readonly teams = this.store.teams;
  readonly loading = this.store.loading;
  readonly error = this.store.error;
  readonly searchFilter = signal<string>('');

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
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(params => {
      const id = Number(params.get('seasonID'));
      if (id > 0) this.loadData(id);
    });
  }

  /** Loads a season, forgetting the one shown before so none of it is left on the new season's page. */
  loadData(seasonID: number): void {
    this.store.show(seasonID);
  }

  clearSearch(): void {
    this.searchFilter.set('');
  }
}
