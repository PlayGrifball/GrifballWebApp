import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import { EventOrganizerService } from '@api/services/event-organizer.service';
import type { SeasonDto } from '@api/models';
// Type-only: pulling in SeasonSettings as a value would import Luxon into the main bundle (see getSeasons/getSeason).
import type { SeasonSettings } from './seasonSettings';
import { nullWhenNotFound } from './nullWhenNotFound';

/**
 * Seasons for the editor, with Luxon DateTimes instead of EventOrganizerService's raw SeasonDto (ISO strings). Luxon
 * is only loaded with the pages that need it (season editing), not with the app: the parser is imported when a
 * season arrives, kept out of this service's own module so component specs that mock it never pull it in either.
 */
@Injectable({ providedIn: 'root' })
export class SeasonSettingsApiService {
  private api = inject(EventOrganizerService);

  getSeasons(): Observable<SeasonSettings[]> {
    return this.api.getSeasons().pipe(switchMap(async seasons => {
      const { parseSeason } = await import('./seasonSettings');
      // A list never holds nulls (List<SeasonDto> on the server).
      return seasons.map(season => parseSeason(season)!);
    }));
  }

  /** Null when there is no such season (404). */
  getSeason(seasonID: number): Observable<SeasonSettings | null> {
    return this.api.getSeason({ seasonID }).pipe(
      nullWhenNotFound(),
      switchMap(async season => season && (await import('./seasonSettings')).parseSeason(season)),
    );
  }

  upsertSeason(seasonDto: SeasonSettings): Observable<number> {
    return this.api.upsertSeason({ body: toSeasonDto(seasonDto) });
  }
}

/**
 * The settings as the API takes them: each date as ISO 8601 in UTC (the DateTimes' own methods, so Luxon itself is not
 * imported here).
 */
export function toSeasonDto(settings: SeasonSettings): SeasonDto {
  const iso = (date: SeasonSettings['seasonStart']) => date.toUTC().toISO()!;
  return {
    ...settings,
    publicAt: settings.publicAt ? iso(settings.publicAt) : null,
    signupsOpen: iso(settings.signupsOpen),
    signupsClose: iso(settings.signupsClose),
    draftStart: iso(settings.draftStart),
    seasonStart: iso(settings.seasonStart),
    seasonEnd: iso(settings.seasonEnd),
  };
}
