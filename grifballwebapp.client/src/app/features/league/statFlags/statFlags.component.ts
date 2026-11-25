import { Component, OnInit, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import type { StatFlagDto } from '@api/models';
import { SkeletonComponent } from '@shared/skeleton/skeleton.component';
import { StatFlagsStore } from './statFlags.store';

export type { StatFlagDto };

@Component({
  selector: 'app-stat-flags',
  imports: [RouterLink, MatTableModule, MatButtonModule, MatIconModule, SkeletonComponent],
  templateUrl: './statFlags.component.html',
  styleUrls: ['./statFlags.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [StatFlagsStore],
})
export class StatFlagsComponent implements OnInit {
  private readonly store = inject(StatFlagsStore);

  readonly flags = this.store.flags;
  readonly loading = this.store.loading;
  readonly error = this.store.error;

  displayedColumns: string[] = ['rule', 'reason', 'player', 'game', 'foundAt', 'dismiss'];

  ngOnInit(): void {
    this.store.load();
  }

  /** Each rule, in plain words (the reason already says the specifics; this says what kind of question it is). */
  ruleLabel(rule: StatFlagDto['rule']): string {
    switch (rule) {
      case 'ScoreDoesNotMatchGoals': return "Score doesn't match goals";
      case 'WinnerDoesNotMatchScores': return "Winner doesn't match scores";
      case 'TeamSmallerThanFormat': return 'Team smaller than the format';
      case 'DateOutsideSeason': return 'Date outside the season';
      case 'NegativeStat': return 'Negative stat';
      case 'ImpossibleAccuracy': return 'Impossible accuracy or shots';
      case 'UndecodedCustomScore': return 'Undecoded custom score';
      case 'SeriesDoesNotMatchGames': return "Series doesn't match its games";
      case 'KillsDoNotMatchDeaths': return "Kills don't match deaths";
      case 'DateOutsideMatchWeek': return "Date outside the match's week";
      case 'StatsExceedGameLength': return "Stats beyond the game's length";
      default: return rule;
    }
  }

  formatDateTime(dateStr: string): string {
    return new Date(dateStr).toLocaleString();
  }

  dismiss(flag: StatFlagDto): void {
    this.store.dismiss(flag.statFlagID);
  }
}
