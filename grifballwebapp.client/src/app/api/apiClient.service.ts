import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, map, Observable, of, switchMap, throwError } from 'rxjs';
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
import type { SeasonPlayerStatDto } from './dtos/seasonPlayerStatDto';
import type { SeasonListFilter, SeasonListItemDto } from './dtos/seasonListItemDto';
import type { LeaderboardsDto } from './dtos/leaderboardsDto';
import type { MatchmakingQueueDto, MatchmakingSeasonDetailDto, MatchmakingSeasonDto } from './dtos/lateLeagueDto';
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

/** The VodController action of each page's VODs. */
const VOD_ACTIONS: Record<VodScope, string> = { seasonMatch: 'ForSeasonMatch', season: 'ForSeason', team: 'ForTeam', player: 'ForPlayer' };

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
    return this.http.get<unknown[]>('/api/EventOrganizer/GetSeasons')
      // A list never holds nulls (List<SeasonDto> on the server).
      .pipe(switchMap(async seasons => {
        const { parseSeason } = await import('./dtos/seasonDto');
        return seasons.map(season => parseSeason(season)!);
      }));
  }

  /** Null when there is no such season (the API answers 204). */
  getSeason(seasonID: number): Observable<SeasonDto | null> {
    return this.http.get<unknown>('/api/EventOrganizer/GetSeason/' + seasonID)
      .pipe(switchMap(async season => (await import('./dtos/seasonDto')).parseSeason(season)));
  }

  upsertSeason(seasonDto: SeasonDto): Observable<number> {
    return this.http.post<number>('/api/EventOrganizer/UpsertSeason/', seasonDto);
  }

  /** The games a season can be played in, for the season editor. */
  getGameVersions(): Observable<GameVersionDto[]> {
    return this.http.get<GameVersionDto[]>('/api/EventOrganizer/GetGameVersions');
  }

  /** The leagues a season can belong to, for the season editor. */
  getLeagues(): Observable<LeagueDto[]> {
    return this.http.get<LeagueDto[]>('/api/EventOrganizer/GetLeagues');
  }

  /** A season's header, champions and how much of each kind of data it has. */
  getSeasonSummary(seasonID: number): Observable<SeasonSummaryDto> {
    return this.http.get<SeasonSummaryDto>(seasonSummaryUrl(seasonID));
  }

  /** The season's player stats; with a team, only that team's rows. */
  getSeasonPlayerStats(seasonID: number, teamID?: number | null): Observable<SeasonPlayerStatDto[]> {
    const params = teamID != null ? new HttpParams().set('teamID', teamID) : undefined;
    return this.http.get<SeasonPlayerStatDto[]>('/api/Season/GetSeasonPlayerStats/' + seasonID, { params });
  }

  /** Every season, the current ones included (the seasons page), of one league, game, format and year when given. */
  getSeasonList(filter: SeasonListFilter, pageSize: number): Observable<PaginationResult<SeasonListItemDto>> {
    let params = new HttpParams().set('pageSize', pageSize);
    if (filter.leagueID !== null) params = params.set('leagueID', filter.leagueID);
    if (filter.gameVersionID !== null) params = params.set('gameVersionID', filter.gameVersionID);
    if (filter.format) params = params.set('format', filter.format);
    if (filter.year != null) params = params.set('year', filter.year);
    return this.http.get<PaginationResult<SeasonListItemDto>>('/api/Season/GetSeasons', { params });
  }

  /** The all-time leaderboards: titles (of seasons of one format, or all), dynasties and career totals. */
  getLeaderboards(format: SeasonFormat | null = null): Observable<LeaderboardsDto> {
    const params = format ? new HttpParams().set('format', format) : undefined;
    return this.http.get<LeaderboardsDto>('/api/Leaderboards/GetLeaderboards', { params });
  }

  /** Late League as it is played now: the Discord queue's reported games and each player's totals. */
  getLateLeagueQueue(): Observable<MatchmakingQueueDto> {
    return this.http.get<MatchmakingQueueDto>('/api/LateLeague/GetQueue');
  }

  /** Late League's seasons of the archive, latest first. */
  getLateLeagueSeasons(): Observable<MatchmakingSeasonDto[]> {
    return this.http.get<MatchmakingSeasonDto[]>('/api/LateLeague/GetSeasons');
  }

  /** A Late League matchmaking season and its players' stats. */
  getLateLeagueSeason(matchmakingSeasonID: number): Observable<MatchmakingSeasonDetailDto> {
    return this.http.get<MatchmakingSeasonDetailDto>(`/api/LateLeague/GetSeason/${matchmakingSeasonID}`);
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
    return this.http.get<ClaimPreviewDto>('/api/MyProfile/PreviewClaim', { params: new HttpParams().set('gamertag', gamertag) });
  }

  /** Claims a past gamertag for the signed-in user; a refusal is a 400 with a ProblemDetails body. */
  claimGamertag(dto: SubmitGamertagClaimDto): Observable<GamertagClaimDto> {
    return this.http.post<GamertagClaimDto>('/api/MyProfile/ClaimGamertag', dto);
  }

  /** The signed-in user's claims of past gamertags. */
  getMyGamertagClaims(): Observable<GamertagClaimDto[]> {
    return this.http.get<GamertagClaimDto[]>('/api/MyProfile/GamertagClaims');
  }

  /** Sets the signed-in user's region (null clears it). */
  setMyRegion(regionID: number | null): Observable<void> {
    return this.http.post<void>('/api/MyProfile/SetRegion', { regionID });
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
    return this.http.get<VodDto[]>(`/api/Vod/${VOD_ACTIONS[scope]}/${id}`);
  }

  /** The VODs the matcher wasn't sure of (sysadmins). */
  getVodReview(): Observable<VodReviewDto[]> {
    return this.http.get<VodReviewDto[]>('/api/Vod/Review');
  }

  getVodLinkSeasons(): Observable<VodLinkSeasonDto[]> {
    return this.http.get<VodLinkSeasonDto[]>('/api/Vod/LinkSeasons');
  }

  /** A season's teams and matches, to link a VOD to. */
  getVodLinkOptions(seasonID: number): Observable<VodLinkOptionsDto> {
    return this.http.get<VodLinkOptionsDto>('/api/Vod/LinkOptions/' + seasonID);
  }

  /** Links a VOD as the sysadmin says: no refresh changes it again. */
  linkVod(vodID: number, request: LinkVodRequest): Observable<void> {
    return this.http.post<void>('/api/Vod/Link/' + vodID, request);
  }

  /** Not a VOD of anything: hidden, and never suggested again. */
  dismissVod(vodID: number): Observable<void> {
    return this.http.post<void>('/api/Vod/Dismiss/' + vodID, null);
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
    return this.http.get<PastGamertagsDto>('/api/GamertagClaims/PastGamertags', { params }).pipe(
      catchError((error: unknown) => error instanceof HttpErrorResponse && error.status === 404 ? of(null) : throwError(() => error)));
  }

  getCurrentSeasonID(): Observable<number> {
    return this.http.get<number>('/api/Season/getCurrentSeasonID/');
  }

  /** Signups, drafts and seasons that have not ended yet (shared/seasonEvents.ts). */
  getCurrentAndFutureEvents(): Observable<SeasonEvent[]> {
    return this.http.get<SeasonEvent[]>('/api/Home/CurrentAndFutureEvents/');
  }

  getSeasonName(seasonID: number): Observable<string | null> {
    return this.http.get<string | null>('/api/Season/getSeasonName/' + seasonID, this.text);
  }

  getSignups(seasonID: number): Observable<SignupResponseDto[]> {
    return this.http.get<SignupResponseDto[]>('/api/Signups/getSignups/' + seasonID);
  }

  getTimeslots(seasonID: number, offset: number): Observable<TimeslotDto[]> {
    let path = '/api/Signups/getTimeslots/' + seasonID;
    path += '?offset=' + offset;
    return this.http.get<TimeslotDto[]>(path);
  }

  getSignup(seasonID: number, offset: number): Observable<SignupResponseDto> {
    let path = '/api/Signups/getSignup/' + seasonID;
    path += '?offset=' + offset;
    return this.http.get<SignupResponseDto>(path);
  }

  // The timeslots keep the API's time format: the availability table only formats copies for display.
  upsertSignup(signupDto: SignupRequestDto): Observable<Object> {
    return this.http.post('/api/Signups/UpsertSignup/', signupDto);
  }

  getTeams(seasonID: number): Observable<TeamResponseDto[]> {
    return this.http.get<TeamResponseDto[]>('/api/Teams/GetTeams/' + seasonID);
  }

  getPlayerPool(seasonID: number): Observable<PlayerDto[]> {
    return this.http.get<PlayerDto[]>('/api/Teams/GetPlayerPool/' + seasonID);
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
    return this.http.post<CaptainAddedResponseDto>('/api/Teams/AddCaptain/', dto, this.prepareHeaders(connectionID));
  }

  resortCaptain(dto: CaptainPlacementDto, connectionID: string | null): Observable<Object> {
    return this.http.post('/api/Teams/ResortCaptain/', dto, this.prepareHeaders(connectionID));
  }

  removeCaptain(dto: RemoveCaptainDto, connectionID: string | null): Observable<Object> {
    return this.http.post('/api/Teams/RemoveCaptain/', dto, this.prepareHeaders(connectionID));
  }

  removePlayerFromTeam(dto: RemovePlayerFromTeamRequestDto, connectionID: string | null): Observable<Object> {
    return this.http.post('/api/Teams/removePlayerFromTeam/', dto, this.prepareHeaders(connectionID));
  }

  movePlayerToTeam(dto: MovePlayerToTeamRequestDto, connectionID: string | null): Observable<Object> {
    return this.http.post('/api/Teams/movePlayerToTeam/', dto, this.prepareHeaders(connectionID));
  }

  addPlayerToTeam(dto: AddPlayerToTeamRequestDto, connectionID: string | null): Observable<Object> {
    return this.http.post('/api/Teams/addPlayerToTeam/', dto, this.prepareHeaders(connectionID));
  }

  lockCaptains(seasonID: number, connectionID: string | null): Observable<Object> {
    return this.http.post('/api/Teams/lockCaptains/' + seasonID, null, this.prepareHeaders(connectionID));
  }

  unlockCaptains(seasonID: number, connectionID: string | null): Observable<Object> {
    return this.http.post('/api/Teams/unlockCaptains/' + seasonID, null, this.prepareHeaders(connectionID));
  }

  areCaptainsLocked(seasonID: number): Observable<boolean> {
    return this.http.get<boolean>('/api/Teams/areCaptainsLocked/' + seasonID);
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
