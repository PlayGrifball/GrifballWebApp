import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import type { GameVersionOptionDto, LeagueOptionDto } from '@api/models';
import { EventOrganizerService } from '@api/services/event-organizer.service';
import { StoreInput } from '@shared/storeInput';
import { whileCurrent } from '@shared/whileCurrent';
import type { SeasonSettings } from '../api/seasonSettings';
import { SeasonSettingsApiService } from '../api/seasonSettingsApi.service';

/**
 * The season editor's data: the season being edited (none for a new one), the games, leagues and seasons its selects
 * offer, and saving it. One per page (the component provides it).
 */
@Injectable()
export class SeasonEditStore {
  private readonly eventOrganizer = inject(EventOrganizerService);
  private readonly api = inject(SeasonSettingsApiService);

  /** The route's season (0: a new one): the component's input. */
  readonly seasonID = new StoreInput<number>();

  /** The season as it was loaded; null for a new one, while it loads, or when there is no such season. */
  private readonly _season = signal<SeasonSettings | null>(null);
  readonly season = this._season.asReadonly();

  /** The games and leagues a season can have (empty if they fail to load). */
  private readonly _gameVersions = signal<GameVersionOptionDto[]>([]);
  readonly gameVersions = this._gameVersions.asReadonly();
  private readonly _leagues = signal<LeagueOptionDto[]>([]);
  readonly leagues = this._leagues.asReadonly();
  /** Every season, for "Copy from" (empty if it fails to load). */
  private readonly _seasons = signal<SeasonSettings[]>([]);
  readonly seasons = this._seasons.asReadonly();

  constructor() {
    effect(() => {
      const seasonID = this.seasonID.value();
      if (seasonID !== undefined)
        untracked(() => this.load(seasonID));
    });
  }

  /** Loads what the selects offer. Without them their selects are empty; the rest of the form still works. */
  loadOptions(): void {
    this.eventOrganizer.getGameVersions().subscribe({
      next: gameVersions => this._gameVersions.set(gameVersions),
      error: error => console.error(error),
    });
    this.eventOrganizer.getLeagues().subscribe({
      next: leagues => this._leagues.set(leagues),
      error: error => console.error(error),
    });
    this.api.getSeasons().subscribe({
      next: seasons => this._seasons.set(seasons),
      error: error => console.error(error),
    });
  }

  /** Saves the season; `saved` gets its ID (a new one's, once made). A failure is logged. */
  save(season: SeasonSettings, saved: (seasonID: number) => void): void {
    this.api.upsertSeason(season).subscribe({
      next: seasonID => saved(seasonID),
      error: result => console.log(result),
    });
  }

  private load(seasonID: number): void {
    this._season.set(null);
    if (seasonID > 0) {
      this.api.getSeason(seasonID)
        .pipe(whileCurrent(seasonID, this.seasonID.value))
        .subscribe({ next: season => this._season.set(season) });
    }
  }
}
