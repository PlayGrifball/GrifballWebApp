/** A source of the historical archive (HistoricalSourceDto): what the worker downloads, and how its last download went. */
export interface HistoricalSourceDto {
  sourceID: string;
  category: string;
  year: number | null;
  title: string;
  url: string;
  kind: string;
  league: string | null;
  gameVersion: string | null;
  format: string | null;
  enabled: boolean;
  isDead: boolean;
  /** The import reads it; the others are kept as a backup. */
  imported: boolean;
  lastFetchedAt: string | null;
  lastFetchStatus: string | null;
  lastChangedAt: string | null;
  snapshotCount: number;
  latestSnapshotSize: number | null;
}

export type HistoricalImportJobKind = 'Import' | 'Refresh' | 'Preview';
export type HistoricalImportJobStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed';
export type HistoricalSeasonReviewStatus = 'Preview' | 'NeedsReview' | 'Approved' | 'Skipped';

export interface HistoricalImportJobSummaryDto {
  jobID: number;
  kind: HistoricalImportJobKind;
  trigger: 'Manual' | 'Scheduled';
  status: HistoricalImportJobStatus;
  stage: string | null;
  createdAt: string;
  finishedAt: string | null;
  needsReview: number;
}

export interface HistoricalSeasonRemovalDto {
  seasonKey: string;
  seasonName: string | null;
  statsBefore: number;
  statsRemoved: number;
  teamsBefore: number;
  teamsRemoved: number;
  titlesBefore: number;
  titlesRemoved: number;
  stopped: boolean;
}

/** HistoricalImportReport: what an import did (or a preview would do), or what a refresh downloaded. */
export interface HistoricalImportReportDto {
  seasonsCreated: number;
  seasonsUpdated: number;
  teamsCreated: number;
  teamsRemoved: number;
  statsRemoved: number;
  championshipsRemoved: number;
  statsInserted: number;
  statsUpdated: number;
  recordedGamesImported: number;
  recordedGamesRemoved: number;
  championshipsInserted: number;
  championshipsUpdated: number;
  dummyUsersCreated: number;
  waypointMatchesPulled: number;
  skipped: string[];
  warnings: string[];
  errors: string[];
  seasonRemovals: HistoricalSeasonRemovalDto[];
  sourcesChanged: string[];
  sourcesFailed: string[];
  sourcesUnchanged: number;
  importJobID: number | null;
}

export interface HistoricalSeasonReviewDto {
  reviewID: number;
  seasonKey: string;
  seasonName: string | null;
  status: HistoricalSeasonReviewStatus;
  statsBefore: number;
  statsRemoved: number;
  teamsBefore: number;
  teamsRemoved: number;
  titlesBefore: number;
  titlesRemoved: number;
  approvalJobID: number | null;
  decidedAt: string | null;
}

export interface HistoricalImportJobDto {
  jobID: number;
  kind: HistoricalImportJobKind;
  trigger: 'Manual' | 'Scheduled';
  guarded: boolean;
  status: HistoricalImportJobStatus;
  stage: string | null;
  seasonsDone: number;
  seasonsTotal: number;
  report: HistoricalImportReportDto | null;
  error: string | null;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  reviews: HistoricalSeasonReviewDto[];
}

export interface HistoricalImportScheduleDto {
  paused: boolean;
  /** A TimeSpan, "1.00:00:00". */
  interval: string;
  lastRunAt: string | null;
  nextRunAt: string | null;
  lastJobID: number | null;
  lastRunNote: string | null;
}

/** The 202 of an action that queued a job. */
export interface HistoricalImportQueuedDto {
  jobID: number;
}
