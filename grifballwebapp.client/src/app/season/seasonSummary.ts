import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay, tap } from 'rxjs';

export interface ChampionPlayerDto {
  userID: number;
  gamertag: string;
  isCaptain: boolean;
}

/** A tier's champions: the team and the players who hold the title. */
export interface SeasonChampionDto {
  tier: string;
  teamID: number;
  teamName: string;
  roster: ChampionPlayerDto[];
}

/**
 * A season's header: what it is, its champions, and how much data of each kind it has, so each section can say what
 * is missing instead of showing an empty table.
 */
export interface SeasonSummaryDto {
  seasonID: number;
  seasonName: string;
  year: number;
  leagueID: number;
  leagueName: string;
  gameVersionName?: string | null;
  gameVersionSlug?: string | null;
  division?: string | null;
  /** Backfilled from the historical archive; historicalSourceUrl is its spreadsheet. */
  isHistorical: boolean;
  historicalSourceUrl?: string | null;
  teamCount: number;
  signupCount: number;
  matchCount: number;
  bracketMatchCount: number;
  playerStatCount: number;
  champions: SeasonChampionDto[];
}

/** The URL of a season's summary (index.html fetches it early for the season page). */
export const seasonSummaryUrl = (seasonID: number) => '/api/Season/getSeasonSummary/' + seasonID;

/** Season summaries, each fetched once per page load and shared by the season page and its sub-pages. */
@Injectable({ providedIn: 'root' })
export class SeasonSummaryService {
  private http = inject(HttpClient);
  private readonly cache = new Map<number, Observable<SeasonSummaryDto>>();

  get(seasonID: number): Observable<SeasonSummaryDto> {
    let summary = this.cache.get(seasonID);
    if (!summary) {
      summary = this.http.get<SeasonSummaryDto>(seasonSummaryUrl(seasonID)).pipe(
        // A failed fetch is not kept: the next page asks again
        tap({ error: () => this.cache.delete(seasonID) }),
        shareReplay({ bufferSize: 1, refCount: false }));
      this.cache.set(seasonID, summary);
    }
    return summary;
  }
}

/** The season is backfilled and has nothing of a kind: its sections say so in place of an empty table. */
export const missing = (summary: SeasonSummaryDto | null | undefined, count: (s: SeasonSummaryDto) => number) =>
  !!summary && summary.isHistorical && count(summary) === 0;
