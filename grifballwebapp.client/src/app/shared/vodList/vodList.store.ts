import { inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { VodDto } from '@api/models';
import { VodService } from '@api/services/vod.service';
import { StoreInput } from '../storeInput';
import { getVods, type VodScope } from './vods';
import { resourceView } from '../resourceView';

/** A page's VODs: a season match's, season's, team's or player's. One per list (the component provides it). */
@Injectable()
export class VodListStore {
  private readonly api = inject(VodService);

  /** What the VODs are of: the component's inputs. */
  readonly scope = new StoreInput<VodScope>();
  readonly id = new StoreInput<number>();

  private readonly resource = resourceView(rxResource({
    params: () => {
      const scope = this.scope.value();
      const id = this.id.value();
      return scope === undefined || id === undefined ? undefined : { scope, id };
    },
    stream: ({ params }) => getVods(this.api, params.scope, params.id),
  }), [] as VodDto[]);
  /** The VODs, newest first; none while they load, or when they could not be. */
  readonly vods = this.resource.value;
}
