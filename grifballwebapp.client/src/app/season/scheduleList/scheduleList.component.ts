import { CommonModule } from '@angular/common';
import { Component, OnInit, ChangeDetectionStrategy, inject, signal, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
import { parseScheduledMatch, ScheduledMatchDto } from './scheduledMatchDto';
import { DateTime } from 'luxon';
import { TimeDto } from './timeDto';
import { groupBy, map, orderBy } from 'lodash-es';
import { map as mapResponse } from 'rxjs';
import { WeekDto, WeekGameDto } from './weekDto';
import { CreateRegularMatchesDialogComponent } from './createRegularMatchesDialog/createRegularMatchesDialog.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { ToDatePipe } from '../../shared/toDate.pipe';
@Component({
    selector: 'app-schedule-list',
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
        ToDatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScheduleListComponent  implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  accountService = inject(AccountService);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);

  private seasonID : number = 0;
  readonly unscheduledMatches = signal<UnscheduledMatchDto[]>([]);
  readonly scheduledMatches = signal<WeekDto[]>([]);
  readonly isEditing = signal(false);

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

    dialogRef.componentInstance.regularMatchesCreated
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(() => {
      this.GetMatches();
    });
  }

  private GetMatches(): void {
    this.GetUnscheduledMatches();
    this.GetScheduledMatches();
  }

  GetUnscheduledMatches(): void {
    this.http.get<UnscheduledMatchDto[]>('/api/MatchPlanner/GetUnscheduledMatches/' + this.seasonID)
    .subscribe({
      next: r => this.unscheduledMatches.set(r),
    });
  }

  GetScheduledMatches(): void {
    this.http.get<unknown[]>('/api/MatchPlanner/GetScheduledMatches/' + this.seasonID)
    .pipe(mapResponse(matches => matches.map(parseScheduledMatch)))
    .subscribe({
      next: r => this.gotScheduledMatches(r),
    });
  }

  private gotScheduledMatches(matches: ScheduledMatchDto[]): void {
    const mappedMatches = matches.map(this.map);

    const grouped = groupBy(mappedMatches, (match) => match.LocalWeekYear + "-" + match.LocalWeekNumber);
    const mapped = map(grouped, (matches, key) => ({ key, matches }));
    const groupedByWeek = orderBy(mapped, group => Number(group.key), ['asc']);

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

    this.scheduledMatches.set(weeks);
  }

  private map(value: ScheduledMatchDto, index: number, array: ScheduledMatchDto[]): TimeDto {
    const dateTime = value.time;

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
    this.isEditing.update(editing => !editing);
  }

  /** `time` is the row's own form value; the loaded match objects are never edited. */
  onSubmit(match: UnscheduledMatchDto | WeekGameDto, time: DateTime | '' | null | undefined): void {
    // An empty picker gives '', which the server's DateTime? cannot bind: send null (no time).
    const dto: UpdateMatchTimeDto = { seasonMatchID: match.seasonMatchID, time: time || null };
    this.http.post('/api/MatchPlanner/UpdateMatchTime/', dto)
    .subscribe({
      next: r => this.GetMatches(),
      error: e => this.GetMatches()
    })
  }
}

