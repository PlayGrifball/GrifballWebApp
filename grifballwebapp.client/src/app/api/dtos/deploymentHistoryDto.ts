import type { SeasonInfoDto } from './seasonInfoDto';

/** How far a team went in its season (the server's DeploymentFinish, sent by name), best first. */
export type DeploymentFinish = 'Champion' | 'Playoffs' | 'RegularSeason' | 'InProgress';

/** One team a player was on in a season (DeploymentHistoryDto): a line of their profile's deployment history. */
export interface DeploymentHistoryDto {
  season: SeasonInfoDto;
  teamID: number;
  teamName: string;
  /** The player captained the team. */
  isCaptain: boolean;
  /** The team's record, as the standings count it. */
  wins: number;
  losses: number;
  /** The record is the source spreadsheet's: the season's matches are not in the app. */
  recordFromSource: boolean;
  finish: DeploymentFinish;
  /** The tier of the title the team won; null unless the finish is Champion. */
  championshipTier?: string | null;
}

const FINISH_LABELS: Record<DeploymentFinish, string> = {
  Champion: 'Champion',
  Playoffs: 'Playoffs',
  RegularSeason: 'Regular season',
  InProgress: 'In progress',
};

/** A finish as the profile shows it: a champion's with its tier ("Pro Champion"), unless the tier is the default one. */
export function deploymentFinishLabel(line: Pick<DeploymentHistoryDto, 'finish' | 'championshipTier'>): string {
  const label = FINISH_LABELS[line.finish] ?? line.finish;
  return line.finish === 'Champion' && line.championshipTier && line.championshipTier !== 'Open'
    ? `${line.championshipTier} ${label}`
    : label;
}
