import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { KillsDto } from './dtos/killsDto';
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

@Injectable({
  providedIn: 'root'
})
export class ApiClientService {
  private http = inject(HttpClient);


  private readonly text : Object = {
    responseType: 'text'
  };


  getKills(): Observable<KillsDto[]> {
    return this.http.get<KillsDto[]>('/api/stats/TopKills');
  }

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
