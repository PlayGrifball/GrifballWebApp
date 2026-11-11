import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable, switchMap } from 'rxjs';
import type { SeasonDto } from './dtos/seasonDto';
import { SignupResponseDto, TimeslotDto } from './dtos/signupResponseDto';
import { SignupRequestDto } from './dtos/signupRequestDto';
import { TeamResponseDto } from './dtos/teamResponseDto';
import { PlayerDto } from './dtos/playerDto';
import { CaptainAddedResponseDto, CaptainPlacementDto, RemoveCaptainDto } from './dtos/captainPlacementDto';
import { RemovePlayerFromTeamRequestDto } from './dtos/RemovePlayerFromTeamRequestDto';
import { MovePlayerToTeamRequestDto } from './dtos/MovePlayerToTeamRequestDto';
import { AddPlayerToTeamRequestDto } from './dtos/AddPlayerToTeamRequestDto';
import { SeasonEvent } from '../shared/seasonEvents';
import type { GameVersionDto, LeagueDto } from './dtos/optionDtos';
import { seasonSummaryUrl, type SeasonSummaryDto } from './dtos/seasonSummaryDto';
import type { SeasonTeamDto } from './dtos/seasonTeamDto';
import type { SeasonPlayerStatDto } from './dtos/seasonPlayerStatDto';
import type { SeasonListFilter, SeasonListItemDto } from './dtos/seasonListItemDto';
import type { LeaderboardsDto } from './dtos/leaderboardsDto';
import type { QuickSearchDto, QuickSearchPlayerDto } from './dtos/quickSearchDto';
import type { MatchmakingQueueDto, MatchmakingSeasonDetailDto, MatchmakingSeasonDto } from './dtos/lateLeagueDto';
import type { PlayerAllTimeRankDto } from './dtos/playerAllTimeRankDto';
import type { SeasonFormat } from './dtos/seasonFormat';
import type { LogsDto, LogsQuery, MetricsDto, TelemetryStatusDto, TraceDto, TracesQuery, TraceSummaryDto } from './dtos/telemetryDto';
import type {
  AddGamertagHistoryDto, ClaimPreviewDto, DuplicateSuggestionDto, GamertagClaimDto, GamertagClaimStatus, PastGamertagsDto,
  SubmitGamertagClaimDto, XuidLookupStatusDto,
} from './dtos/gamertagClaimDto';
import type { UserMergeDto } from './dtos/userMergeDto';
import type {
  HistoricalImportJobDto, HistoricalImportJobSummaryDto, HistoricalImportQueuedDto, HistoricalImportScheduleDto, HistoricalSourceDto,
} from './dtos/historicalImportDto';
import type { JobKeyDto, JobRunDto, JobsOverviewDto } from './dtos/jobsDto';
import type { PaginationResult } from '../shared/paginationResult';
import type { LinkVodRequest, VodDto, VodLinkOptionsDto, VodLinkSeasonDto, VodReviewDto, VodScope } from './dtos/vodDto';
import { nullWhenNotFound } from './nullWhenNotFound';

/** The resource whose VODs each page shows. */
const VOD_OWNERS: Record<VodScope, string> = { seasonMatch: 'season-matches', season: 'seasons', team: 'teams', player: 'users' };

@Injectable({
  providedIn: 'root'
})
export class ApiClientService {
  private http = inject(HttpClient);


  private readonly text : Object = {
    responseType: 'text'
  };


  // A season's dates are Luxon DateTimes, and Luxon is only loaded with the pages that need it
  // (season editing), not with the app: the parser is imported when a season arrives.
  getSeasons(): Observable<SeasonDto[]> {
    return this.http.get<unknown[]>('/api/seasons/settings')
      // A list never holds nulls (List<SeasonDto> on the server).
      .pipe(switchMap(async seasons => {
        const { parseSeason } = await import('./dtos/seasonDto');
        return seasons.map(season => parseSeason(season)!);
      }));
  }

  /** Null when there is no such season (404). */
  getSeason(seasonID: number): Observable<SeasonDto | null> {
    return this.http.get<unknown>(`/api/seasons/${seasonID}/settings`)
      .pipe(nullWhenNotFound(), switchMap(async season => (await import('./dtos/seasonDto')).parseSeason(season)));
  }

  upsertSeason(seasonDto: SeasonDto): Observable<number> {
    return this.http.post<number>('/api/seasons/settings', seasonDto);
  }

  /** The games a season can be played in, for the season editor. */
  getGameVersions(): Observable<GameVersionDto[]> {
    return this.http.get<GameVersionDto[]>('/api/game-versions');
  }

  /** The leagues a season can belong to, for the season editor. */
  getLeagues(): Observable<LeagueDto[]> {
    return this.http.get<LeagueDto[]>('/api/leagues');
  }

  /** A season's header, champions and how much of each kind of data it has. */
  getSeasonSummary(seasonID: number): Observable<SeasonSummaryDto> {
    return this.http.get<SeasonSummaryDto>(seasonSummaryUrl(seasonID));
  }

  /** The season's teams and their rosters: records, titles, and players. */
  getSeasonTeams(seasonID: number): Observable<SeasonTeamDto[]> {
    return this.http.get<SeasonTeamDto[]>(`/api/seasons/${seasonID}/teams`);
  }

  /** The season's player stats; with a team, only that team's rows. */
  getSeasonPlayerStats(seasonID: number, teamID?: number | null): Observable<SeasonPlayerStatDto[]> {
    const params = teamID != null ? new HttpParams().set('teamID', teamID) : undefined;
    return this.http.get<SeasonPlayerStatDto[]>(`/api/seasons/${seasonID}/player-stats`, { params });
  }

  /** Every season, the current ones included (the seasons page), of one league, game, format and year when given. */
  getSeasonList(filter: SeasonListFilter, pageSize: number): Observable<PaginationResult<SeasonListItemDto>> {
    let params = new HttpParams().set('pageSize', pageSize);
    if (filter.leagueID !== null) params = params.set('leagueID', filter.leagueID);
    if (filter.gameVersionID !== null) params = params.set('gameVersionID', filter.gameVersionID);
    if (filter.format) params = params.set('format', filter.format);
    if (filter.year != null) params = params.set('year', filter.year);
    return this.http.get<PaginationResult<SeasonListItemDto>>('/api/seasons', { params });
  }

  /** The Ctrl+K navigator's hits: players (by current and past gamertags), teams and seasons. */
  quickSearch(q: string): Observable<QuickSearchDto> {
    return this.http.get<QuickSearchDto>('/api/search', { params: new HttpParams().set('q', q) });
  }

  /** The all-time leaderboards: titles (of seasons of one format, or all), dynasties and career totals. */
  getLeaderboards(format: SeasonFormat | null = null): Observable<LeaderboardsDto> {
    const params = format ? new HttpParams().set('format', format) : undefined;
    return this.http.get<LeaderboardsDto>('/api/leaderboards', { params });
  }

  /** Late League as it is played now: the Discord queue's reported games and each player's totals. */
  getLateLeagueQueue(): Observable<MatchmakingQueueDto> {
    return this.http.get<MatchmakingQueueDto>('/api/late-league/queue');
  }

  /** Late League's seasons of the archive, latest first. */
  getLateLeagueSeasons(): Observable<MatchmakingSeasonDto[]> {
    return this.http.get<MatchmakingSeasonDto[]>('/api/late-league/seasons');
  }

  /** A Late League matchmaking season and its players' stats. */
  getLateLeagueSeason(matchmakingSeasonID: number): Observable<MatchmakingSeasonDetailDto> {
    return this.http.get<MatchmakingSeasonDetailDto>(`/api/late-league/seasons/${matchmakingSeasonID}`);
  }

  /** Find a player's all-time ranking summary and totals across every tracked metric. */
  getPlayerRank(query: string | null = null, userID: number | null = null, format: SeasonFormat | null = null): Observable<PlayerAllTimeRankDto | null> {
    let params = new HttpParams();
    if (query) params = params.set('query', query);
    if (userID != null) params = params.set('userID', userID);
    if (format) params = params.set('format', format);
    // 404: no player matches
    return this.http.get<PlayerAllTimeRankDto>('/api/leaderboards/player-rank', { params }).pipe(nullWhenNotFound());
  }

  /** The leaderboards' player suggestions: the toolbar search's players, by its rules. */
  searchPlayers(query: string): Observable<QuickSearchPlayerDto[]> {
    const params = new HttpParams().set('query', query);
    return this.http.get<QuickSearchPlayerDto[]>('/api/leaderboards/players', { params });
  }

  /** The gamertag claims with this status, for the sysadmins to review. */
  getGamertagClaims(status: GamertagClaimStatus): Observable<GamertagClaimDto[]> {
    return this.http.get<GamertagClaimDto[]>('/api/GamertagClaims/GetClaims', { params: new HttpParams().set('status', status) });
  }

  /** Answers 204; a refusal is a 400 or 409 with a ProblemDetails body. */
  approveGamertagClaim(gamertagClaimID: number): Observable<void> {
    return this.http.post<void>('/api/GamertagClaims/ApproveClaim/' + gamertagClaimID, null);
  }

  /** Answers 204; a refusal is a 400 or 409 with a ProblemDetails body. The reason, if any, is shown to the claimant. */
  rejectGamertagClaim(gamertagClaimID: number, reason: string | null = null): Observable<void> {
    return this.http.post<void>('/api/GamertagClaims/RejectClaim/' + gamertagClaimID, { reason });
  }

  /** Placeholders that may be the same player as another user, for a sysadmin to merge or dismiss. */
  getDuplicateSuggestions(): Observable<DuplicateSuggestionDto[]> {
    return this.http.get<DuplicateSuggestionDto[]>('/api/GamertagClaims/DuplicateSuggestions');
  }

  /** Merges the suggested placeholder into the other user (audited); a refusal is a 400 or 409 with a ProblemDetails body. */
  mergeDuplicate(sourceUserID: number, targetUserID: number): Observable<void> {
    return this.http.post<void>('/api/GamertagClaims/MergeDuplicate', { sourceUserID, targetUserID });
  }

  /** The pair are different players: not suggested again. */
  dismissDuplicate(sourceUserID: number, targetUserID: number): Observable<void> {
    return this.http.post<void>('/api/GamertagClaims/DismissDuplicate', { sourceUserID, targetUserID });
  }

  /** The merges' audit trail, newest first. */
  getUserMerges(pageNumber: number, pageSize: number): Observable<PaginationResult<UserMergeDto>> {
    const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    return this.http.get<PaginationResult<UserMergeDto>>('/api/UserMerges/List', { params });
  }

  /** Puts back what a merge changed; a refusal is a 409 whose ProblemDetails says why. */
  undoUserMerge(userMergeID: number, note: string | null): Observable<void> {
    return this.http.post<void>('/api/UserMerges/Undo/' + userMergeID, { note });
  }

  /** What claiming a past gamertag would merge, for the signed-in user. */
  previewGamertagClaim(gamertag: string): Observable<ClaimPreviewDto> {
    return this.http.get<ClaimPreviewDto>('/api/me/gamertag-claims/preview', { params: new HttpParams().set('gamertag', gamertag) });
  }

  /** Claims a past gamertag for the signed-in user; a refusal is a 400 with a ProblemDetails body. */
  claimGamertag(dto: SubmitGamertagClaimDto): Observable<GamertagClaimDto> {
    return this.http.post<GamertagClaimDto>('/api/me/gamertag-claims', dto);
  }

  /** The signed-in user's claims of past gamertags. */
  getMyGamertagClaims(): Observable<GamertagClaimDto[]> {
    return this.http.get<GamertagClaimDto[]>('/api/me/gamertag-claims');
  }

  /** Sets the signed-in user's region (null clears it). */
  setMyRegion(regionID: number | null): Observable<void> {
    return this.http.post<void>('/api/me/region', { regionID });
  }

  /** Records a gamertag an Xbox account went by. */
  addGamertagHistory(dto: AddGamertagHistoryDto): Observable<void> {
    return this.http.post<void>('/api/GamertagClaims/AddGamertagHistory', dto);
  }

  /** Removes a row from an Xbox account's gamertag history; a refusal (not removable) is a 400 with a message. */
  removeGamertagHistory(xboxGamertagHistoryID: number): Observable<void> {
    return this.http.delete<void>('/api/GamertagClaims/RemoveGamertagHistory/' + xboxGamertagHistoryID);
  }

  /** Queues the background lookup of placeholders' current Xbox gamertags; answers 202 with the status right away. */
  lookUpXuids(): Observable<XuidLookupStatusDto> {
    return this.http.post<XuidLookupStatusDto>('/api/GamertagClaims/LookUpXuids', null);
  }

  /** The historical archive's sources, with their last download. */
  getHistoricalSources(): Observable<HistoricalSourceDto[]> {
    return this.http.get<HistoricalSourceDto[]>('/api/HistoricalImport/Sources');
  }

  /** A disabled source is no longer downloaded (the import keeps its last snapshot). */
  setHistoricalSourceEnabled(sourceID: string, enabled: boolean): Observable<void> {
    return this.http.post<void>('/api/HistoricalImport/SetSourceEnabled/' + encodeURIComponent(sourceID), { enabled });
  }

  /** Queues a download of every enabled source (no import). */
  refreshHistoricalSources(): Observable<HistoricalImportQueuedDto> {
    return this.http.post<HistoricalImportQueuedDto>('/api/HistoricalImport/Refresh', null);
  }

  /** Queues a dry run of the import: its job lists what each season would lose. */
  previewHistoricalImport(): Observable<HistoricalImportQueuedDto> {
    return this.http.post<HistoricalImportQueuedDto>('/api/HistoricalImport/Preview', null);
  }

  /** Queues the import of what a finished preview read. */
  applyHistoricalImportPreview(jobID: number): Observable<HistoricalImportQueuedDto> {
    return this.http.post<HistoricalImportQueuedDto>('/api/HistoricalImport/ApplyPreview/' + jobID, null);
  }

  /** Imports a season the guardrail stopped. */
  approveHistoricalSeasonReview(reviewID: number): Observable<HistoricalImportQueuedDto> {
    return this.http.post<HistoricalImportQueuedDto>('/api/HistoricalImport/ApproveReview/' + reviewID, null);
  }

  /** Leaves a season the guardrail stopped as it is. */
  skipHistoricalSeasonReview(reviewID: number): Observable<void> {
    return this.http.post<void>('/api/HistoricalImport/SkipReview/' + reviewID, null);
  }

  /** The latest historical import jobs, newest first. */
  getHistoricalImportJobs(): Observable<HistoricalImportJobSummaryDto[]> {
    return this.http.get<HistoricalImportJobSummaryDto[]>('/api/HistoricalImport/Jobs');
  }

  /** A historical import job: its progress, report and season reviews. */
  getHistoricalImportJob(jobID: number): Observable<HistoricalImportJobDto> {
    return this.http.get<HistoricalImportJobDto>('/api/HistoricalImport/Job/' + jobID);
  }

  getHistoricalImportSchedule(): Observable<HistoricalImportScheduleDto> {
    return this.http.get<HistoricalImportScheduleDto>('/api/HistoricalImport/Schedule');
  }

  /** Paused, the scheduled refresh queues nothing (manual runs still work). */
  setHistoricalImportSchedulePaused(paused: boolean): Observable<HistoricalImportScheduleDto> {
    return this.http.post<HistoricalImportScheduleDto>('/api/HistoricalImport/SetSchedulePaused', { paused });
  }

  /** The background jobs, their schedules and last runs, and the schedulers checked in (sysadmins). */
  getJobs(): Observable<JobsOverviewDto> {
    return this.http.get<JobsOverviewDto>('/api/Jobs/List');
  }

  /** A job's latest runs, newest first. */
  getJobHistory(job: JobKeyDto, take = 50): Observable<JobRunDto[]> {
    const params = new HttpParams().set('group', job.group).set('name', job.name).set('take', take);
    return this.http.get<JobRunDto[]>('/api/Jobs/History', { params });
  }

  /** Runs a job now, on the worker. */
  runJob(job: JobKeyDto): Observable<void> {
    return this.http.post<void>('/api/Jobs/Run', { group: job.group, name: job.name });
  }

  setJobPaused(job: JobKeyDto, paused: boolean): Observable<void> {
    return this.http.post<void>('/api/Jobs/SetPaused', { group: job.group, name: job.name, paused });
  }

  /** A recurring job runs every intervalSeconds from now on. */
  setJobInterval(job: JobKeyDto, intervalSeconds: number): Observable<void> {
    return this.http.post<void>('/api/Jobs/SetInterval', { group: job.group, name: job.name, intervalSeconds });
  }

  /** A recurring job back on its default schedule. */
  resetJobSchedule(job: JobKeyDto): Observable<void> {
    return this.http.post<void>('/api/Jobs/ResetSchedule', { group: job.group, name: job.name });
  }

  /** Asks a running job to stop. */
  interruptJob(fireInstanceID: string): Observable<void> {
    return this.http.post<void>('/api/Jobs/Interrupt', { fireInstanceID });
  }

  /** Which of the Telemetry page's backends are configured, and the service they are scoped to (sysadmins). */
  getTelemetryStatus(): Observable<TelemetryStatusDto> {
    return this.http.get<TelemetryStatusDto>('/api/Telemetry/Status');
  }

  /** The Overview's charts and busiest routes over the last minutes. */
  getTelemetryMetrics(minutes: number): Observable<MetricsDto> {
    return this.http.get<MetricsDto>('/api/Telemetry/Metrics', { params: new HttpParams().set('minutes', minutes) });
  }

  /** Log lines, newest first: some levels only (none: every level), lines containing a text, one trace's. */
  getTelemetryLogs(query: LogsQuery): Observable<LogsDto> {
    let params = new HttpParams().set('minutes', query.minutes).set('limit', query.limit);
    for (const level of query.levels)
      params = params.append('level', level);
    if (query.search.trim())
      params = params.set('search', query.search.trim());
    if (query.traceId.trim())
      params = params.set('traceId', query.traceId.trim());
    return this.http.get<LogsDto>('/api/Telemetry/Logs', { params });
  }

  /** Traces over the last minutes, newest first: failed ones only, slower than a minimum. */
  getTelemetryTraces(query: TracesQuery): Observable<TraceSummaryDto[]> {
    let params = new HttpParams().set('minutes', query.minutes).set('errorsOnly', query.errorsOnly);
    if (query.minDurationMs)
      params = params.set('minDurationMs', query.minDurationMs);
    return this.http.get<TraceSummaryDto[]>('/api/Telemetry/Traces', { params });
  }

  /** One trace's spans from this app; 404 when it has none. */
  getTelemetryTrace(id: string): Observable<TraceDto> {
    return this.http.get<TraceDto>('/api/Telemetry/Trace', { params: new HttpParams().set('id', id) });
  }

  /** The VODs linked to a season match, season, team, or a player's teams; newest first. */
  getVods(scope: VodScope, id: number): Observable<VodDto[]> {
    return this.http.get<VodDto[]>(`/api/${VOD_OWNERS[scope]}/${id}/vods`);
  }

  /** The VODs the matcher wasn't sure of (sysadmins). */
  getVodReview(): Observable<VodReviewDto[]> {
    return this.http.get<VodReviewDto[]>('/api/vods/review');
  }

  getVodLinkSeasons(): Observable<VodLinkSeasonDto[]> {
    return this.http.get<VodLinkSeasonDto[]>('/api/vods/link-seasons');
  }

  /** A season's teams and matches, to link a VOD to. */
  getVodLinkOptions(seasonID: number): Observable<VodLinkOptionsDto> {
    return this.http.get<VodLinkOptionsDto>(`/api/seasons/${seasonID}/vod-link-options`);
  }

  /** Links a VOD as the sysadmin says: no refresh changes it again. */
  linkVod(vodID: number, request: LinkVodRequest): Observable<void> {
    return this.http.post<void>(`/api/vods/${vodID}/link`, request);
  }

  /** Not a VOD of anything: hidden, and never suggested again. */
  dismissVod(vodID: number): Observable<void> {
    return this.http.post<void>(`/api/vods/${vodID}/dismiss`, null);
  }

  /** How the background XUID lookup is going (for the gamertag claims page). */
  getXuidLookupStatus(): Observable<XuidLookupStatusDto> {
    return this.http.get<XuidLookupStatusDto>('/api/GamertagClaims/XuidLookupStatus');
  }

  /** An Xbox account's current gamertag and history, by its XUID or a user's ID; null when there is no such account (404). */
  getPastGamertags(query: { xboxUserID?: number | null, userID?: number | null }): Observable<PastGamertagsDto | null> {
    let params = new HttpParams();
    if (query.xboxUserID != null)
      params = params.set('xboxUserID', query.xboxUserID);
    if (query.userID != null)
      params = params.set('userID', query.userID);
    return this.http.get<PastGamertagsDto>('/api/GamertagClaims/PastGamertags', { params }).pipe(nullWhenNotFound());
  }

  getCurrentSeasonID(): Observable<number> {
    return this.http.get<number>('/api/seasons/current');
  }

  /** Signups, drafts and seasons that have not ended yet (shared/seasonEvents.ts). */
  getCurrentAndFutureEvents(): Observable<SeasonEvent[]> {
    return this.http.get<SeasonEvent[]>('/api/events');
  }

  getSeasonName(seasonID: number): Observable<string | null> {
    return this.http.get<string>(`/api/seasons/${seasonID}/name`, this.text).pipe(nullWhenNotFound());
  }

  getSignups(seasonID: number): Observable<SignupResponseDto[]> {
    return this.http.get<SignupResponseDto[]>(`/api/seasons/${seasonID}/signups`);
  }

  getTimeslots(seasonID: number, offset: number): Observable<TimeslotDto[]> {
    let path = `/api/seasons/${seasonID}/signups/mine/timeslots`;
    path += '?offset=' + offset;
    return this.http.get<TimeslotDto[]>(path);
  }

  getSignup(seasonID: number, offset: number): Observable<SignupResponseDto> {
    let path = `/api/seasons/${seasonID}/signups/mine`;
    path += '?offset=' + offset;
    return this.http.get<SignupResponseDto>(path);
  }

  // The timeslots keep the API's time format: the availability table only formats copies for display.
  upsertSignup(signupDto: SignupRequestDto): Observable<Object> {
    return this.http.post(`/api/seasons/${signupDto.seasonID}/signups/mine`, signupDto);
  }

  getTeams(seasonID: number): Observable<TeamResponseDto[]> {
    return this.http.get<TeamResponseDto[]>(`/api/seasons/${seasonID}/draft/teams`);
  }

  getPlayerPool(seasonID: number): Observable<PlayerDto[]> {
    return this.http.get<PlayerDto[]>(`/api/seasons/${seasonID}/draft/player-pool`);
  }

  // The hub connection's id is null (or undefined/empty) until it has connected: send no header then.
  private prepareHeaders(connectionID: string | null | undefined) : Object | undefined {
    if (!connectionID)
      return undefined;
    let headers = new HttpHeaders();
    headers = headers.append('SignalRConnectionID', connectionID);

    const obj: Object = {
      headers: headers
    };
    return obj;
  }

  /** Answers with the name the server gave the new team (it is not sent back over SignalR). */
  addCaptain(dto: CaptainPlacementDto, connectionID: string | null): Observable<CaptainAddedResponseDto> {
    return this.http.post<CaptainAddedResponseDto>(`/api/seasons/${dto.seasonID}/draft/captains`, dto, this.prepareHeaders(connectionID));
  }

  resortCaptain(dto: CaptainPlacementDto, connectionID: string | null): Observable<Object> {
    return this.http.post(`/api/seasons/${dto.seasonID}/draft/captains/resort`, dto, this.prepareHeaders(connectionID));
  }

  removeCaptain(dto: RemoveCaptainDto, connectionID: string | null): Observable<Object> {
    return this.http.post(`/api/seasons/${dto.seasonID}/draft/captains/remove`, dto, this.prepareHeaders(connectionID));
  }

  removePlayerFromTeam(dto: RemovePlayerFromTeamRequestDto, connectionID: string | null): Observable<Object> {
    return this.http.post(`/api/seasons/${dto.seasonID}/draft/players/remove`, dto, this.prepareHeaders(connectionID));
  }

  movePlayerToTeam(dto: MovePlayerToTeamRequestDto, connectionID: string | null): Observable<Object> {
    return this.http.post(`/api/seasons/${dto.seasonID}/draft/players/move`, dto, this.prepareHeaders(connectionID));
  }

  addPlayerToTeam(dto: AddPlayerToTeamRequestDto, connectionID: string | null): Observable<Object> {
    return this.http.post(`/api/seasons/${dto.seasonID}/draft/picks`, dto, this.prepareHeaders(connectionID));
  }

  lockCaptains(seasonID: number, connectionID: string | null): Observable<Object> {
    return this.http.post(`/api/seasons/${seasonID}/draft/captains/lock`, null, this.prepareHeaders(connectionID));
  }

  unlockCaptains(seasonID: number, connectionID: string | null): Observable<Object> {
    return this.http.post(`/api/seasons/${seasonID}/draft/captains/unlock`, null, this.prepareHeaders(connectionID));
  }

  areCaptainsLocked(seasonID: number): Observable<boolean> {
    return this.http.get<boolean>(`/api/seasons/${seasonID}/draft/captains/locked`);
  }

  commitHash(): Observable<string> {
    return this.http.get<string>('/api/CommitHash', this.text);
  }

  /** The build's date (ISO 8601 from the API) as a native Date, which the date pipe formats as it would a DateTime. */
  commitDate(): Observable<Date | null> {
    return this.http.get<string>('/api/CommitDate', this.text)
      .pipe(map(x => x ? new Date(x) : null));
  }
}
