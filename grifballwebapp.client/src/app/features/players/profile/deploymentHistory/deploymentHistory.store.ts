import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { DeploymentHistoryDto } from '@api/models';
import { DeploymentHistoryService } from '@api/services/deployment-history.service';
import { StoreInput } from '@shared/storeInput';

/** A player's deployment history: loaded for the player shown, and again on a retry. One per panel (the component provides it). */
@Injectable()
export class DeploymentHistoryStore {
  private readonly api = inject(DeploymentHistoryService);

  /** The player shown: the component's input. */
  readonly userID = new StoreInput<number>();

  private readonly resource = rxResource({
    params: () => this.userID.value(),
    stream: ({ params }) => this.api.getDeploymentHistory({ userID: params }),
  });

  /** Each season the player played, newest first; empty until loaded, or when it failed to. */
  readonly deployments = computed<DeploymentHistoryDto[]>(() => this.resource.hasValue() ? this.resource.value() ?? [] : []);
  readonly failed = computed(() => this.resource.error() !== undefined);

  /** Loads the history again (after it failed to). */
  reload(): void {
    this.resource.reload();
  }
}
