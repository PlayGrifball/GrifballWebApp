import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { ProfileCommendationsDto } from '@api/models';
import { ProfileService } from '@api/services/profile.service';
import { StoreInput } from '@shared/storeInput';

/** A player's commendations: loaded for the player shown, again for another. One per panel (the component provides it). */
@Injectable()
export class ProfileCommendationsStore {
  private readonly api = inject(ProfileService);

  /** The player shown: the component's input. */
  readonly userID = new StoreInput<number>();

  private readonly commendations = rxResource({
    params: () => this.userID.value(),
    stream: ({ params }) => this.api.getCommendations({ userID: params }),
  });

  readonly loading = computed(() => this.commendations.isLoading());
  /** The player's commendations; null until loaded, or when they failed to. */
  readonly data = computed<ProfileCommendationsDto | null>(() => this.commendations.hasValue() ? this.commendations.value() : null);
  readonly failed = computed(() => this.commendations.status() === 'error');
}
