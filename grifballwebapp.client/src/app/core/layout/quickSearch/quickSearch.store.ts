import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { QuickSearchDto } from '@api/models';
import { SearchService } from '@api/services/search.service';
import { StoreInput } from '@shared/storeInput';

/** The fewest characters searched for (as the server's QuickSearchService.MinLength). */
export const QUICK_SEARCH_MIN_LENGTH = 2;

/** The quick search's hits for the term searched for. One per navigator (the component provides it). */
@Injectable()
export class QuickSearchStore {
  private readonly search = inject(SearchService);

  /** What is searched for (the typed text, trimmed, once typing pauses): the component's. */
  readonly term = new StoreInput<string>();

  /** Nothing is asked for a term shorter than the server searches. */
  private readonly results = rxResource({
    params: () => {
      const term = this.term.value();
      return term !== undefined && term.length >= QUICK_SEARCH_MIN_LENGTH ? term : undefined;
    },
    stream: ({ params }) => this.search.quickSearch({ q: params }),
  });

  /** The hits for the term; null while they load, before a term, or when the search failed. */
  readonly hits = computed<QuickSearchDto | null>(() => this.results.hasValue() ? this.results.value() : null);
  readonly loading = computed(() => this.results.isLoading());
  readonly failed = computed(() => this.results.status() === 'error');

  /** Searches again for the term (after that failed). */
  retry(): void {
    this.results.reload();
  }
}
