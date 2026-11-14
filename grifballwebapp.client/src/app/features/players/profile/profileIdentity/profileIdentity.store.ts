import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { SubmitGamertagClaimRequest, ClaimPreviewDto, GamertagClaimDto } from '@api/models';
import { MyProfileService } from '@api/services/my-profile.service';
import { problemText } from '@shared/errorText';

/**
 * The signed-in user's identity settings: saving their region, and their gamertag claims (looking a gamertag up,
 * claiming it, and the list of claims). One per panel (the component provides it).
 */
@Injectable()
export class ProfileIdentityStore {
  private readonly api = inject(MyProfileService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly _savingRegion = signal(false);
  readonly savingRegion = this._savingRegion.asReadonly();

  /** Saves the user's region (null: none); `saved` runs once it is. */
  setRegion(regionID: number | null, saved: () => void): void {
    this._savingRegion.set(true);
    this.api.setMyRegion({ body: { regionID } }).subscribe({
      next: () => {
        this._savingRegion.set(false);
        this.snackBar.open(regionID ? 'Region saved.' : 'Region cleared.', 'Close');
        saved();
      },
      error: (error: HttpErrorResponse) => {
        this._savingRegion.set(false);
        this.snackBar.open(problemText(error, "Couldn't save your region."), 'Close');
      },
    });
  }

  /** What the gamertag last looked up would claim; null before one is, and once it is claimed. */
  private readonly _preview = signal<ClaimPreviewDto | null>(null);
  readonly preview = this._preview.asReadonly();
  private readonly _previewing = signal(false);
  readonly previewing = this._previewing.asReadonly();
  private readonly _submitting = signal(false);
  readonly submitting = this._submitting.asReadonly();

  /** Looks a gamertag up: what claiming it would give, or why it can't be claimed. */
  previewClaim(gamertag: string): void {
    this._previewing.set(true);
    this.api.previewGamertagClaim({ gamertag }).subscribe({
      next: preview => {
        this._previewing.set(false);
        this._preview.set(preview);
      },
      error: (error: HttpErrorResponse) => {
        this._previewing.set(false);
        this.snackBar.open(problemText(error, "Couldn't look that gamertag up."), 'Close');
      },
    });
  }

  /** Claims a gamertag; `claimed` runs once the claim is sent (the preview is dropped and the claims load again). */
  claim(body: SubmitGamertagClaimRequest, claimed: () => void): void {
    this._submitting.set(true);
    this.api.claimGamertag({ body }).subscribe({
      next: claim => {
        this._submitting.set(false);
        this.snackBar.open(`Your claim to ${claim.gamertag} is waiting for a sysadmin.`, 'Close');
        this._preview.set(null);
        this.claims.reload();
        claimed();
      },
      error: (error: HttpErrorResponse) => {
        this._submitting.set(false);
        this.snackBar.open(problemText(error, "Couldn't send the claim."), 'Close');
      },
    });
  }

  private readonly claims = rxResource({
    params: () => true,
    stream: () => this.api.getMyGamertagClaims(),
  });
  readonly claimsLoaded = computed(() => this.claims.hasValue());
  readonly claimRows = computed<GamertagClaimDto[]>(() => this.claims.hasValue() ? this.claims.value() : []);
  readonly claimsFailed = computed(() => this.claims.status() === 'error');
}
