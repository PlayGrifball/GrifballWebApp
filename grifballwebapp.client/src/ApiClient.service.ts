import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { KillsDto } from './app/top-stats/killsDto';
import { Observable } from 'rxjs';
import { LoginDto } from './app/login/loginDto';

@Injectable({
  providedIn: 'root'
})
export class ApiClientService {

  constructor(private http: HttpClient) { }


  getKills() : Observable<KillsDto[]> {
    return this.http.get<KillsDto[]>('/api/stats/TopKills');
  }

  login(loginDto: LoginDto) {
    return this.http.post('/api/account/login', loginDto);
  }

  register(loginDto: LoginDto) {
    return this.http.post('/api/account/register', loginDto);
  }
}
