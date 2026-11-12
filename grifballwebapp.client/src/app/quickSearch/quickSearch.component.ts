import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, linkedSignal, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { SearchService } from '@api/services';
import { QuickSearchDto } from '@api/models';
import { playerDetail } from './playerDetail';

/** How long typing pauses before the search runs. */
export const QUICK_SEARCH_DEBOUNCE_MS = 200;
/** The fewest characters searched for (as the server's QuickSearchService.MinLength). */
export const QUICK_SEARCH_MIN_LENGTH = 2;

/** One hit: what it is, what it is called, a line about it and where it goes. */
export interface QuickSearchItem {
  id: string;
  kind: 'player' | 'team' | 'season';
  label: string;
  detail: string | null;
  link: (string | number)[];
}

/** A group of hits under its heading, in the order the list shows them. */
export interface QuickSearchGroup {
  kind: QuickSearchItem['kind'];
  heading: string;
  items: QuickSearchItem[];
}

/** The hits as the list shows them: players, then teams (each season's, linking to its roster), then seasons. */
export function toGroups(result: QuickSearchDto): QuickSearchGroup[] {
  const groups: QuickSearchGroup[] = [
    {
      kind: 'player', heading: 'Players',
      items: result.players.map(p => ({
        id: `qs-player-${p.userID}`, kind: 'player' as const, label: p.name,
        detail: playerDetail(p),
        link: ['/profile', p.userID],
      })),
    },
    {
      kind: 'team', heading: 'Teams',
      items: result.teams.map(t => ({
        id: `qs-team-${t.teamID}`, kind: 'team' as const, label: t.teamName, detail: t.seasonName,
        link: ['/season', t.seasonID, 'team', t.teamID],
      })),
    },
    {
      kind: 'season', heading: 'Seasons',
      items: result.seasons.map(s => ({
        id: `qs-season-${s.seasonID}`, kind: 'season' as const, label: s.seasonName, detail: s.years,
        link: ['/season', s.seasonID],
      })),
    },
  ];
  return groups.filter(g => g.items.length > 0);
}

/**
 * The quick jump navigator (Ctrl+K, or the toolbar's search button), opened in a dialog: players by their current
 * or past gamertags, teams and seasons as you type. The arrow keys move through the hits, Enter goes to the one
 * picked, Esc closes it. The input is an ARIA combobox over the hits' listbox.
 */
@Component({
  selector: 'app-quick-search',
  imports: [MatButtonModule, MatIconModule],
  templateUrl: './quickSearch.component.html',
  styleUrl: './quickSearch.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuickSearchComponent {
  private search = inject(SearchService);
  private router = inject(Router);
  /** Null when it is not in a dialog (in a test): then nothing is closed on a jump. */
  readonly dialogRef = inject<MatDialogRef<QuickSearchComponent>>(MatDialogRef, { optional: true });

  /** What is typed, as it is typed. */
  readonly text = signal('');
  /** What is searched for: the typed text, trimmed, once typing pauses. */
  readonly term = signal('');
  private timer: ReturnType<typeof setTimeout> | undefined;

  /** Enter was pressed before the hits for what is typed came: go to the first of them once they do. */
  private readonly goWhenReady = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
    effect(() => {
      if (!this.goWhenReady() || this.loading())
        return;
      untracked(() => {
        this.goWhenReady.set(false);
        const item = this.activeItem();
        if (!this.failed() && item)
          this.go(item);
      });
    });
  }

  readonly tooShort = computed(() => this.term().length < QUICK_SEARCH_MIN_LENGTH);

  readonly results = rxResource({
    params: () => this.tooShort() ? undefined : this.term(),
    stream: ({ params }) => this.search.quickSearch({ q: params! }),
  });

  readonly loading = computed(() => this.results.isLoading());
  readonly failed = computed(() => this.results.status() === 'error');
  /** The hits by group. While the next term's hits load, the last ones stay, so the list does not jump as you type. */
  readonly groups = linkedSignal<QuickSearchGroup[] | null, QuickSearchGroup[]>({
    source: () => {
      if (this.tooShort() || this.failed())
        return [];
      // With a term long enough and no error, the search has its hits or is loading them: null keeps the last shown
      return this.results.hasValue() ? toGroups(this.results.value()) : null;
    },
    computation: (next, previous) => next ?? previous?.value ?? [],
  });
  /** Every hit, in list order: what the arrow keys move through. */
  readonly items = computed(() => this.groups().flatMap(g => g.items));
  readonly empty = computed(() => !this.tooShort() && !this.loading() && !this.failed() && this.items().length === 0);

  /** The hit picked (its index in items), back on the first whenever the hits change. */
  readonly active = linkedSignal<QuickSearchItem[], number>({ source: this.items, computation: () => 0 });
  readonly activeItem = computed<QuickSearchItem | null>(() => this.items()[this.active()] ?? null);

  onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.text.set(value);
    // A pending Enter was for what was typed then: going there now, mid-typing, would be a surprise
    this.goWhenReady.set(false);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.term.set(value.trim()), QUICK_SEARCH_DEBOUNCE_MS);
  }

  /**
   * The arrow keys, Home and End move the pick, round the ends, keeping it in view; Enter goes to it. When the list
   * shown is not yet for what is typed (typing has not paused, or its hits are loading), Enter searches now and goes
   * to the first hit once it comes. After a failed search, Enter tries it again (and goes to the first hit).
   */
  onKeydown(event: KeyboardEvent): void {
    const count = this.items().length;
    const move = (to: number) => {
      event.preventDefault();
      if (!count)
        return;
      this.active.set((to + count) % count);
      document.getElementById(this.items()[this.active()].id)?.scrollIntoView({ block: 'nearest' });
    };
    switch (event.key) {
      case 'ArrowDown': move(this.active() + 1); break;
      case 'ArrowUp': move(this.active() - 1); break;
      case 'Home': if (event.ctrlKey) move(0); break;
      case 'End': if (event.ctrlKey) move(count - 1); break;
      case 'Enter': {
        const typed = this.text().trim();
        if (typed === this.term() && this.failed()) {
          event.preventDefault();
          this.retry();
          this.goWhenReady.set(true);
          break;
        }
        if (typed !== this.term() || this.loading()) {
          event.preventDefault();
          clearTimeout(this.timer);
          this.term.set(typed);
          if (!this.tooShort())
            this.goWhenReady.set(true);
          break;
        }
        const item = this.activeItem();
        if (item) {
          event.preventDefault();
          this.go(item);
        }
        break;
      }
    }
  }

  /** Searches again for what was searched for, after that failed, keeping the focus in the input. */
  retry(): void {
    this.results.reload();
    document.getElementById('quick-search-input')?.focus();
  }

  /** Where the pointer was at its last move over the list (screen coordinates), or null before one. */
  private pointerAt: string | null = null;

  /**
   * The pointer moved over the list: pick the hit under it. Only a real move counts: when the arrow keys scroll the
   * list under a resting pointer, the browser sends a move event at the same spot (and enters the hit now under it),
   * which must not take the pick from the keyboard. The first move seen only marks where the pointer is.
   */
  onPointerMove(event: MouseEvent): void {
    const at = `${event.screenX},${event.screenY}`;
    const moved = this.pointerAt !== null && this.pointerAt !== at;
    this.pointerAt = at;
    const id = moved ? (event.target as Element).closest('[role="option"]')?.id : undefined;
    const index = this.items().findIndex(item => item.id === id);
    if (index >= 0)
      this.active.set(index);
  }

  /** Goes to the hit's page and closes the navigator. */
  go(item: QuickSearchItem): void {
    this.dialogRef?.close();
    void this.router.navigate(item.link);
  }

  close(): void {
    this.dialogRef?.close();
  }
}
