import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { REGIONS } from '@shared/regions';
import { SeasonLinksComponent } from '@shared/seasonLinks/seasonLinks.component';
import { ProfileIdentityStore } from './profileIdentity.store';

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
  providers: [ProfileIdentityStore],
})
export class ProfileIdentityComponent {
  private readonly store = inject(ProfileIdentityStore);

  /** The user's region now (from their profile). */
  readonly regionID = input<number | null>(null);
  /** Something on the profile changed (the region): the page reloads it. */
  readonly changed = output<void>();

  readonly regions = REGIONS;
  readonly savingRegion = this.store.savingRegion;

  setRegion(value: string): void {
    this.store.setRegion(value ? Number(value) : null, () => this.changed.emit());
  }

  readonly gamertag = signal('');
  readonly firstSeen = signal('');
  readonly lastSeen = signal('');
  readonly preview = this.store.preview;
  readonly previewing = this.store.previewing;
  readonly submitting = this.store.submitting;

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
    if (gamertag)
      this.store.previewClaim(gamertag);
  }

  submit(): void {
    if (!this.canSubmit())
      return;
    this.store.claim({ gamertag: this.gamertag().trim(), firstSeen: apiDay(this.firstSeen()), lastSeen: apiDay(this.lastSeen()) }, () => {
      this.gamertag.set('');
      this.firstSeen.set('');
      this.lastSeen.set('');
    });
  }

  readonly claimsLoaded = this.store.claimsLoaded;
  readonly claimRows = this.store.claimRows;
  readonly claimsFailed = this.store.claimsFailed;
}
