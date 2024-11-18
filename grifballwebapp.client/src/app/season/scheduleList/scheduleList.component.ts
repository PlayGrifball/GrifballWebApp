import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { UnscheduledMatchDto } from './unscheduledMatchDto';
import { MatDividerModule } from '@angular/material/divider';
import { AccountService } from '../../account.service';
import { ErrorMessageComponent } from "../../validation/errorMessage.component";
import { MatFormFieldModule } from '@angular/material/form-field';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { UpdateMatchTimeDto } from './updateMatchTimeDto';
import { ScheduledMatchDto } from './scheduledMatchDto';
import { DateTime, Settings } from 'luxon';
import { TimeDto } from './timeDto';
import { chain } from 'lodash-es';
import { WeekDto, WeekGameDto } from './weekDto';
import { CreateRegularMatchesDialogComponent } from './createRegularMatchesDialog/createRegularMatchesDialog.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

@Component({
    selector: 'app-schedule-list',
    standalone: true,
    templateUrl: './scheduleList.component.html',
    styleUrl: './scheduleList.component.scss',
    imports: [
        CommonModule,
        MatCardModule,
        MatDividerModule,
        ErrorMessageComponent,
        MatFormFieldModule,
        MtxDatetimepickerModule,
        MatInputModule,
        FormsModule,
        MatButtonModule,
        RouterModule,
        MatDialogModule,
    ]
})
export class ScheduleListComponent  implements OnInit {
  private seasonID : number = 0;
  unscheduledMatches: UnscheduledMatchDto[] = [];
  scheduledMatches: WeekDto[] = [];
  isEditing: boolean = false;

  constructor(private route: ActivatedRoute, private http: HttpClient, public accountService: AccountService, private dialog: MatDialog) {}

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    if (this.seasonID === 0) {
    }
    else {
      this.GetMatches();
    }
  }

  createRegularSeasonMatches() {
    const dialogRef = this.dialog.open(CreateRegularMatchesDialogComponent, {
      data: this.seasonID
    });

    const subcription = dialogRef.componentInstance.regularMatchesCreated.subscribe(() => {
      this.GetMatches();
    });

    dialogRef.afterClosed().subscribe(result => {
      subcription.unsubscribe();
    });
  }

  private GetMatches(): void {
    this.GetUnscheduledMatches();
    this.GetScheduledMatches();
  }

  GetUnscheduledMatches(): void {
    this.http.get<UnscheduledMatchDto[]>('/api/MatchPlanner/GetUnscheduledMatches/' + this.seasonID)
    .subscribe({
      next: r => this.unscheduledMatches = r,
    });
  }

  GetScheduledMatches(): void {
    this.http.get<ScheduledMatchDto[]>('/api/MatchPlanner/GetScheduledMatches/' + this.seasonID)
    .subscribe({
      next: r => this.gotScheduledMatches(r),
    });
  }

  private gotScheduledMatches(matches: ScheduledMatchDto[]): void {
    const mappedMatches = matches.map(this.map);

    const groupedByWeek = chain(mappedMatches)
      .groupBy((match) => match.LocalWeekYear + "-" + match.LocalWeekNumber)
      .map((matches, key) => ({ key, matches }))
      .orderBy(group => Number(group.key), ['asc'])
      .value()

    let weeks: WeekDto[] = [];

    let weekNumber = 1;
    groupedByWeek.forEach((x, index) => {
        const week = x.matches;

        const weekDto: WeekDto = {
          weekNumber: weekNumber++,
          games: week.map(this.mapTimeDtoToWeekGameDto)//.sort()
        };
        weeks.push(weekDto);
    });

    this.scheduledMatches = weeks;
  }

  private map(value: ScheduledMatchDto, index: number, array: ScheduledMatchDto[]): TimeDto {
    const timeString = value.time;
    var dateTime = DateTime.fromISO(timeString.toString());

    let weekYear = dateTime.localWeekYear;
    let weekNumber = dateTime.localWeekNumber;

    return {
      SeasonMatchID: value.seasonMatchID,
      HomeCaptainName: value.homeCaptain,
      AwayCaptainName: value.awayCaptain,
      Complete: value.complete,
      Time: value.time,
      DateTime: dateTime,
      LocalWeekNumber: dateTime.localWeekNumber,
      LocalWeekYear: dateTime.localWeekYear,
    }
  }

  private mapTimeDtoToWeekGameDto(value: TimeDto, index: number, array: TimeDto[]): WeekGameDto {
    return {
      seasonMatchID: value.SeasonMatchID,
      homeCaptainName: value.HomeCaptainName,
      awayCaptainName: value.AwayCaptainName,
      complete: value.Complete,
      time: value.DateTime,
    }
  }

  toggleEdit(): void {
    this.isEditing = !this.isEditing;
  }

  onSubmit(match: UnscheduledMatchDto | WeekGameDto): void {
    const dto: UpdateMatchTimeDto = match;
    this.http.post('/api/MatchPlanner/UpdateMatchTime/', dto)
    .subscribe({
      next: r => this.GetMatches(),
      error: e => this.GetMatches()
    })
  }
}

