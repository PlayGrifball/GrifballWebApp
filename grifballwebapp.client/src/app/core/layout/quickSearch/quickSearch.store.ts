import { computed, inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { SearchService } from '@api/services/search.service';
import { StoreInput } from '@shared/storeInput';
import { resourceView } from '@shared/resourceView';

/** The fewest characters searched for (as the server's QuickSearchService.MinLength). */
export const QUICK_SEARCH_MIN_LENGTH = 2;

/** The quick search's hits for the term searched for. One per navigator (the component provides it). */
@Injectable()
export class QuickSearchStore {
  private readonly search = inject(SearchService);

  /** What is searched for (the typed text, trimmed, once typing pauses): the component's. */
  readonly term = new StoreInput<string>();

  /** The term is shorter than the server searches: nothing is asked for it. */
  readonly tooShort = computed(() => (this.term.value() ?? '').length < QUICK_SEARCH_MIN_LENGTH);

  private readonly results = resourceView(rxResource({
    params: () => this.tooShort() ? undefined : this.term.value(),
    stream: ({ params }) => this.search.quickSearch({ q: params }),
  }), null);

  /** The hits for the term; null while they load, before a term, or when the search failed. */
  readonly hits = this.results.value;
  readonly loading = this.results.isLoading;
  readonly failed = this.results.failed;

  /** Searches again for the term (after that failed). */
  retry(): void {
    this.results.reload();
  }
}
