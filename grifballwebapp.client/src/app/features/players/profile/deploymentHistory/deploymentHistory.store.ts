import { inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { DeploymentHistoryDto } from '@api/models';
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
  }), [] as DeploymentHistoryDto[]);

  /** Each season the player played, newest first; empty until loaded, or when it failed to. */
  readonly deployments = this.resource.value;
  readonly failed = this.resource.failed;

  /** Loads the history again (after it failed to). */
  reload(): void {
    this.resource.reload();
  }
}
