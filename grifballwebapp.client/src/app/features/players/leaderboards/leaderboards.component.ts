import { formatNumber } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, LOCALE_ID, OnInit, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { catchError, EMPTY, map, of, Subject, switchMap, tap } from 'rxjs';
import { LeaderboardsService } from '@api/services/leaderboards.service';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import type { LeaderboardsDto, MetricLeaderDto } from '@api/models';
import type { PlayerAllTimeRankDto } from '@api/models';
import type { QuickSearchPlayerDto } from '@api/models';
import { leaderboardFormats } from '@shared/seasonFormats';
import type { LeaderboardFilter } from '@api/models';
import type { Board } from './board';
import { PlayerSearchComponent } from './playerSearch/playerSearch.component';
import { PlayerSpotlightComponent } from './playerSpotlight/playerSpotlight.component';

type MetricBoard = Exclude<Board, 'rings' | 'dynasties'>;

const ALL_FORMATS: LeaderboardFilter[] = leaderboardFormats.map(f => f.value);

const METRICS: Record<MetricBoard, { title: string, list: (l: LeaderboardsDto) => MetricLeaderDto[] }> = {
  goals: { title: 'Goals', list: l => l.goalLeaders },
  kills: { title: 'Kills', list: l => l.killLeaders },
  games: { title: 'Games played', list: l => l.gamesPlayedLeaders },
  stops: { title: 'Carrier stops', list: l => l.carrierStopLeaders },
};

const isMetric = (board: Board): board is MetricBoard => board in METRICS;

/** A player to show the standing of: by user ID when picked from the suggestions, else by the text searched. */
interface Lookup { query: string, userId: number | null }

/** The all-time leaderboards over every season: titles, dynasties and career totals, and a player's standing on each. */
@Component({
  selector: 'app-leaderboards',
  imports: [MatFormFieldModule, MatSelectModule, RouterLink, PlayerSearchComponent, PlayerSpotlightComponent],
  templateUrl: './leaderboards.component.html',
  styleUrl: './leaderboards.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LeaderboardsComponent implements OnInit {
  private leaderboards = inject(LeaderboardsService);
  private locale = inject(LOCALE_ID);

  readonly boards = signal<LeaderboardsDto | null>(null);
  readonly loaded = signal(false);
  readonly board = signal<Board>('rings');
  readonly formatOptions = leaderboardFormats;
  /** The formats every board counts, titles and stats alike: any of them, all of them to start with. */
  readonly formats = signal<LeaderboardFilter[]>(ALL_FORMATS);
  /** The select's text: "All formats", or the formats chosen ("League season, Late League"). */
  readonly formatsLabel = computed(() => {
    const chosen = this.formats();
    return chosen.length === ALL_FORMATS.length ? 'All formats' : leaderboardFormats.filter(f => chosen.includes(f.value)).map(f => f.label).join(', ');
  });

  /** The text in the player search. */
  readonly searchQuery = signal('');
  readonly spotlight = signal<PlayerAllTimeRankDto | null>(null);
  readonly spotlightLoading = signal(false);
  readonly spotlightError = signal<string | null>(null);

  private readonly search = viewChild.required(PlayerSearchComponent);

  /** The player last asked for (null: none), shown or still loading: a new format looks them up again. */
  private requested: Lookup | null = null;

  /** Each lookup (or null: none) cancels the one in flight, so an older player's standing never replaces a newer one. */
  private readonly lookups = new Subject<Lookup | null>();
  /** Each load cancels the one in flight, so the boards shown are always the formats chosen last. */
  private readonly loads = new Subject<LeaderboardFilter[] | undefined>();

  readonly tabs: { id: Board, title: string }[] = [
    { id: 'rings', title: 'Championships' },
    { id: 'dynasties', title: 'Dynasties' },
    { id: 'goals', title: 'Goals' },
    { id: 'kills', title: 'Kills' },
    { id: 'games', title: 'Games played' },
    { id: 'stops', title: 'Carrier stops' },
  ];

  /** The table's accessible name: the chosen board's. */
  readonly tableLabel = computed(() => `${this.tabs.find(t => t.id === this.board())!.title} leaderboard`);

  /** The chosen career-total board, its per-game figures ready to show; null for the titles boards. */
  readonly metric = computed(() => {
    const board = this.board();
    const boards = this.boards();
    if (!boards || !isMetric(board))
      return null;
    return {
      title: METRICS[board].title,
      leaders: METRICS[board].list(boards).map(leader => ({
        ...leader,
        perGameText: leader.perGame == null ? '-' : formatNumber(leader.perGame, this.locale, '1.2-2'),
      })),
    };
  });

  constructor() {
    this.lookups.pipe(
      tap(lookup => {
        this.spotlightError.set(null);
        this.spotlightLoading.set(lookup != null);
        if (!lookup) this.spotlight.set(null);
      }),
      switchMap(lookup => !lookup ? EMPTY : this.leaderboards.getPlayerRank({ query: lookup.query || undefined, userID: lookup.userId ?? undefined, formats: this.formatsAsked() })
        .pipe(
        nullWhenNotFound(), // 404: no player matches
        map(rank => ({ lookup, rank, failed: false })),
        catchError(() => of({ lookup, rank: null, failed: true })),
      )),
      takeUntilDestroyed(),
    ).subscribe(({ lookup, rank, failed }) => {
      this.spotlightLoading.set(false);
      this.spotlight.set(rank);
      if (!rank)
        this.spotlightError.set(failed ? `Failed to load ranking for "${lookup.query}".` : `No player found matching "${lookup.query}".`);
    });

    this.loads.pipe(
      switchMap(formats => this.leaderboards.getLeaderboards({ formats }).pipe(catchError(() => of(null)))),
      takeUntilDestroyed(),
    ).subscribe(boards => {
      if (boards)
        this.boards.set(boards);
      this.loaded.set(true);
    });
  }

  ngOnInit(): void {
    this.load();
  }

  /** Counts the formats chosen, in the select's order; none chosen counts them all again. */
  setFormats(chosen: LeaderboardFilter[]): void {
    this.formats.set(chosen.length === 0 ? ALL_FORMATS : ALL_FORMATS.filter(f => chosen.includes(f)));
    this.load();
    // The standing counts the formats too: the player last asked for, even while their standing is loading
    if (this.requested)
      this.lookUp(this.requested);
  }

  /** The formats asked of the server: none named when every one is (its default). */
  private formatsAsked(): LeaderboardFilter[] | undefined {
    const chosen = this.formats();
    return chosen.length === ALL_FORMATS.length ? undefined : chosen;
  }

  selectPlayer(player: QuickSearchPlayerDto): void {
    this.lookUp({ query: player.name, userId: player.userID });
  }

  findPlayer(query: string): void {
    this.lookUp({ query, userId: null });
  }

  /** Closes the standing, and empties the search, dropping its pending suggestions too. */
  clearSpotlight(): void {
    this.search().reset();
    this.lookUp(null);
  }

  private lookUp(lookup: Lookup | null): void {
    this.requested = lookup;
    this.lookups.next(lookup);
  }

  private load(): void {
    this.loads.next(this.formatsAsked());
  }
}
