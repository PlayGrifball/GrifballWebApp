import { inject, Injectable, signal } from '@angular/core';
import type { SeasonSchedulingRulesDto } from '@api/models';
import { SchedulingService } from '@api/services/scheduling.service';
import { errorText } from '@shared/errorText';

/** A season's scheduling rules, as staff change them. One per editor (the component provides it). */
@Injectable()
export class SchedulingRulesStore {
  private readonly api = inject(SchedulingService);

  private readonly _rules = signal<SeasonSchedulingRulesDto | null>(null);
  /** The season's rules (the league's defaults until changed); null until loaded. */
  readonly rules = this._rules.asReadonly();
  /** The rules have arrived, or could not be read: a skeleton holds their place until then. */
  private readonly _loaded = signal(false);
  readonly loaded = this._loaded.asReadonly();
  private readonly _saving = signal(false);
  readonly saving = this._saving.asReadonly();
  /** The last save went through. */
  private readonly _saved = signal(false);
  readonly saved = this._saved.asReadonly();
  /** Why the server refused the rules. */
  private readonly _error = signal<string | null>(null);
  readonly error = this._error.asReadonly();

  load(seasonID: number): void {
    this._loaded.set(false);
    this.api.getSchedulingRules({ seasonID }).subscribe({
      next: rules => {
        this._rules.set(rules);
        this._loaded.set(true);
      },
      error: () => {
        this._rules.set(null);
        this._loaded.set(true);
      },
    });
  }

  save(seasonID: number, rules: SeasonSchedulingRulesDto): void {
    this._saving.set(true);
    this._saved.set(false);
    this._error.set(null);
    this.api.changeSchedulingRules({ seasonID, body: rules }).subscribe({
      next: () => {
        this._rules.set(rules);
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
