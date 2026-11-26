import { inject, Injectable, signal } from '@angular/core';
import type { SeasonSchedulingRulesDto } from '@api/models';
import { SchedulingService } from '@api/services/scheduling.service';
import { errorText } from '@shared/errorText';
import { rxResource } from '@angular/core/rxjs-interop';
import { resourceView } from '@shared/resourceView';
import { StoreInput } from '@shared/storeInput';

/** A season's scheduling rules, as staff change them. One per editor (the component provides it). */
@Injectable()
export class SchedulingRulesStore {
  private readonly api = inject(SchedulingService);

  /** The season (the component's input): another drops the request still on its way. */
  readonly seasonID = new StoreInput<number>();

  private readonly loaded = resourceView(rxResource({
    params: () => this.seasonID.value(),
    stream: ({ params: seasonID }) => this.api.getSchedulingRules({ seasonID }),
  }), null);
  /** The season's rules (the league's defaults until changed); null until loaded, or when they could not be. */
  readonly rules = this.loaded.value;
  /** The rules are on their way: a skeleton holds their place. */
  readonly firstLoad = this.loaded.firstLoad;
  /** They could not be read: the page says so. */
  readonly failed = this.loaded.failed;
  private readonly _saving = signal(false);
  readonly saving = this._saving.asReadonly();
  /** The last save went through. */
  private readonly _saved = signal(false);
  readonly saved = this._saved.asReadonly();
  /** Why the server refused the rules. */
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  save(seasonID: number, rules: SeasonSchedulingRulesDto): void {
    this._saving.set(true);
    this._saved.set(false);
    this._error.set(null);
    this.api.changeSchedulingRules({ seasonID, body: rules }).subscribe({
      next: () => {
        this.loaded.set(rules);
        this._saved.set(true);
        this._saving.set(false);
      },
      error: error => {
        this._error.set(errorText(error, 'The rules could not be saved. Try again.'));
        this._saving.set(false);
      },
    });
  }
}
