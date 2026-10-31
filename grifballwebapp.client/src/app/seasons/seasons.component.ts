import { HttpClient, HttpParams } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PaginationResult } from '../shared/paginationResult';
import { SeasonChampionDto } from '../season/seasonSummary';

/** A season in the season list (GET /api/Season/GetSeasons). */
export interface SeasonListItemDto {
  seasonID: number;
  name: string;
  start: string;
  end: string;
  year: number;
  isCurrent: boolean;
  leagueID: number;
  leagueName: string;
  leagueSlug: string;
  gameVersionID?: number | null;
  gameVersionName?: string | null;
  gameVersionSlug?: string | null;
  division?: string | null;
  isHistorical: boolean;
  historicalSourceUrl?: string | null;
  teamCount: number;
  playerCount: number;
  champions: SeasonChampionDto[];
}

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
  private http = inject(HttpClient);

  readonly seasons = signal<SeasonListItemDto[]>([]);
  readonly loaded = signal(false);
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
    let params = new HttpParams().set('pageSize', SEASONS_PAGE_SIZE);
    const league = this.league();
    const game = this.game();
    if (league !== null) params = params.set('leagueID', league);
    if (game !== null) params = params.set('gameVersionID', game);
    this.loaded.set(false);
    this.http.get<PaginationResult<SeasonListItemDto>>('/api/Season/GetSeasons', { params }).subscribe({
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
