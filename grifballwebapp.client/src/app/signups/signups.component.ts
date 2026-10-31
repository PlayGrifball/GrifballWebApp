import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ApiClientService } from '../api/apiClient.service';
import { SignupResponseDto } from '../api/dtos/signupResponseDto';
import { ActivatedRoute } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { SeasonTabsComponent } from '../season/seasonTabs/seasonTabs.component';
import { DataNoticeComponent } from '../shared/dataNotice/dataNotice.component';
import { missing, SeasonSummaryService } from '../season/seasonSummary';
import type { SeasonSummaryDto } from '../api/dtos/seasonSummaryDto';

@Component({
    selector: 'app-signups',
    imports: [
        CommonModule,
        MatTableModule,
        SeasonTabsComponent,
        DataNoticeComponent,
    ],
    templateUrl: './signups.component.html',
    styleUrl: './signups.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SignupsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(ApiClientService);
  private summaries = inject(SeasonSummaryService);

  readonly signUps = signal<SignupResponseDto[]>([]);
  readonly summary = signal<SeasonSummaryDto | null>(null);
  /** A backfilled or ended season without signups: the page says so instead of an empty table. */
  readonly noSignups = computed(() => missing(this.summary(), s => s.signupCount));
  public displayedColumns: string[] = ['personName', 'timestamp', 'teamName', 'willCaptain', 'requiresAssistanceDrafting'];
  seasonID: number = 0;

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    if (this.seasonID === 0) {
      this.api.getCurrentSeasonID()
        .subscribe({
          next: (result) => {
            this.seasonID = result;
            this.getSignups();
          },
        });
    }
    else {
      this.getSignups();
    }
  }

  getSignups(): void {
    this.summaries.get(this.seasonID).subscribe({ next: s => this.summary.set(s), error: () => {} });
    this.api.getSignups(this.seasonID).subscribe({
      next: (result) => this.signUps.set(result),
      error: (error) => console.error(error),
    });
  }
}
