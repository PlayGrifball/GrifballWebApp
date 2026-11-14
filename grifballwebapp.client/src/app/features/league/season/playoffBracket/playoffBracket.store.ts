import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BracketsService } from '@api/services/brackets.service';
import { StoreInput } from '@shared/storeInput';
import { whileCurrent } from '@shared/whileCurrent';
import type { ViewerData } from 'brackets-viewer';

/** A season's playoff bracket, as brackets-viewer draws it. One per bracket (the component provides it). */
@Injectable()
export class PlayoffBracketStore {
  private readonly brackets = inject(BracketsService);
  private readonly snackBar = inject(MatSnackBar);

  /** The season page's season (0: none yet): the component's input. */
  readonly seasonID = new StoreInput<number>();

  /** The bracket; null until it arrives, and when the season has none (or it could not be loaded). */
  private readonly _viewerData = signal<ViewerData | null>(null);
  readonly viewerData = this._viewerData.asReadonly();
  /** The bracket (or that there is none) is known. */
  private readonly _loaded = signal(false);
  readonly loaded = this._loaded.asReadonly();

  constructor() {
    effect(() => {
      const seasonID = this.seasonID.value();
      if (seasonID === undefined)
        return;
      untracked(() => {
        // Nothing of the previous season's bracket stays while the new one loads
        this._viewerData.set(null);
        this._loaded.set(seasonID === 0);
        if (seasonID !== 0)
          this.reload();
      });
    });
  }

  /** Loads the bracket again (after it was created or reseeded). */
  reload(): void {
    const seasonID = this.seasonID.value();
    this.brackets.getViewerData({ seasonID: seasonID! })
      .pipe(whileCurrent(seasonID, this.seasonID.value))
      .subscribe({
        // brackets-model's data as the server sends it (Status as its number, as the generated Status says too): the two
        // types are declared apart, brackets-viewer's by brackets-model and this one generated from openapi.json.
        next: data => {
          this._viewerData.set(data as unknown as ViewerData);
          this._loaded.set(true);
        },
        error: e => {
          this._loaded.set(true);
          // 404: the season does not exist, so there is no bracket to show.
          if (e?.status === 404)
            return;
          console.log(e);
          this.snackBar.open('Failed to get viewer data', 'Close');
        },
      });
  }
}
