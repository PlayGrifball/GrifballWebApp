import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { map } from 'rxjs';
import { MatchDetailsService } from '@api/services/match-details.service';
import { ToDatePipe } from '@shared/toDate.pipe';
import { describePage, pageDate, PageDescription } from '@shared/pageMeta';
import { formatSeconds, MatchDetailsDto, MatchDetailsTeamDto, parseMatchDetails } from './matchDetailsDto';

/** A team's name on the page: its season team's, or Infinite's side when the game was not reported. */
export function teamName(team: MatchDetailsTeamDto): string {
  return team.seasonTeamName ?? `Team ${team.teamID + 1}`;
}

/**
 * The match as search results and link previews show it: the score, then where it was played and when
 * ("Red Rampage 5–3 Team 2"; "Autumn Brawl, game 2 of best of 3: Red Rampage beat Team 2 5–3 on Oct 30, 2026. …").
 */
export function describeMatch(match: MatchDetailsDto): PageDescription {
  const [first, second] = match.teams;
  if (!first || !second)
    return { title: 'Match Details' };
  const score = `${teamName(first)} ${first.score}–${second.score} ${teamName(second)}`;
  let where = 'Halo Infinite Grifball match';
  if (match.seasonMatch)
    where = `${match.seasonMatch.seasonName}, game ${match.seasonMatch.gameNumber} of best of ${match.seasonMatch.bestOf}`;
  const secondWon = second.outcome === 'Won' && first.outcome !== 'Won';
  const winner = secondWon ? second : first;
  const loser = secondWon ? first : second;
  let result = score;
  if (winner.outcome === 'Won')
    result = `${teamName(winner)} beat ${teamName(loser)} ${winner.score}–${loser.score}`;
  const date = pageDate(match.startTime);
  if (date)
    result = `${result} on ${date}`;
  return {
    title: score,
    description: `${where}: ${result}. Every player's goals, kills, deaths and medals.`,
  };
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
  private readonly api = inject(MatchDetailsService);

  /** The Waypoint match ID (the route's :matchID). */
  readonly matchID = input.required<string>();

  /** The match, loaded again for each match ID: a new one cancels the request still on its way (and so does leaving the page). */
  private readonly match = rxResource({
    params: () => ({ matchID: this.matchID() }),
    stream: ({ params }) => this.api.getMatchDetails(params).pipe(map(parseMatchDetails)),
  });

  readonly details = computed<MatchDetailsDto | null>(() => this.match.hasValue() ? this.match.value() : null);
  /** Loading; loaded; no such match (none, a 404, or an ID that isn't one: 400); or the request failed. */
  readonly state = computed<MatchDetailsState>(() => {
    if (this.match.isLoading())
      return 'loading';
    const error = this.match.error();
    if (error)
      return error instanceof HttpErrorResponse && (error.status === 404 || error.status === 400) ? 'notFound' : 'error';
    return this.details() ? 'loaded' : 'notFound';
  });

  /** The two sides for the scoreboard: the first team on the left (red), the second on the right (blue). */
  readonly sides = computed(() => this.details()?.teams ?? []);

  readonly teamName = teamName;
  readonly formatSeconds = formatSeconds;

  constructor() {
    describePage(() => {
      const details = this.details();
      return details ? describeMatch(details) : null;
    });
  }
}
