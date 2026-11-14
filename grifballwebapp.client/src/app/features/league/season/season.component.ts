import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, numberAttribute } from '@angular/core';
import { RouterModule } from '@angular/router';
import { ScheduleListComponent } from "./scheduleList/scheduleList.component";
import { PlayoffBracketComponent } from './playoffBracket/playoffBracket.component';
import { TeamStandingsComponent } from './teamStandings/teamStandings.component';
import { SeasonTabsComponent } from './seasonTabs/seasonTabs.component';
import { SeasonThisWeekComponent } from './seasonThisWeek/seasonThisWeek.component';
import { signupsOpen } from '@shared/seasonEvents';
import { pageNow } from '@shared/pageNow';
import { BackfilledBannerComponent, DataNoticeComponent } from '@shared/dataNotice/dataNotice.component';
import { missing } from './seasonSummary';
import type { SeasonSummaryDto } from '@api/models';
import { seasonFormatLabel } from '@shared/seasonFormats';
import { fromRequiredApiDate } from '@shared/apiDates';
import { SeasonChampionsComponent } from './seasonChampions/seasonChampions.component';
import { SeasonPlayerStatsComponent } from './seasonPlayerStats/seasonPlayerStats.component';
import { VodListComponent } from '@shared/vodList/vodList.component';
import { SeasonStore } from './season.store';
import { describePage, pageDate, PageDescription, plural } from '@shared/pageMeta';

/** The sections shown on the season page itself (the other tabs are their own pages). */
export type SeasonView = 'overview' | 'schedule' | 'bracket';

/** The season as search results and link previews show it: its name, league, dates, size and champions. */
export function describeSeason(season: SeasonSummaryDto): PageDescription {
  const title = season.division ? `${season.seasonName} – ${season.division}` : season.seasonName;
  const start = pageDate(fromRequiredApiDate(season.start));
  const end = pageDate(fromRequiredApiDate(season.end));
  const facts = [
    season.signupCount ? plural(season.signupCount, 'player') : null,
    season.matchCount ? plural(season.matchCount, 'match', 'matches') : null,
    season.hasEnded ? describeChampions(season.champions) : null,
  ].filter(f => f);
  let summary = `${season.leagueName} Grifball season`;
  if (start && end)
    summary = `${summary}, ${start} to ${end}`;
  if (facts.length)
    summary = `${summary}: ${facts.join(', ')}`;
  return {
    title,
    description: `${summary}. Standings, schedule, brackets and stats.`,
    // /season/0 is the current season: its canonical address is the season's own
    path: `/season/${season.seasonID}`,
  };
}

/** The season's champions: the one team, or each tier's with its tier (not "A and B", which reads as a shared title). */
function describeChampions(champions: SeasonSummaryDto['champions']): string | null {
  if (champions.length === 0)
    return null;
  if (champions.length === 1)
    return `won by ${champions[0].teamName}`;
  return champions.map(c => `${c.tier} champions ${c.teamName}`).join(', ');
}

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
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [SeasonStore],
})
export class SeasonComponent implements OnInit {
  private readonly store = inject(SeasonStore);

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
  readonly shownSeasonID = this.store.shownSeasonID;
  readonly seasonName = this.store.seasonName;
  /** What the season is and has: its header, champions, and which sections have nothing to show. */
  readonly summary = this.store.summary;
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
  readonly nameLoaded = this.store.nameLoaded;
  /** The current and upcoming signups, drafts and seasons (empty until they load, or if they fail). */
  private readonly events = this.store.events;
  readonly eventsLoaded = this.store.eventsLoaded;
  /** The page is shown once the name and whether to offer Sign up are known, so nothing moves. */
  readonly ready = computed(() => this.nameLoaded() && this.eventsLoaded());
  /** The server's time while the browser takes over its page, then the browser's (pageNow). */
  readonly now = pageNow();
  /** Sign up is offered only while this season's signups are open. */
  readonly canSignUp = computed(() => signupsOpen(this.events(), this.shownSeasonID(), this.now()));

  constructor() {
    this.store.seasonID.use(this.seasonID);
    describePage(() => {
      const summary = this.summary();
      return summary ? describeSeason(summary) : null;
    });
  }

  ngOnInit(): void {
    this.store.loadEvents();
  }

  /** The bracket was made again or reseeded: the server cleared its champion, so the summary is read again. */
  getSummary(): void {
    this.store.loadSummary();
  }
}
