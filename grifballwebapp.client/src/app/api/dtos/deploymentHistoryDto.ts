import type { SeasonInfoDto } from './seasonInfoDto';

/** How far a team went in its season (the server's DeploymentFinish, sent by name), best first. */
export type DeploymentFinish = 'Champion' | 'Playoffs' | 'Eliminated' | 'RegularSeason' | 'InProgress';

/** One team a player was on in a season (DeploymentHistoryDto): a line of their profile's deployment history. */
export interface DeploymentHistoryDto {
  season: SeasonInfoDto;
  teamID: number;
  teamName: string;
  /** The player captained the team. */
  isCaptain: boolean;
  /** The team's record (not the player's own), as the standings count it. */
  wins: number;
  losses: number;
  /** The record is the source spreadsheet's: the season's matches are not in the app. */
  recordFromSource: boolean;
  finish: DeploymentFinish;
  /** The tiers of the titles the team won, A to Z; empty unless the finish is Champion. */
  championshipTiers: string[];
}

const FINISH_LABELS: Record<DeploymentFinish, string> = {
  Champion: 'Champion',
  Playoffs: 'Playoffs',
  Eliminated: 'Eliminated',
  RegularSeason: 'Regular season',
  InProgress: 'In progress',
};

/** A finish as the profile shows it: a champion's with each tier it won, as the season pages name them ("Open Champion", "Amateur & Pro Champion"). */
export function deploymentFinishLabel(line: Pick<DeploymentHistoryDto, 'finish' | 'championshipTiers'>): string {
  const label = FINISH_LABELS[line.finish] ?? line.finish;
  const tiers = line.championshipTiers ?? [];
  return line.finish === 'Champion' && tiers.length ? `${tiers.join(' & ')} ${label}` : label;
}
