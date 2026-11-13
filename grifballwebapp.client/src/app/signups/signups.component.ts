import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, numberAttribute, signal, untracked } from '@angular/core';
import { SeasonService } from '@api/services/season.service';
import { SignupsService } from '@api/services/signups.service';
import type { SignupResponseDto } from '@api/models';
import { RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { SeasonTabsComponent } from '../season/seasonTabs/seasonTabs.component';
import { DataNoticeComponent } from '../shared/dataNotice/dataNotice.component';
import { missing, SeasonSummaryService } from '../season/seasonSummary';
import type { SeasonSummaryDto } from '@api/models';
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
  private seasons = inject(SeasonService);
  private signups = inject(SignupsService);
  private summaries = inject(SeasonSummaryService);

  readonly signUps = signal<SignupResponseDto[]>([]);
  readonly summary = signal<SeasonSummaryDto | null>(null);
  /** A backfilled or ended season without signups: the page says so instead of an empty table. */
  readonly noSignups = computed(() => missing(this.summary(), s => s.signupCount));
  public displayedColumns: string[] = ['personName', 'timestamp', 'teamName', 'willCaptain', 'requiresAssistanceDrafting'];
  /** The :seasonID of the route (0: the current season). Angular keeps this page when only it changes, so a new one
   * loads that season's signups in place of the ones shown. */
  readonly seasonID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });
  /** The season shown: the route's, or the current season's ID once known. */
  readonly shownSeasonID = signal(0);

  constructor() {
    effect(() => {
      const routeSeasonID = this.seasonID();
      untracked(() => {
        this.signUps.set([]);
        this.summary.set(null);
        this.shownSeasonID.set(routeSeasonID);
        if (routeSeasonID === 0) {
          this.seasons.getCurrentSeasonId()
            .pipe(whileCurrent(routeSeasonID, this.seasonID))
            .subscribe({
              next: (result) => {
                this.shownSeasonID.set(result);
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
    const seasonID = this.shownSeasonID();
    this.summaries.get(seasonID).pipe(whileCurrent(seasonID, this.shownSeasonID)).subscribe({ next: s => this.summary.set(s), error: () => {} });
    this.signups.getSignups({ seasonID }).pipe(whileCurrent(seasonID, this.shownSeasonID)).subscribe({
      next: (result) => this.signUps.set(result),
      error: (error) => console.error(error),
    });
  }
}
