import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

export interface ServiceRecordTotalsDto {
  gamesPlayed: number;
  wins: number;
  losses: number;
  winPercent?: number | null;
  kills: number;
  deaths: number;
  kdRatio?: number | null;
  /** Bomb scores (goals), every game's alike. */
  bombScores: number;
  /** Halo Infinite games whose score isn't goals (a custom mode's encoded counts), left out of the bomb scores. */
  uncountedBombScoreGames: number;
}

/** A profile's stat strip: MMR, and career totals over league seasons, matchmaking (Late League) and both. */
export interface ServiceRecordDto {
  /** The Late League rating; null for a player who has never played matchmaking. */
  mmr?: number | null;
  all: ServiceRecordTotalsDto;
  league: ServiceRecordTotalsDto;
  matchmaking: ServiceRecordTotalsDto;
}

export type ServiceRecordScope = 'all' | 'league' | 'matchmaking';

/** The stat strip at the top of a profile: K/D, MMR and bomb scores, over league, Late League or both. */
@Component({
  selector: 'app-service-record',
  imports: [DecimalPipe],
  templateUrl: './serviceRecord.component.html',
  styleUrl: './serviceRecord.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceRecordComponent {
  readonly record = input<ServiceRecordDto | null | undefined>(null);

  readonly scopes: { value: ServiceRecordScope; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'league', label: 'League' },
    { value: 'matchmaking', label: 'Late League' },
  ];
  readonly scope = signal<ServiceRecordScope>('all');

  readonly totals = computed<ServiceRecordTotalsDto | null>(() => this.record()?.[this.scope()] ?? null);

  selectScope(scope: ServiceRecordScope): void {
    this.scope.set(scope);
  }
}
