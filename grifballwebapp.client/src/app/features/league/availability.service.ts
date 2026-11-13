import { Injectable } from '@angular/core';
import { DateTime } from 'luxon';
import { LEAGUE_TIME_ZONE } from '@shared/leagueTimeZone';

@Injectable({
  providedIn: 'root'
})
export class AvailabilityService {

  DayOfWeek: string = 'Day Of Week';

  constructor() { }

  getDif(): number {
    const leagueOffset = DateTime.now().setZone(LEAGUE_TIME_ZONE).offset;
    const localOffset = DateTime.now().offset;
    const dif = localOffset - leagueOffset;
    return dif;
  }

  parseTime(time: string): string {
    if (time === this.DayOfWeek)
      return time;
    let dateTime = DateTime.fromFormat(time, 'TT');
    return dateTime.toFormat('t');
  }
}
