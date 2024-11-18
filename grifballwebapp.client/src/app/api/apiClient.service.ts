import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { KillsDto } from './dtos/killsDto';
import { Observable } from 'rxjs';
import { SeasonDto } from './dtos/seasonDto';
import { SignupResponseDto, TimeslotDto } from './dtos/signupResponseDto';
import { SignupRequestDto } from './dtos/signupRequestDto';
import { TeamResponseDto } from './dtos/teamResponseDto';
import { PlayerDto } from './dtos/playerDto';
import { CaptainPlacementDto, RemoveCaptainDto } from './dtos/captainPlacementDto';
import { RemovePlayerFromTeamRequestDto } from './dtos/RemovePlayerFromTeamRequestDto';
import { MovePlayerToTeamRequestDto } from './dtos/MovePlayerToTeamRequestDto';
import { AddPlayerToTeamRequestDto } from './dtos/AddPlayerToTeamRequestDto';
import { DateTime } from 'luxon';
import { cloneDeep } from 'lodash-es';

@Injectable({
  providedIn: 'root'
})
export class ApiClientService {

  constructor(private http: HttpClient) { }

  private readonly text : Object = {
    responseType: 'text'
  };


  getKills(): Observable<KillsDto[]> {
    return this.http.get<KillsDto[]>('/api/stats/TopKills');
  }

  getSeasons(): Observable<SeasonDto[]> {
    return this.http.get<SeasonDto[]>('/api/EventOrganizer/GetSeasons');
  }

  getSeason(seasonID: number): Observable<SeasonDto> {
    return this.http.get<SeasonDto>('/api/EventOrganizer/GetSeason/' + seasonID);
  }

  upsertSeason(seasonDto: SeasonDto): Observable<number> {
    return this.http.post<number>('/api/EventOrganizer/UpsertSeason/', seasonDto);
  }

  getCurrentSeasonID(): Observable<number> {
    return this.http.get<number>('/api/Season/getCurrentSeasonID/');
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

  private parseTime(time: string): string {
    let dateTime = DateTime.fromFormat(time, 't');
    return dateTime.toFormat('TT');
  }

  upsertSignup(signupDto: SignupRequestDto): Observable<Object> {
    const clone = cloneDeep(signupDto);
    clone.timeslots.forEach(x => x.time = this.parseTime(x.time));
    return this.http.post('/api/Signups/UpsertSignup/', clone);
  }

  getTeams(seasonID: number): Observable<TeamResponseDto[]> {
    return this.http.get<TeamResponseDto[]>('/api/Teams/GetTeams/' + seasonID);
  }

  getPlayerPool(seasonID: number): Observable<PlayerDto[]> {
    return this.http.get<PlayerDto[]>('/api/Teams/GetPlayerPool/' + seasonID);
  }

  private prepareHeaders(connectionID: string | null) : Object | undefined {
    if (connectionID === null)
      return undefined;
    let headers = new HttpHeaders();
    headers = headers.append('SignalRConnectionID', connectionID);

    const obj: Object = {
      headers: headers
    };
    return obj;
  }

  addCaptain(dto: CaptainPlacementDto, connectionID: string | null): Observable<Object> {
    return this.http.post('/api/Teams/AddCaptain/', dto, this.prepareHeaders(connectionID));
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
    return this.http.get('/api/Teams/lockCaptains/' + seasonID, this.prepareHeaders(connectionID));
  }

  unlockCaptains(seasonID: number, connectionID: string | null): Observable<Object> {
    return this.http.get('/api/Teams/unlockCaptains/' + seasonID, this.prepareHeaders(connectionID));
  }

  areCaptainsLocked(seasonID: number): Observable<boolean> {
    return this.http.get<boolean>('/api/Teams/areCaptainsLocked/' + seasonID);
  }
}
