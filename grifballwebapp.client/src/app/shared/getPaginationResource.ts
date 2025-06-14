import { httpResource, HttpResourceRef } from "@angular/common/http";
import { PaginationResult } from "./paginationResult";
import { linkedSignal } from "@angular/core";

export function getPaginationResource<T>(
    url: () => string,
    paginationFilter: PaginationFilter,
) {
    const resource = httpResource<PaginationResult<T>>(
        () => {
            const x = new URLSearchParams(); // HttpParams vs URLSearchParams?
            x.set('pageSize', paginationFilter.pageSize().toString());
            x.set('pageNumber', paginationFilter.pageNumber().toString());

            // First check if the signals for sortDirection and sortColumn are defined
            if (paginationFilter.sortDirection && paginationFilter.sortColumn) {
                // Now check that both signals return a value, only then set them
                const sortDirection = paginationFilter.sortDirection();
                const sortColumn = paginationFilter.sortColumn();
                if (sortDirection && sortColumn) {
                    x.set('sortDirection', sortDirection);
                    x.set('sortColumn', sortColumn);
                }
            }

            return `${url()}?${x}`;
        },
        { defaultValue: PAGINATION_RESPONSE_DEFAULT }
    );

    const current = linkedSignal<
    HttpResourceRef<PaginationResult<T>>,
    PaginationResult<T>
    >({
        source: () => resource,
        computation: (value, prevValue) =>
        value.isLoading() && prevValue ? prevValue.value : value.value(),
    });

    return {
    resource: resource,
    current: current,
    };
}

export interface PaginationFilter
{
    pageSize: () => number,
    pageNumber: () => number,
    sortDirection?: () => string | undefined,
    sortColumn?: () => string | undefined,
}

const PAGINATION_RESPONSE_DEFAULT: PaginationResult<never> = {
  results: [],
  totalCount: 0,
};