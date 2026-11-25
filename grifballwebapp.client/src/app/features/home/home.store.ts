import { computed, inject, Injectable, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { SeasonListItemDto, SeasonsLiveDto } from '@api/models';
import { HomeService } from '@api/services/home.service';
import { SeasonService } from '@api/services/season.service';
import { apiSortDirection } from '@shared/apiSort';
import { PaginationResult } from '@shared/paginationResult';
import { resourceView } from '@shared/resourceView';
import { StoreInput } from '@shared/storeInput';
import { liveData } from '@core/live/liveReload';
import { LiveTopics } from '@core/live/liveTopics';

/** Which page of past seasons the table shows, and how they are sorted (none: the server's order). */
export interface PastSeasonsPage {
  pageSize: number;
  pageNumber: number;
  sortDirection?: string;
  sortColumn?: string;
}

/** The home page's data: the current and coming season events, and the past seasons a page at a time. One per page (the component provides it). */
@Injectable()
export class HomeStore {
  private readonly home = inject(HomeService);
  private readonly seasonService = inject(SeasonService);

  private readonly loadedEvents = resourceView(rxResource({
    stream: () => this.home.currentAndFutureEvents(),
  }), []);
  readonly events = this.loadedEvents.value;
  /** The events are on their way: the hero and the objectives are skeletons. A failure shows none. */
  readonly eventsFirstLoad = this.loadedEvents.firstLoad;

  /** The page of past seasons the table asks for: the component's paginator and sort. */
  readonly page = new StoreInput<PastSeasonsPage>();

  /** A page of the seasons that have ended (index.html fetches the first page early, with these parameters in this order). */
  private readonly pages = resourceView(rxResource({
    params: () => this.page.value(),
    stream: ({ params }) => {
      const sorted = !!(params.sortColumn && params.sortDirection);
      return this.seasonService.getSeasonList({
        ended: true,
        pageNumber: params.pageNumber,
        pageSize: params.pageSize,
        sortDirection: sorted ? apiSortDirection(params.sortDirection) : undefined,
        sortColumn: sorted ? params.sortColumn : undefined,
      });
    },
  }), null);
  /** The page shown: the last one answered, kept while the next loads, and when it fails (the table pages on). */
  private readonly shown = linkedSignal<PaginationResult<SeasonListItemDto> | null, PaginationResult<SeasonListItemDto> | null>({
    source: () => this.pages.value(),
    computation: (page, previous) => page ?? previous?.value ?? null,
  });
  readonly pastSeasons = computed(() => this.shown()?.results ?? []);
  readonly total = computed(() => this.shown()?.totalCount ?? 0);
  /** No page shown yet and one on its way: the table's rows are a skeleton. */
  readonly pastSeasonsFirstLoad = computed(() => this.shown() === null && this.pages.firstLoad());
  /** No page shown and the last one failed: the table says so. */
  readonly pastSeasonsFailed = computed(() => this.shown() === null && this.pages.failed());

  constructor() {
    // A season or a match of one changed: the events as they are now come with it, as this viewer may see them (the
    // past seasons are past)
    liveData<SeasonsLiveDto>(() => [LiveTopics.seasons], seasons => {
      this.loadedEvents.set(seasons.events);
      return true;
    }, () => {
      if (this.loadedEvents.hasValue())
        this.loadedEvents.reload();
    });
  }
}
