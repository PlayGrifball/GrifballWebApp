import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, numberAttribute } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { SeasonTabsComponent } from '../season/seasonTabs/seasonTabs.component';
import { DataNoticeComponent } from '@shared/dataNotice/dataNotice.component';
import { missing } from '../season/seasonSummary';
import { SignupsStore } from './signups.store';

@Component({
    selector: 'app-signups',
    imports: [
        MatTableModule,
        SeasonTabsComponent,
        DataNoticeComponent,
        RouterLink,
        DatePipe,
    ],
    templateUrl: './signups.component.html',
    styleUrl: './signups.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [SignupsStore],
})
export class SignupsComponent {
  private readonly store = inject(SignupsStore);

  readonly signUps = this.store.signUps;
  readonly summary = this.store.summary;
  /** A backfilled or ended season without signups: the page says so instead of an empty table. */
  readonly noSignups = computed(() => missing(this.summary(), s => s.signupCount));
  public displayedColumns: string[] = ['personName', 'timestamp', 'teamName', 'willCaptain', 'requiresAssistanceDrafting'];
  /** The :seasonID of the route (0: the current season). Angular keeps this page when only it changes, so a new one
   * loads that season's signups in place of the ones shown. */
  readonly seasonID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });
  /** The season shown: the route's, or the current season's ID once known. */
  readonly shownSeasonID = this.store.shownSeasonID;

  constructor() {
    this.store.seasonID.use(this.seasonID);
  }
}
