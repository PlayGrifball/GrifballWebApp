import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiClientService } from '../api/apiClient.service';
import type { SeasonListItemDto } from '../api/dtos/seasonListItemDto';
import { seasonFormatLabel, seasonFormats, type SeasonFormat } from '../api/dtos/seasonFormat';

interface Option { id: number; name: string; }

/** The most seasons the list is asked for at once: every season there is. */
export const SEASONS_PAGE_SIZE = 250;

/** Every season so far, the current ones included, filterable by league, game and format. */
@Component({
  selector: 'app-seasons',
  imports: [RouterLink],
  templateUrl: './seasons.component.html',
  styleUrl: './seasons.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonsComponent {
  private api = inject(ApiClientService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  /** The filters a link opens the list with (?league=, ?game=, ?format=), e.g. a season's league or game label. */
  readonly leagueParam = input<string | undefined>(undefined, { alias: 'league' });
  readonly gameParam = input<string | undefined>(undefined, { alias: 'game' });
  readonly formatParam = input<string | undefined>(undefined, { alias: 'format' });
  private request?: Subscription;

  readonly seasons = signal<SeasonListItemDto[]>([]);
  readonly loaded = signal(false);
  /** Nothing to show yet: the page fades in once the first list is in. */
  readonly waiting = computed(() => !this.loaded() && this.seasons().length === 0);
  /** The leagues and games there are seasons of (from the unfiltered list). */
  readonly leagues = signal<Option[]>([]);
  readonly games = signal<Option[]>([]);
  readonly league = signal<number | null>(null);
  readonly game = signal<number | null>(null);
  readonly format = signal<SeasonFormat | null>(null);
  readonly formats = seasonFormats;
  /** A season's format label: tournaments and Late League are labelled, a league season is not. */
  readonly formatLabel = (s: SeasonListItemDto) => s.format && s.format !== 'LeagueSeason' ? seasonFormatLabel(s.format) : null;
  readonly query = signal('');

  readonly shown = computed(() => {
    const query = this.query().trim().toLowerCase();
    return query
      ? this.seasons().filter(s => s.seasonName.toLowerCase().includes(query) || s.champions.some(c => c.teamName.toLowerCase().includes(query)))
      : this.seasons();
  });
  readonly empty = computed(() => this.loaded() && this.shown().length === 0);

  private optionsLoaded = false;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
    // Opened, or linked to again, with filters in the URL (a league or game label elsewhere): the list they make. A
    // change made here is in the URL already (filtered) and loaded, so it is not loaded twice.
    effect(() => {
      const league = idParam(this.leagueParam());
      const game = idParam(this.gameParam());
      const param = this.formatParam();
      const format = seasonFormats.some(f => f.value === param) ? param as SeasonFormat : null;
      untracked(() => {
        const first = !this.optionsLoaded;
        this.optionsLoaded = true;
        if (!first && league === this.league() && game === this.game() && format === this.format())
          return;
        this.league.set(league);
        this.game.set(game);
        this.format.set(format);
        const unfiltered = league === null && game === null && format === null;
        // The choices are every league's and game's: from the first list when it is unfiltered
        if (first && !unfiltered)
          this.api.getSeasonList({ leagueID: null, gameVersionID: null, format: null }, SEASONS_PAGE_SIZE).subscribe(page => this.setOptions(page.results));
        this.load(first && unfiltered);
      });
    });
  }

  setLeague(event: Event): void {
    this.league.set(optionValue(event));
    this.filtered();
  }

  setGame(event: Event): void {
    this.game.set(optionValue(event));
    this.filtered();
  }

  setFormat(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.format.set(value === '' ? null : value as SeasonFormat);
    this.filtered();
  }

  search(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  /** The filters changed: they go in the URL, so the filtered list can be linked to and comes back on Back. */
  private filtered(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { league: this.league(), game: this.game(), format: this.format() },
      replaceUrl: true,
    });
    this.load(false);
  }

  private setOptions(seasons: SeasonListItemDto[]): void {
    this.leagues.set(distinct(seasons.map(s => ({ id: s.leagueID, name: s.leagueName }))));
    this.games.set(distinct(seasons.filter(s => s.gameVersionID != null).map(s => ({ id: s.gameVersionID!, name: s.gameVersionName ?? '' }))));
  }

  private load(options: boolean): void {
    // Only the latest filters' list is shown: an older answer arriving late is dropped
    this.request?.unsubscribe();
    this.loaded.set(false);
    this.request = this.api.getSeasonList({ leagueID: this.league(), gameVersionID: this.game(), format: this.format() }, SEASONS_PAGE_SIZE).subscribe({
      next: page => {
        this.seasons.set(page.results);
        if (options)
          this.setOptions(page.results);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}

function idParam(value: string | undefined): number | null {
  const id = Number(value);
  return value !== undefined && value !== '' && Number.isInteger(id) ? id : null;
}

function optionValue(event: Event): number | null {
  const value = (event.target as HTMLSelectElement).value;
  return value === '' ? null : Number(value);
}

function distinct(options: Option[]): Option[] {
  const byID = new Map(options.map(o => [o.id, o]));
  return [...byID.values()].sort((a, b) => a.name.localeCompare(b.name));
}
