import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ApiClientService } from '../../api/apiClient.service';
import type { SeasonPlayerStatDto } from '../../api/dtos/seasonPlayerStatDto';

export type StatPhase = 'all' | 'regular' | 'playoffs';
/** The columns the leaderboard sorts by. */
export type StatSortColumn = 'goals' | 'kills' | 'gamesPlayed';
export type SortDirection = 'asc' | 'desc';

const SORTED: Record<SortDirection, { ariaSort: string, arrow: string }> = {
  asc: { ariaSort: 'ascending', arrow: '\u25B2' },
  desc: { ariaSort: 'descending', arrow: '\u25BC' },
};
const UNSORTED = { ariaSort: 'none', arrow: '' };

/**
 * The season's player stats, or one team's: a leaderboard by phase, filtered by gamertag or team and sorted by
 * clicking a column header (goals, kills or games played).
 * With a team, it shows nothing at all when the team has no stats, so the team page has no empty table.
 */
@Component({
  selector: 'app-season-player-stats',
  imports: [RouterLink],
  templateUrl: './seasonPlayerStats.component.html',
  styleUrl: './seasonPlayerStats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonPlayerStatsComponent {
  private api = inject(ApiClientService);

  readonly seasonID = input.required<number>();
  /** Only this team's players; null or unset for the whole season. */
  readonly teamID = input<number | null>(null);

  // A new season or team starts over (no rows) rather than showing the last one's while it loads.
  private readonly resource = rxResource({
    params: () => ({ seasonID: this.seasonID(), teamID: this.teamID() }),
    stream: ({ params }) => this.api.getSeasonPlayerStats(params.seasonID, params.teamID),
  });

  /** The rows as the server sent them (none while loading, or if they could not be loaded). */
  readonly stats = computed<SeasonPlayerStatDto[]>(() => this.resource.hasValue() ? this.resource.value() : []);
  readonly loaded = computed(() => !this.resource.isLoading());
  readonly hasRows = computed(() => this.stats().length > 0);
  /** A team without stats (or while they load) shows nothing; the season's table always shows. */
  readonly visible = computed(() => this.teamID() == null || this.hasRows());
  /** The team column, for the season's table: a team's own page does not need it. */
  readonly showTeam = computed(() => this.teamID() == null);

  readonly phase = signal<StatPhase>('all');
  readonly query = signal('');
  /** null: the server's order. */
  readonly sort = signal<{ column: StatSortColumn, direction: SortDirection } | null>(null);

  readonly phases: { id: StatPhase; title: string }[] = [
    { id: 'all', title: 'All' },
    { id: 'regular', title: 'Regular season' },
    { id: 'playoffs', title: 'Playoffs' },
  ];

  readonly shown = computed(() => {
    const phase = this.phase();
    const query = this.query().trim().toLowerCase();
    const rows = this.stats().filter(p =>
      (phase === 'all' || p.isPlayoffs === (phase === 'playoffs')) &&
      (!query || p.gamertag.toLowerCase().includes(query) || (p.teamName?.toLowerCase().includes(query) ?? false)));
    const sort = this.sort();
    if (!sort)
      return rows;
    const sign = sort.direction === 'asc' ? 1 : -1;
    // Stable: ties keep the server's order
    return [...rows].sort((a, b) => sign * (a[sort.column] - b[sort.column]));
  });

  /** Each sortable header's aria-sort, and the arrow beside it (decoration: aria-sort says it to screen readers). */
  readonly headers = computed(() => {
    const sort = this.sort();
    const header = (column: StatSortColumn) => sort?.column === column ? SORTED[sort.direction] : UNSORTED;
    return { goals: header('goals'), kills: header('kills'), gamesPlayed: header('gamesPlayed') };
  });

  /** Sorts by a column, most first; again flips it. */
  sortBy(column: StatSortColumn): void {
    this.sort.update(sort => sort?.column === column
      ? { column, direction: sort.direction === 'desc' ? 'asc' : 'desc' }
      : { column, direction: 'desc' });
  }

  search(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }
}
