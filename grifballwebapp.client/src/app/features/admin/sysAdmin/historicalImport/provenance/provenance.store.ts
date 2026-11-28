import { inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { HistoricalImportService } from '@api/services/historical-import.service';
import { StoreInput } from '@shared/storeInput';
import { resourceView } from '@shared/resourceView';

/** A lookup: an entity's type and key (User 4182). */
export interface ProvenanceQuery {
  entity: string;
  key: string;
}

/** Where the historical import got one row from: its provenance rows, once a lookup is asked. One per panel. */
@Injectable()
export class ProvenanceStore {
  private readonly api = inject(HistoricalImportService);

  /** The lookup asked (null: none yet): the component's. */
  readonly query = new StoreInput<ProvenanceQuery | null>();

  private readonly lookup = resourceView(rxResource({
    params: () => this.query.value() ?? undefined,
    stream: ({ params }) => this.api.getHistoricalProvenance({ entity: params.entity, key: params.key }),
  }), []);
  readonly rows = this.lookup.value;
  readonly loaded = this.lookup.hasValue;
  readonly failed = this.lookup.failed;
}
