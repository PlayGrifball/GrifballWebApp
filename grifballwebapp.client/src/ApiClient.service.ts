import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { KillsDto } from './app/top-stats/killsDto';
import { Observable } from 'rxjs';
import { LoginDto } from './app/login/loginDto';
import { RegisterDto } from './app/dtos/registerDto';

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
}
