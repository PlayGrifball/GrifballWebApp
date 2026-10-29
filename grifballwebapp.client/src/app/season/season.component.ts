import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { ScheduleListComponent } from "./scheduleList/scheduleList.component";
import { PlayoffBracketComponent } from './playoffBracket/playoffBracket.component';
import { TeamStandingsComponent } from './teamStandings/teamStandings.component';
import { SeasonTabsComponent } from './seasonTabs/seasonTabs.component';
import { SeasonThisWeekComponent } from './seasonThisWeek/seasonThisWeek.component';

/** The sections shown on the season page itself (the other tabs are their own pages). */
export type SeasonView = 'overview' | 'schedule' | 'bracket';

@Component({
    selector: 'app-season',
    templateUrl: './season.component.html',
    styleUrl: './season.component.scss',
    imports: [
        RouterModule,
        ScheduleListComponent,
        PlayoffBracketComponent,
        TeamStandingsComponent,
        SeasonTabsComponent,
        SeasonThisWeekComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(ApiClientService);

  /** The ?tab= query parameter. */
  readonly tab = input<string | null | undefined>();
  readonly view = computed<SeasonView>(() => {
    const tab = this.tab();
    return tab === 'schedule' || tab === 'bracket' ? tab : 'overview';
  });
  readonly kicker = computed(() => ({ overview: 'Season overview', schedule: 'Regular season', bracket: 'Playoffs' })[this.view()]);

  readonly seasonID = signal(0);
  readonly seasonName = signal<string | null>("Season");

  ngOnInit(): void {
    this.seasonID.set(Number(this.route.snapshot.paramMap.get('seasonID')));

    if (this.seasonID() === 0) {
      this.api.getCurrentSeasonID()
        .subscribe({
          next: (result) => {
            this.seasonID.set(result);
            this.getSeasonName();
          },
        });
    }
    else {
      this.getSeasonName();
    }
  }

  getSeasonName(): void {
    this.api.getSeasonName(this.seasonID())
        .subscribe({
          next: (result) => this.seasonName.set(result),
        });
  }

}
