import { Injectable } from '@angular/core';
import { DateTime } from 'luxon';

@Injectable({
  providedIn: 'root'
})
export class AvailabilityService {

  DayOfWeek: string = 'Day Of Week';

  constructor() { }

  getDif(): number {
    const detroitOffset = DateTime.now().setZone('America/Detroit').offset;
    const localOffset = DateTime.now().offset;
    const dif = localOffset - detroitOffset;
    return dif;
  }

  parseTime(time: string): string {
    if (time === this.DayOfWeek)
      return time;
    let dateTime = DateTime.fromFormat(time, 'TT');
    return dateTime.toFormat('t');
  }
}
