import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, numberAttribute, signal, untracked } from '@angular/core';
import { ApiClientService } from '../api/apiClient.service';
import { SignupResponseDto } from '../api/dtos/signupResponseDto';
import { RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { SeasonTabsComponent } from '../season/seasonTabs/seasonTabs.component';
import { DataNoticeComponent } from '../shared/dataNotice/dataNotice.component';
import { missing, SeasonSummaryService } from '../season/seasonSummary';
import type { SeasonSummaryDto } from '../api/dtos/seasonSummaryDto';
import { whileCurrent } from '../shared/whileCurrent';

@Component({
    selector: 'app-signups',
    imports: [
        CommonModule,
        MatTableModule,
        SeasonTabsComponent,
        DataNoticeComponent,
        RouterLink,
    ],
    templateUrl: './signups.component.html',
    styleUrl: './signups.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SignupsComponent {
  private api = inject(ApiClientService);
  private summaries = inject(SeasonSummaryService);

  readonly signUps = signal<SignupResponseDto[]>([]);
  readonly summary = signal<SeasonSummaryDto | null>(null);
  /** A backfilled or ended season without signups: the page says so instead of an empty table. */
  readonly noSignups = computed(() => missing(this.summary(), s => s.signupCount));
  public displayedColumns: string[] = ['personName', 'timestamp', 'teamName', 'willCaptain', 'requiresAssistanceDrafting'];
  /** The :seasonID of the route (0: the current season). Angular keeps this page when only it changes, so a new one
   * loads that season's signups in place of the ones shown. */
  readonly routeSeasonID = input.required({ alias: 'seasonID', transform: (value: unknown) => numberAttribute(value, 0) });
  /** The season shown: the route's, or the current season's ID once known. */
  readonly seasonID = signal(0);

  constructor() {
    effect(() => {
      const routeSeasonID = this.routeSeasonID();
      untracked(() => {
        this.signUps.set([]);
        this.summary.set(null);
        this.seasonID.set(routeSeasonID);
        if (routeSeasonID === 0) {
          this.api.getCurrentSeasonID()
            .pipe(whileCurrent(routeSeasonID, this.routeSeasonID))
            .subscribe({
              next: (result) => {
                this.seasonID.set(result);
                this.getSignups();
              },
            });
        }
        else {
          this.getSignups();
        }
      });
    });
  }

  getSignups(): void {
    const seasonID = this.seasonID();
    this.summaries.get(seasonID).pipe(whileCurrent(seasonID, this.seasonID)).subscribe({ next: s => this.summary.set(s), error: () => {} });
    this.api.getSignups(seasonID).pipe(whileCurrent(seasonID, this.seasonID)).subscribe({
      next: (result) => this.signUps.set(result),
      error: (error) => console.error(error),
    });
  }
}
