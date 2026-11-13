import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, numberAttribute, signal, untracked } from '@angular/core';
import { RouterModule } from '@angular/router';
import { HomeService } from '@api/services/home.service';
import { SeasonService } from '@api/services/season.service';
import { ScheduleListComponent } from "./scheduleList/scheduleList.component";
import { PlayoffBracketComponent } from './playoffBracket/playoffBracket.component';
import { TeamStandingsComponent } from './teamStandings/teamStandings.component';
import { SeasonTabsComponent } from './seasonTabs/seasonTabs.component';
import { SeasonThisWeekComponent } from './seasonThisWeek/seasonThisWeek.component';
import { SeasonEvent, signupsOpen } from '../../../shared/seasonEvents';
import { pageNow } from '../../../shared/pageNow';
import { BackfilledBannerComponent, DataNoticeComponent } from '../../../shared/dataNotice/dataNotice.component';
import { missing, SeasonSummaryService } from './seasonSummary';
import type { SeasonSummaryDto } from '@api/models';
import { seasonFormatLabel } from '../../../shared/seasonFormats';
import { SeasonChampionsComponent } from './seasonChampions/seasonChampions.component';
import { SeasonPlayerStatsComponent } from './seasonPlayerStats/seasonPlayerStats.component';
import { VodListComponent } from '../../../shared/vodList/vodList.component';
import { whileCurrent } from '../../../shared/whileCurrent';

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
        BackfilledBannerComponent,
        DataNoticeComponent,
        SeasonChampionsComponent,
        SeasonPlayerStatsComponent,
        VodListComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonComponent implements OnInit {
  private seasons = inject(SeasonService);
  private home = inject(HomeService);
  private summaries = inject(SeasonSummaryService);

  /** The ?tab= query parameter. */
  readonly tab = input<string | null | undefined>();
  readonly view = computed<SeasonView>(() => {
    const tab = this.tab();
    return tab === 'schedule' || tab === 'bracket' ? tab : 'overview';
  });
  readonly kicker = computed(() => ({ overview: 'Season overview', schedule: 'Regular season', bracket: 'Playoffs' })[this.view()]);

  /** The :seasonID of the route (0: the current season). Angular keeps this page when only it changes (another season
   * from the quick search or the side bar), so a new one loads that season in place of the one shown. */
  readonly seasonID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });
  /** The season shown: the route's, or the current season's ID once known. */
  readonly shownSeasonID = signal(0);
  readonly seasonName = signal<string | null>("Season");
  /** What the season is and has: its header, champions, and which sections have nothing to show. */
  readonly summary = signal<SeasonSummaryDto | null>(null);
  /** A backfilled or ended season without any matches: the schedule and this week say so. */
  readonly noMatches = computed(() => missing(this.summary(), s => s.matchCount));
  readonly noBracket = computed(() => missing(this.summary(), s => s.bracketMatchCount));
  /** A tournament is a bracket only (no schedule or standings). */
  readonly isTournament = computed(() => this.summary()?.format === 'Tournament');
  /** The format's pill, for a season that is not a league season. */
  readonly formatLabel = computed(() => this.isTournament() ? seasonFormatLabel(this.summary()?.format) : null);
  /** Why sections a league season has are not there. */
  readonly formatNotice = computed(() => this.isTournament()
    ? 'A tournament: a bracket only, with no regular season schedule or standings.'
    : null);
  /** The name is in: the page is shown then, as a longer name can move the Sign up button below it. */
  readonly nameLoaded = signal(false);
  /** The current and upcoming signups, drafts and seasons (empty until they load, or if they fail). */
  private readonly events = signal<SeasonEvent[]>([]);
  readonly eventsLoaded = signal(false);
  /** The page is shown once the name and whether to offer Sign up are known, so nothing moves. */
  readonly ready = computed(() => this.nameLoaded() && this.eventsLoaded());
  /** The server's time while the browser takes over its page, then the browser's (pageNow). */
  readonly now = pageNow();
  /** Sign up is offered only while this season's signups are open. */
  readonly canSignUp = computed(() => signupsOpen(this.events(), this.shownSeasonID(), this.now()));

  constructor() {
    effect(() => {
      const routeSeasonID = this.seasonID();
      untracked(() => {
        // Nothing of the previous season stays: its name and summary. The sections below get the new ID and load it.
        this.shownSeasonID.set(routeSeasonID);
        this.summary.set(null);
        this.seasonName.set("Season");
        this.nameLoaded.set(false);

        if (routeSeasonID === 0) {
          this.seasons.getCurrentSeasonId()
            .pipe(whileCurrent(routeSeasonID, this.seasonID))
            .subscribe({
              next: (result) => {
                this.shownSeasonID.set(result);
                this.getSummary();
              },
            });
        }
        else {
          this.getSummary();
        }
      });
    });
  }

  ngOnInit(): void {
    this.home.currentAndFutureEvents().subscribe({
      next: events => {
        this.events.set(events ?? []);
        this.eventsLoaded.set(true);
      },
      error: () => this.eventsLoaded.set(true),
    });
  }

  getSummary(): void {
    const seasonID = this.shownSeasonID();
    this.summaries.get(seasonID)
        .pipe(whileCurrent(seasonID, this.shownSeasonID))
        .subscribe({
          next: (result) => {
            this.summary.set(result);
            this.seasonName.set(result.seasonName);
            this.nameLoaded.set(true);
          },
          error: () => this.nameLoaded.set(true),
        });
  }

}
