import type { OnDemandJobStatusDto } from '../../api/dtos/onDemandJobDto';

/** The season's schedule recommendation (organizers): its run, a job on the worker, and the latest one it found. */
export interface ScheduleRecommendationPageDto {
  run: OnDemandJobStatusDto;
  latest: ScheduleRecommendationDto | null;
}

export interface ScheduleRecommendationDto {
  scheduleRecommendationID: number;
  createdAt: string;
  requestedBy: string | null;
  /** 0 to 1: 1 is no overlap and every team available. */
  fitness: number;
  generations: number;
  appliedAt: string | null;
  appliedBy: string | null;
  /** In order of the proposed time. */
  matches: ScheduleRecommendationMatchDto[];
}

export interface ScheduleRecommendationMatchDto {
  seasonMatchID: number;
  homeTeam: string;
  awayTeam: string;
  scheduledTime: string;
  /** The time it has now; null while unscheduled. */
  currentTime: string | null;
  overlaps: boolean;
  homeTeamAvailable: boolean;
  awayTeamAvailable: boolean;
  /** A result is in: applying leaves its time alone. */
  decided: boolean;
}
