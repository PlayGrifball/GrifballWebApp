import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { LinkVodRequest, VodLinkOptionsDto, VodLinkSeasonDto, VodReviewDto } from '@api/models';
import { VodService } from '@api/services/vod.service';
import { problemText } from '@shared/errorText';
import { StoreInput } from '@shared/storeInput';
import { Observable } from 'rxjs';

/**
 * The VODs the matcher wasn't sure of, what one can be linked to (the seasons, and a season's matches and teams), and
 * linking or dismissing one. One per panel (the component provides it).
 */
@Injectable()
export class VodReviewStore {
  private readonly api = inject(VodService);
  private readonly snackBar = inject(MatSnackBar);

  /** Whether an editor is open (the seasons load with the first one): the component's. */
  readonly editing = new StoreInput<boolean>();
  /** The season chosen in the editor (null: none): the component's. */
  readonly seasonID = new StoreInput<number | null>();

  private readonly review = rxResource({ params: () => true, stream: () => this.api.getVodReview() });
  readonly rows = computed<VodReviewDto[]>(() => this.review.hasValue() ? this.review.value() : []);
  readonly loaded = computed(() => this.review.hasValue());
  readonly failed = computed(() => this.review.status() === 'error');

  private readonly seasons = rxResource({ params: () => this.editing.value() ? true : undefined, stream: () => this.api.getVodLinkSeasons() });
  readonly seasonRows = computed<VodLinkSeasonDto[]>(() => this.seasons.hasValue() ? this.seasons.value() : []);
  private readonly options = rxResource({ params: () => this.seasonID.value() ?? undefined, stream: ({ params }) => this.api.getVodLinkOptions({ seasonID: params }) });
  readonly optionsValue = computed<VodLinkOptionsDto | null>(() => this.options.hasValue() ? this.options.value() : null);

  /** An action waiting for its answer: the buttons wait too. */
  private readonly _busy = signal(false);
  readonly busy = this._busy.asReadonly();

  /** Links a VOD; `done` runs once it is (it leaves the list). */
  link(vodID: number, request: LinkVodRequest, done: () => void): void {
    this.run(this.api.linkVod({ vodID, body: request }), "Couldn't link the VOD.", done);
  }

  /** Dismisses a VOD for good; `done` runs once it is (it leaves the list). */
  dismiss(vodID: number, done: () => void): void {
    this.run(this.api.dismissVod({ vodID }), "Couldn't dismiss the VOD.", done);
  }

  /** Runs a decision; done, the list loads again. A refusal is said in a snackbar. */
  private run(action: Observable<void>, failed: string, done: () => void): void {
    this._busy.set(true);
    action.subscribe({
      next: () => {
        this._busy.set(false);
        done();
        this.review.reload();
      },
      error: (error: HttpErrorResponse) => {
        this._busy.set(false);
        this.snackBar.open(problemText(error, failed), 'Close');
      },
    });
  }
}
