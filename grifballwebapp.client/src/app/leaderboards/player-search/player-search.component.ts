import { ChangeDetectionStrategy, Component, inject, model, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, map, of, Subject, switchMap, timer } from 'rxjs';
import { ApiClientService } from '../../api/apiClient.service';
import { QuickSearchPlayerDto } from '@api/models';
import { playerDetail } from '../../quickSearch/playerDetail';
import { QUICK_SEARCH_MIN_LENGTH } from '../../quickSearch/quickSearch.component';

/** How long typing pauses before the players are searched. */
export const SEARCH_DEBOUNCE_MS = 250;

/**
 * Whether two names are the same as the server's StringComparison.OrdinalIgnoreCase has it: character by character, each
 * upper-cased on its own (so "ß" is not "SS"), with no accent, width or culture folding.
 */
export function sameIgnoringCase(a: string, b: string): boolean {
  const upper = (c: string) => {
    const u = c.toUpperCase();
    // A character whose capital is several ("ß" → "SS") stays itself, as a single character's mapping in .NET does
    return [...u].length === 1 ? u : c;
  };
  const xs = [...a];
  const ys = [...b];
  return xs.length === ys.length && xs.every((c, i) => c === ys[i] || upper(c) === upper(ys[i]));
}

/**
 * The one player the text points to among those found, as the server's lookup (LeaderboardsService.FindPlayer) decides,
 * with the same comparison: the one whose name, other name or past gamertag it is (ignoring case), else the only one
 * found; none when several could be meant.
 */
export function onlyMatch(found: QuickSearchPlayerDto[], text: string): QuickSearchPlayerDto | null {
  const is = (name: string | null | undefined) => !!name && sameIgnoringCase(name, text);
  const exact = found.filter(p => is(p.name) || is(p.matchedName) || is(p.pastGamertag));
  if (exact.length === 1)
    return exact[0];
  return exact.length === 0 && found.length === 1 ? found[0] : null;
}

/**
 * The player search: the toolbar search's players (current name or past gamertag), each labelled as it labels them, to
 * pick from by mouse or keyboard.
 */
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
  readonly picked = output<QuickSearchPlayerDto>();
  /** Search pressed (or Enter with no suggestion chosen): the text to look up. */
  readonly submitted = output<string>();
  /** The box emptied with its clear button. */
  readonly cleared = output<void>();

  readonly results = signal<QuickSearchPlayerDto[]>([]);
  readonly open = signal(false);
  /** The suggestion the arrow keys are on; -1: none. */
  readonly active = signal(-1);
  /** What a suggestion says beside the name, as in the toolbar search: current gamertag, archive (and years), or the name matched. */
  readonly detail = playerDetail;

  /** The text the suggestions shown are for. */
  private resultsFor = '';

  /**
   * Each new text cancels the pending search and any request in flight (switchMap), so a stale answer never lands after
   * a newer one, or after the list was closed. Short text (and a cancel: '') closes the list at once.
   */
  private readonly texts = new Subject<string>();

  constructor() {
    this.texts.pipe(
      switchMap(text => text.length < QUICK_SEARCH_MIN_LENGTH ? of({ text, results: null }) : timer(SEARCH_DEBOUNCE_MS).pipe(
        switchMap(() => this.api.searchPlayers(text)),
        catchError(() => of([])),
        map(results => ({ text, results })),
      )),
      takeUntilDestroyed(),
    ).subscribe(({ text, results }) => {
      this.resultsFor = text;
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
        this.active.update(i => {
          if (event.key === 'ArrowDown')
            return (i + 1) % count;
          return i <= 0 ? count - 1 : i - 1;
        });
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

  pick(player: QuickSearchPlayerDto): void {
    this.cancel();
    this.query.set(player.name);
    this.picked.emit(player);
  }

  /**
   * Search pressed, or Enter with no suggestion chosen: the player the suggestions shown point to (the one whose name or
   * past gamertag it is, else the only one), or, when several could be meant, the list to choose from, as two players can
   * share a name. Without suggestions for the text yet, the text is looked up (by the same rule).
   */
  submit(): void {
    const text = this.query().trim();
    if (!text) return;
    const found = this.resultsFor === text ? this.results() : [];
    const only = onlyMatch(found, text);
    if (only) {
      this.pick(only);
    } else if (found.length > 1) {
      this.open.set(true);
      this.active.set(0);
    } else {
      this.cancel();
      this.submitted.emit(text);
    }
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
