import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { KillsDto } from './dtos/killsDto';
import { Observable } from 'rxjs';
import { LoginDto } from './dtos/loginDto';
import { RegisterDto } from './dtos/registerDto';
import { SeasonDto } from './dtos/seasonDto';
import { SignupResponseDto } from './dtos/signupResponseDto';
import { SignupRequestDto } from './dtos/signupRequestDto';

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

  login(loginDto: LoginDto): Observable<string> {
    const requestOptions: Object = {
      responseType: 'text'
    };
    return this.http.post<string>('/api/account/login', loginDto, requestOptions);
  }

  register(registerDto: RegisterDto) {
    return this.http.post('/api/account/register', registerDto);
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

  getSignup(seasonID: number, personID: number | null): Observable<SignupResponseDto> {
    let path = '/api/Signups/getSignup/' + seasonID;
    if (personID !== null)
      path += '?personID=' + personID;
    return this.http.get<SignupResponseDto>(path);
  }

  upsertSignup(signupDto: SignupRequestDto): Observable<Object> {
    return this.http.post('/api/Signups/UpsertSignup/', signupDto);
  }
}
