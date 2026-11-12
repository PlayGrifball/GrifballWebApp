import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MetricRankDetail, PlayerAllTimeRankDto } from '@api/models';
import type { Board } from '../board';

type SpotlightBoard = Exclude<Board, 'dynasties'>;

const TILES: { board: SpotlightBoard, label: string, unit: [one: string, many: string], detail: (p: PlayerAllTimeRankDto) => MetricRankDetail }[] = [
  { board: 'rings', label: 'Championships', unit: ['Title', 'Titles'], detail: p => p.rings },
  { board: 'goals', label: 'Goals', unit: ['Goal', 'Goals'], detail: p => p.goals },
  { board: 'kills', label: 'Kills', unit: ['Kill', 'Kills'], detail: p => p.kills },
  { board: 'games', label: 'Games played', unit: ['Game', 'Games'], detail: p => p.gamesPlayed },
  { board: 'stops', label: 'Carrier stops', unit: ['Stop', 'Stops'], detail: p => p.carrierStops },
];

/** A player's standing on every board: each tile shows their rank and opens that board. */
@Component({
  selector: 'app-player-spotlight',
  imports: [RouterLink],
  templateUrl: './player-spotlight.component.html',
  styleUrl: './player-spotlight.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayerSpotlightComponent {
  readonly player = input.required<PlayerAllTimeRankDto>();
  /** The board on show, its tile marked pressed. */
  readonly board = input.required<Board>();
  readonly boardChange = output<Board>();
  readonly closed = output<void>();

  readonly tiles = computed(() => TILES.map(tile => {
    const detail = tile.detail(this.player());
    return {
      board: tile.board,
      label: tile.label,
      rank: detail.rank == null ? 'Unranked' : `#${detail.rank}`,
      of: `of ${detail.totalRanked}`,
      value: `${detail.statValue} ${tile.unit[detail.statValue === 1 ? 0 : 1]}`,
      secondary: detail.secondaryText,
    };
  }));
}
