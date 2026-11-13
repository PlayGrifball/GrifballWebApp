import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable } from 'rxjs';
import { VodService } from '@api/services/vod.service';
import type { LinkVodRequest, VodLinkOptionsDto, VodLinkSeasonDto, VodReviewDto } from '@api/models';
import { problemText } from '../../../shared/errorText';

/** What the matcher suggested for a VOD, "Spring League 2024 · Alpha, Bravo · its match", or null for nothing. */
export function suggestion(vod: VodReviewDto): string | null {
  if (vod.seasonID == null)
    return null;
  return [vod.seasonName, vod.teams.map(t => t.teamName).join(', '), vod.seasonMatchID != null ? 'their match' : null]
    .filter(part => !!part).join(' · ');
}

/**
 * The VODs the matcher wasn't sure of (sysadmins, on the historical import page): accept its suggestion, link one to a
 * season, its match and teams, or dismiss it for good. The refresh that finds them is the import's own.
 */
@Component({
  selector: 'app-vod-review',
  imports: [DatePipe, MatCheckboxModule],
  templateUrl: './vodReview.component.html',
  styleUrl: './vodReview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VodReviewComponent {
  private api = inject(VodService);
  private snackBar = inject(MatSnackBar);

  readonly suggestion = suggestion;

  readonly review = rxResource({ params: () => true, stream: () => this.api.getVodReview() });
  readonly rows = computed<VodReviewDto[]>(() => this.review.hasValue() ? this.review.value() : []);

  /** The VOD being linked by hand, and what it is being linked to. */
  readonly editing = signal<VodReviewDto | null>(null);
  readonly seasonID = signal<number | null>(null);
  readonly seasonMatchID = signal<number | null>(null);
  readonly teamIDs = signal<number[]>([]);

  /** Loaded with the first editor opened. */
  readonly seasons = rxResource({ params: () => this.editing() ? true : undefined, stream: () => this.api.getVodLinkSeasons() });
  readonly seasonRows = computed<VodLinkSeasonDto[]>(() => this.seasons.hasValue() ? this.seasons.value() : []);
  readonly options = rxResource({ params: () => this.seasonID() ?? undefined, stream: ({ params }) => this.api.getVodLinkOptions({ seasonID: params }) });
  readonly optionsValue = computed<VodLinkOptionsDto | null>(() => this.options.hasValue() ? this.options.value() : null);

  /** An action waiting for its answer: the buttons wait too. */
  readonly busy = signal(false);

  watchUrl(vod: VodReviewDto): string {
    return 'https://www.youtube.com/watch?v=' + encodeURIComponent(vod.youTubeVideoID);
  }

  accept(vod: VodReviewDto): void {
    this.link(vod, { seasonID: vod.seasonID ?? null, seasonMatchID: vod.seasonMatchID ?? null, teamIDs: vod.teams.map(t => t.teamID) });
  }

  edit(vod: VodReviewDto): void {
    this.editing.set(vod);
    this.seasonID.set(vod.seasonID ?? null);
    this.seasonMatchID.set(vod.seasonMatchID ?? null);
    this.teamIDs.set(vod.teams.map(t => t.teamID));
  }

  cancel(): void {
    this.editing.set(null);
  }

  chooseSeason(value: string): void {
    this.seasonID.set(value ? Number(value) : null);
    this.seasonMatchID.set(null);
    this.teamIDs.set([]);
  }

  /** A match brings its two teams. */
  chooseMatch(value: string): void {
    const id = value ? Number(value) : null;
    this.seasonMatchID.set(id);
    const match = this.optionsValue()?.matches.find(m => m.seasonMatchID === id);
    if (match)
      this.teamIDs.set([...match.teamIDs]);
  }

  toggleTeam(teamID: number, checked: boolean): void {
    this.teamIDs.update(ids => checked ? [...ids.filter(id => id !== teamID), teamID] : ids.filter(id => id !== teamID));
  }

  save(): void {
    const vod = this.editing();
    if (vod && this.seasonID() != null)
      this.link(vod, { seasonID: this.seasonID(), seasonMatchID: this.seasonMatchID(), teamIDs: this.teamIDs() });
  }

  dismiss(vod: VodReviewDto): void {
    this.run(this.api.dismissVod({ vodID: vod.vodID }), "Couldn't dismiss the VOD.");
  }

  private link(vod: VodReviewDto, request: LinkVodRequest): void {
    this.run(this.api.linkVod({ vodID: vod.vodID, body: request }), "Couldn't link the VOD.");
  }

  /** Runs a decision; done, the VOD leaves the list. */
  private run(action: Observable<void>, failed: string): void {
    this.busy.set(true);
    action.subscribe({
      next: () => {
        this.busy.set(false);
        this.editing.set(null);
        this.review.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.snackBar.open(problemText(error, failed), 'Close');
      },
    });
  }
}
