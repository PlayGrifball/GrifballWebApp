import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { SeasonMatchGameParticipantDto } from '../seasonMatchPageDto';

/**
 * A side's totals over its players; score only when some player has one. Its K/D counts as a player's does (the
 * server's StatMath.KdRatio): none without a death.
 */
export function boxScoreTotals(players: SeasonMatchGameParticipantDto[]) {
  const kills = players.reduce((sum, p) => sum + p.kills, 0);
  const deaths = players.reduce((sum, p) => sum + p.deaths, 0);
  const assists = players.reduce((sum, p) => sum + p.assists, 0);
  const bombScores = players.reduce((sum, p) => sum + p.bombScores, 0);
  const score = players.some(p => p.score != null) ? players.reduce((sum, p) => sum + (p.score ?? 0), 0) : null;
  return { kills, deaths, assists, bombScores, diff: kills - deaths, kdRatio: deaths > 0 ? kills / deaths : null, score };
}

/** Kills less deaths, signed. */
export function diff(kills: number, deaths: number): string {
  const d = kills - deaths;
  return d > 0 ? `+${d}` : `${d}`;
}

/**
 * One side of a match page box score: the team, its score, and a row a player (a game's, or totals over the series)
 * with the side's totals below. Home and away are the season match's, not the in-game team colours.
 */
@Component({
  selector: 'app-box-score',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './boxScore.component.html',
  styleUrl: './boxScore.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoxScoreComponent {
  readonly side = input.required<'home' | 'away'>();
  readonly teamName = input<string | null>(null);
  readonly score = input<number | null>(null);
  readonly players = input.required<SeasonMatchGameParticipantDto[]>();
  /** What the table is of, e.g. 'Game 2' or 'Series Totals', for its accessible name. */
  readonly heading = input.required<string>();

  readonly sideLabel = computed(() => this.side() === 'home' ? 'Home' : 'Away');
  readonly name = computed(() => this.teamName() ?? `${this.sideLabel()} Team`);
  readonly totals = computed(() => boxScoreTotals(this.players()));

  readonly diff = diff;
}
