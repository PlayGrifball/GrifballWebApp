import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { DeploymentHistoryService } from '@api/services/deployment-history.service';
import { StoreInput } from '@shared/storeInput';
import { resourceView } from '@shared/resourceView';
import { map } from 'rxjs';

/** A player's deployment history: loaded for the player shown, and again on a retry. One per panel (the component provides it). */
@Injectable()
export class DeploymentHistoryStore {
  private readonly api = inject(DeploymentHistoryService);

  /** The player shown: the component's input. */
  readonly userID = new StoreInput<number>();

  private readonly resource = resourceView(rxResource({
    params: () => this.userID.value(),
    // A null body is no history
    stream: ({ params }) => this.api.getDeploymentHistory({ userID: params }).pipe(map(history => history ?? [])),
  }), []);

  /** Each season the player played, newest first; empty until loaded, or when it failed to. */
  readonly deployments = this.resource.value;
  /** The last load failed, also while a retry is on its way: the error and its Retry stay up until it answers. */
  readonly failed = computed(() => this.resource.error() !== undefined);
  /** The history is on its way: the panel is a skeleton (most players have one, so it doesn't appear from nothing). */
  readonly firstLoad = this.resource.firstLoad;

  /** Loads the history again (after it failed to). */
  reload(): void {
    this.resource.reload();
  }
}
