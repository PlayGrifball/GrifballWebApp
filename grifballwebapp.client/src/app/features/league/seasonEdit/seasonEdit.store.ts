import { inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { GameVersionOptionDto, LeagueOptionDto } from '@api/models';
import { EventOrganizerService } from '@api/services/event-organizer.service';
import { StoreInput } from '@shared/storeInput';
import { resourceView } from '@shared/resourceView';
import { tap } from 'rxjs';
import type { SeasonSettings } from '../api/seasonSettings';
import { SeasonSettingsApiService } from '../api/seasonSettingsApi.service';
import { AppLog } from '@core/telemetry/appLog';

/**
 * The season editor's data: the season being edited (none for a new one), the games, leagues and seasons its selects
 * offer, and saving it. One per page (the component provides it).
 */
@Injectable()
export class SeasonEditStore {
  private readonly log = inject(AppLog);
  private readonly eventOrganizer = inject(EventOrganizerService);
  private readonly api = inject(SeasonSettingsApiService);

  /** The route's season (0: a new one): the component's input. */
  readonly seasonID = new StoreInput<number>();

  /** The season to edit, loaded again for each season of the route (none for a new one): another drops the request still on its way. */
  private readonly loaded = resourceView(rxResource({
    params: () => {
      const seasonID = this.seasonID.value();
      return seasonID && seasonID > 0 ? seasonID : undefined;
    },
    stream: ({ params: seasonID }) => this.api.getSeason(seasonID).pipe(tap({ error: e => this.log.error('Failed to load the season', e) })),
  }), null);
  /** The season as it was loaded; null for a new one, while it loads, when there is no such season, or when it failed. */
  readonly season = this.loaded.value;
  /** The season to edit is on its way: the form is a skeleton. */
  readonly firstLoad = this.loaded.firstLoad;
  /** The season could not be loaded: the page says so, rather than offering a new season's form in its place. */
  readonly failed = this.loaded.failed;

  /** The games and leagues a season can have (empty if they fail to load). */
  private readonly _gameVersions = signal<GameVersionOptionDto[]>([]);
  readonly gameVersions = this._gameVersions.asReadonly();
  private readonly _leagues = signal<LeagueOptionDto[]>([]);
  readonly leagues = this._leagues.asReadonly();
  /** Every season, for "Copy from" (empty if it fails to load). */
  private readonly _seasons = signal<SeasonSettings[]>([]);
  readonly seasons = this._seasons.asReadonly();

  /** Loads what the selects offer. Without them their selects are empty; the rest of the form still works. */
  loadOptions(): void {
    this.eventOrganizer.getGameVersions().subscribe({
      next: gameVersions => this._gameVersions.set(gameVersions),
      error: error => this.log.error('Failed to load the game versions', error),
    });
    this.eventOrganizer.getLeagues().subscribe({
      next: leagues => this._leagues.set(leagues),
      error: error => this.log.error('Failed to load the leagues', error),
    });
    this.api.getSeasons().subscribe({
      next: seasons => this._seasons.set(seasons),
      error: error => this.log.error('Failed to load the seasons to copy from', error),
    });
  }

  /** Saves the season; `saved` gets its ID (a new one's, once made). A failure is logged. */
  save(season: SeasonSettings, saved: (seasonID: number) => void): void {
    this.api.upsertSeason(season).subscribe({
      next: seasonID => saved(seasonID),
      error: result => this.log.error('Failed to save the season', result),
    });
  }
}
