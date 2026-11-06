import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { map, Subscription } from 'rxjs';
import { ToDatePipe } from '../shared/toDate.pipe';
import { formatSeconds, MatchDetailsDto, MatchDetailsTeamDto, parseMatchDetails } from './matchDetailsDto';

/** A team's name on the page: its season team's, or Infinite's side when the game was not reported. */
export function teamName(team: MatchDetailsTeamDto): string {
  return team.seasonTeamName ?? `Team ${team.teamID + 1}`;
}

/** What the page shows: loading, the match, no such match pulled, or the request failed. */
export type MatchDetailsState = 'loading' | 'loaded' | 'notFound' | 'error';

/**
 * One Halo Infinite match by its Waypoint match ID: both teams' scores, every player's full stat line
 * and the medals they earned. Linked from the season match page's reported and possible games.
 */
@Component({
  selector: 'app-match-details',
  imports: [RouterModule, DatePipe, DecimalPipe, ToDatePipe],
  templateUrl: './matchDetails.component.html',
  styleUrl: './matchDetails.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MatchDetailsComponent {
  private readonly http = inject(HttpClient);

  /** The Waypoint match ID (the route's :matchID). */
  readonly matchID = input.required<string>();

  readonly details = signal<MatchDetailsDto | null>(null);
  readonly state = signal<MatchDetailsState>('loading');

  /** The two sides for the scoreboard: the first team on the left (red), the second on the right (blue). */
  readonly sides = computed(() => this.details()?.teams ?? []);

  readonly teamName = teamName;
  readonly formatSeconds = formatSeconds;

  constructor() {
    // A new match ID cancels the previous request, and so does leaving the page, so a slow response for the
    // match we just left can never land on this one.
    effect(onCleanup => {
      const request = this.load(this.matchID());
      onCleanup(() => request.unsubscribe());
    });
  }

  private load(matchID: string): Subscription {
    this.state.set('loading');
    this.details.set(null);
    return this.http.get<unknown>('/api/MatchDetails/GetMatchDetails/' + encodeURIComponent(matchID))
      .pipe(map(parseMatchDetails))
      .subscribe({
        next: details => {
          this.details.set(details);
          this.state.set(details ? 'loaded' : 'notFound');
        },
        error: (error: HttpErrorResponse) => this.state.set(error.status === 404 || error.status === 400 ? 'notFound' : 'error'),
      });
  }
}
