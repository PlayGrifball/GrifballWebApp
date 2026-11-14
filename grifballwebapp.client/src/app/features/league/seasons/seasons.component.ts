import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import type { SeasonListItemDto } from '@api/models';
import { seasonFormatLabel, seasonFormats } from '@shared/seasonFormats';
import type { SeasonFormat } from '@api/models';
import { SeasonsStore } from './seasons.store';

/** Every season so far, the current ones included, filterable by league, game, format and year. */
@Component({
  selector: 'app-seasons',
  imports: [RouterLink],
  templateUrl: './seasons.component.html',
  styleUrl: './seasons.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [SeasonsStore],
})
export class SeasonsComponent {
  private readonly store = inject(SeasonsStore);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  /** The filters a link opens the list with (?league=, ?game=, ?format=, ?year=), e.g. a season's league or game label. */
  readonly league = input<string | undefined>(undefined);
  readonly game = input<string | undefined>(undefined);
  readonly format = input<string | undefined>(undefined);
  readonly year = input<string | undefined>(undefined);

  readonly seasons = this.store.seasons;
  readonly loaded = this.store.loaded;
  /** Nothing to show yet: the page fades in once the first list is in. */
  readonly waiting = computed(() => !this.loaded() && this.seasons().length === 0);
  /** The leagues, games and years there are seasons of (from the unfiltered list); the latest year first. */
  readonly leagues = this.store.leagues;
  readonly games = this.store.games;
  readonly years = this.store.years;
  readonly selectedLeague = signal<number | null>(null);
  readonly selectedGame = signal<number | null>(null);
  readonly selectedFormat = signal<SeasonFormat | null>(null);
  readonly selectedYear = signal<number | null>(null);
  readonly formats = seasonFormats;
  /** A season's format label: tournaments are labelled, a league season is not. */
  readonly formatLabel = (s: SeasonListItemDto) => s.format && s.format !== 'LeagueSeason' ? seasonFormatLabel(s.format) : null;
  readonly query = signal('');

  readonly shown = computed(() => {
    const query = this.query().trim().toLowerCase();
    return query
      // Every team of the season, champions included
      ? this.seasons().filter(s => s.seasonName.toLowerCase().includes(query)
        || s.teamNames.some(t => t.toLowerCase().includes(query)))
      : this.seasons();
  });
  readonly empty = computed(() => this.loaded() && this.shown().length === 0);

  private optionsLoaded = false;

  constructor() {
    // Opened, or linked to again, with filters in the URL (a league or game label elsewhere): the list they make. A
    // change made here is in the URL already (filtered) and loaded, so it is not loaded twice.
    effect(() => {
      const league = idParam(this.league());
      const game = idParam(this.game());
      const param = this.format();
      const format = seasonFormats.some(f => f.value === param) ? param as SeasonFormat : null;
      const year = idParam(this.year());
      untracked(() => {
        const first = !this.optionsLoaded;
        this.optionsLoaded = true;
        if (!first && league === this.selectedLeague() && game === this.selectedGame() && format === this.selectedFormat() && year === this.selectedYear())
          return;
        this.selectedLeague.set(league);
        this.selectedGame.set(game);
        this.selectedFormat.set(format);
        this.selectedYear.set(year);
        const unfiltered = league === null && game === null && format === null && year === null;
        // The choices are every league's, game's and year's: from the first list when it is unfiltered
        if (first && !unfiltered)
          this.store.loadOptions();
        this.load(first && unfiltered);
      });
    });
  }

  setLeague(event: Event): void {
    this.selectedLeague.set(optionValue(event));
    this.filtered();
  }

  setGame(event: Event): void {
    this.selectedGame.set(optionValue(event));
    this.filtered();
  }

  setFormat(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.selectedFormat.set(value === '' ? null : value as SeasonFormat);
    this.filtered();
  }

  setYear(event: Event): void {
    this.selectedYear.set(optionValue(event));
    this.filtered();
  }

  search(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  /** The filters changed: they go in the URL, so the filtered list can be linked to and comes back on Back. */
  private filtered(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { league: this.selectedLeague(), game: this.selectedGame(), format: this.selectedFormat(), year: this.selectedYear() },
      replaceUrl: true,
    });
    this.load(false);
  }

  private load(options: boolean): void {
    this.store.load({ leagueID: this.selectedLeague(), gameVersionID: this.selectedGame(), format: this.selectedFormat(), year: this.selectedYear() }, options);
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

