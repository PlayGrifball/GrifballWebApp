import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiClientService } from '../api/apiClient.service';
import type { SeasonListItemDto } from '../api/dtos/seasonListItemDto';

interface Option { id: number; name: string; }

/** The most seasons the list is asked for at once: every season there is. */
export const SEASONS_PAGE_SIZE = 250;

/** Every season so far, the current ones included, filterable by league and game. */
@Component({
  selector: 'app-seasons',
  imports: [RouterLink],
  templateUrl: './seasons.component.html',
  styleUrl: './seasons.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeasonsComponent implements OnInit {
  private api = inject(ApiClientService);
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
  readonly query = signal('');

  readonly shown = computed(() => {
    const query = this.query().trim().toLowerCase();
    return query
      ? this.seasons().filter(s => s.name.toLowerCase().includes(query) || s.champions.some(c => c.teamName.toLowerCase().includes(query)))
      : this.seasons();
  });
  readonly empty = computed(() => this.loaded() && this.shown().length === 0);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  ngOnInit(): void {
    this.load(true);
  }

  setLeague(event: Event): void {
    this.league.set(optionValue(event));
    this.load(false);
  }

  setGame(event: Event): void {
    this.game.set(optionValue(event));
    this.load(false);
  }

  search(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  private load(options: boolean): void {
    // Only the latest filters' list is shown: an older answer arriving late is dropped
    this.request?.unsubscribe();
    this.loaded.set(false);
    this.request = this.api.getSeasonList({ leagueID: this.league(), gameVersionID: this.game() }, SEASONS_PAGE_SIZE).subscribe({
      next: page => {
        this.seasons.set(page.results);
        if (options) {
          this.leagues.set(distinct(page.results.map(s => ({ id: s.leagueID, name: s.leagueName }))));
          this.games.set(distinct(page.results.filter(s => s.gameVersionID != null).map(s => ({ id: s.gameVersionID!, name: s.gameVersionName ?? '' }))));
        }
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}

function optionValue(event: Event): number | null {
  const value = (event.target as HTMLSelectElement).value;
  return value === '' ? null : Number(value);
}

function distinct(options: Option[]): Option[] {
  const byID = new Map(options.map(o => [o.id, o]));
  return [...byID.values()].sort((a, b) => a.name.localeCompare(b.name));
}
