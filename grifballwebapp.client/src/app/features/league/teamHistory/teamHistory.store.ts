import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { TeamService } from '@api/services/team.service';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { StoreInput } from '@shared/storeInput';

/** A franchise's history, found from any one of its teams. One per page (the component provides it). */
@Injectable()
export class TeamHistoryStore {
  private readonly api = inject(TeamService);

  /** One of the franchise's teams: the component's input. */
  readonly teamID = new StoreInput<number>();

  /** A new team cancels the request before it and clears what it showed. */
  private readonly resource = rxResource({
    params: () => this.teamID.value(),
    stream: ({ params: teamID }) => this.api.history({ teamID }).pipe(nullWhenNotFound()),
  });
  /** The franchise's history; null while loading, when there is no such team, or when it failed to load. */
  readonly history = computed(() => this.resource.hasValue() ? this.resource.value() ?? null : null);
  readonly loaded = computed(() => !this.resource.isLoading());
  readonly failed = computed(() => this.resource.status() === 'error');
}
