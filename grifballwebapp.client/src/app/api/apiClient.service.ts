import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { KillsDto } from './dtos/killsDto';
import { Observable } from 'rxjs';
import { LoginDto } from './dtos/loginDto';
import { RegisterDto } from './dtos/registerDto';
import { SeasonDto } from './dtos/seasonDto';

@Injectable({
  providedIn: 'root'
})
export class ApiClientService {

  constructor(private http: HttpClient) { }


  getKills() : Observable<KillsDto[]> {
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
}
