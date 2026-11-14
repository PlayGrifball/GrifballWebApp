import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterModule } from '@angular/router';
import { ToDatePipe } from '@shared/toDate.pipe';
import { describePage, pageDate, PageDescription } from '@shared/pageMeta';
import { formatSeconds, MatchDetailsDto, MatchDetailsTeamDto } from './matchDetailsDto';
import { MatchDetailsStore } from './matchDetails.store';

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
  providers: [MatchDetailsStore],
})
export class MatchDetailsComponent {
  private readonly store = inject(MatchDetailsStore);

  /** The Waypoint match ID (the route's :matchID). */
  readonly matchID = input.required<string>();

  readonly details = this.store.details;
  readonly state = this.store.state;

  /** The two sides for the scoreboard: the first team on the left (red), the second on the right (blue). */
  readonly sides = computed(() => this.details()?.teams ?? []);

  readonly teamName = teamName;
  readonly formatSeconds = formatSeconds;

  constructor() {
    this.store.matchID.use(this.matchID);
    describePage(() => {
      const details = this.details();
      return details ? describeMatch(details) : null;
    });
  }
}
