import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MyProfileService } from '@api/services';
import { ClaimPreviewDto, GamertagClaimDto } from '@api/models';
import { problemText } from '../../shared/errorText';
import { REGIONS } from '../../shared/regions';
import { SeasonLinksComponent } from '../../shared/seasonLinks/seasonLinks.component';

/** A date input's "yyyy-MM-dd" as the API takes it (midnight UTC), or null when empty. */
function apiDay(value: string): string | null {
  return value ? `${value}T00:00:00Z` : null;
}

/**
 * The signed-in user's own identity settings on their profile: their region, and claims of past gamertags (the
 * placeholder of a gamertag they played under, which a sysadmin approves), with each claim's status.
 */
@Component({
  selector: 'app-profile-identity',
  imports: [DatePipe, SeasonLinksComponent],
  templateUrl: './profileIdentity.component.html',
  styleUrl: './profileIdentity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileIdentityComponent {
  private api = inject(MyProfileService);
  private snackBar = inject(MatSnackBar);

  /** The user's region now (from their profile). */
  readonly regionID = input<number | null>(null);
  /** Something on the profile changed (the region): the page reloads it. */
  readonly changed = output<void>();

  readonly regions = REGIONS;
  readonly savingRegion = signal(false);

  setRegion(value: string): void {
    const regionID = value ? Number(value) : null;
    this.savingRegion.set(true);
    this.api.setMyRegion({ body: { regionID } }).subscribe({
      next: () => {
        this.savingRegion.set(false);
        this.snackBar.open(regionID ? 'Region saved.' : 'Region cleared.', 'Close');
        this.changed.emit();
      },
      error: (error: HttpErrorResponse) => {
        this.savingRegion.set(false);
        this.snackBar.open(problemText(error, "Couldn't save your region."), 'Close');
      },
    });
  }

  readonly gamertag = signal('');
  readonly firstSeen = signal('');
  readonly lastSeen = signal('');
  readonly preview = signal<ClaimPreviewDto | null>(null);
  readonly previewing = signal(false);
  readonly submitting = signal(false);

  /** A preview of what is typed now that found something claimable. */
  readonly canSubmit = computed(() => {
    const preview = this.preview();
    return !!preview && !preview.refusal && preview.gamertag.toLowerCase() === this.gamertag().trim().replace(/\s+/g, ' ').toLowerCase()
      && !this.submitting();
  });

  setGamertag(value: string): void {
    this.gamertag.set(value);
  }

  previewClaim(): void {
    const gamertag = this.gamertag().trim();
    if (!gamertag)
      return;
    this.previewing.set(true);
    this.api.previewGamertagClaim({ gamertag }).subscribe({
      next: preview => {
        this.previewing.set(false);
        this.preview.set(preview);
      },
      error: (error: HttpErrorResponse) => {
        this.previewing.set(false);
        this.snackBar.open(problemText(error, "Couldn't look that gamertag up."), 'Close');
      },
    });
  }

  submit(): void {
    if (!this.canSubmit())
      return;
    this.submitting.set(true);
    this.api.claimGamertag({ body: { gamertag: this.gamertag().trim(), firstSeen: apiDay(this.firstSeen()), lastSeen: apiDay(this.lastSeen()) } }).subscribe({
      next: claim => {
        this.submitting.set(false);
        this.snackBar.open(`Your claim to ${claim.gamertag} is waiting for a sysadmin.`, 'Close');
        this.gamertag.set('');
        this.firstSeen.set('');
        this.lastSeen.set('');
        this.preview.set(null);
        this.claims.reload();
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.snackBar.open(problemText(error, "Couldn't send the claim."), 'Close');
      },
    });
  }

  readonly claims = rxResource({
    params: () => true,
    stream: () => this.api.getMyGamertagClaims(),
  });
  readonly claimRows = computed<GamertagClaimDto[]>(() => this.claims.hasValue() ? this.claims.value() : []);
  readonly claimsFailed = computed(() => this.claims.status() === 'error');
}
