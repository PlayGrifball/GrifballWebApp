import { ChangeDetectionStrategy, Component, inject, model, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, Subject, switchMap, timer } from 'rxjs';
import { ApiClientService } from '../../api/apiClient.service';
import type { PlayerSearchResultDto } from '../../api/dtos/playerAllTimeRankDto';

/** How long typing pauses before the players are searched. */
export const SEARCH_DEBOUNCE_MS = 250;

/** The player search: a gamertag or past alias, with suggestions to pick from by mouse or keyboard. */
@Component({
  selector: 'app-player-search',
  templateUrl: './player-search.component.html',
  styleUrl: './player-search.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayerSearchComponent {
  private api = inject(ApiClientService);

  /** The text in the box. */
  readonly query = model('');
  /** A suggestion picked. */
  readonly picked = output<PlayerSearchResultDto>();
  /** Search pressed (or Enter with no suggestion chosen): the text to look up. */
  readonly submitted = output<string>();
  /** The box emptied with its clear button. */
  readonly cleared = output<void>();

  readonly results = signal<PlayerSearchResultDto[]>([]);
  readonly open = signal(false);
  /** The suggestion the arrow keys are on; -1: none. */
  readonly active = signal(-1);

  /**
   * Each new text cancels the pending search and any request in flight (switchMap), so a stale answer never lands after
   * a newer one, or after the list was closed. Short text (and a cancel: '') closes the list at once.
   */
  private readonly texts = new Subject<string>();

  constructor() {
    this.texts.pipe(
      switchMap(text => text.length < 2 ? of(null) : timer(SEARCH_DEBOUNCE_MS).pipe(
        switchMap(() => this.api.searchPlayers(text)),
        catchError(() => of([])),
      )),
      takeUntilDestroyed(),
    ).subscribe(results => {
      this.results.set(results ?? []);
      this.open.set(!!results?.length);
      this.active.set(-1);
    });
  }

  onInput(value: string): void {
    this.query.set(value);
    this.texts.next(value.trim());
  }

  onKeydown(event: KeyboardEvent): void {
    const count = this.results().length;
    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowUp':
        if (!count) return;
        event.preventDefault();
        this.open.set(true);
        this.active.update(i => event.key === 'ArrowDown' ? (i + 1) % count : (i <= 0 ? count - 1 : i - 1));
        return;
      case 'Enter': {
        event.preventDefault();
        const chosen = this.open() ? this.results()[this.active()] : undefined;
        if (chosen) this.pick(chosen);
        else this.submit();
        return;
      }
      case 'Escape':
        this.close();
        return;
    }
  }

  pick(player: PlayerSearchResultDto): void {
    this.cancel();
    this.query.set(player.gamertag);
    this.picked.emit(player);
  }

  submit(): void {
    const text = this.query().trim();
    if (!text) return;
    this.cancel();
    this.submitted.emit(text);
  }

  /** The clear button: empties the box and says so. */
  clear(): void {
    this.reset();
    this.cleared.emit();
  }

  /** Empties the box and drops the pending search, without saying so: for the page closing the standing itself. */
  reset(): void {
    this.cancel();
    this.query.set('');
  }

  /** Closes the list. On blur too: a suggestion is picked on mousedown, which comes before the blur. */
  close(): void {
    this.open.set(false);
    this.active.set(-1);
  }

  /** Drops the pending search and closes the list. */
  private cancel(): void {
    this.texts.next('');
  }
}
