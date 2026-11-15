import { map, Observable } from "rxjs";
import { rxResource } from "@angular/core/rxjs-interop";
import { PaginationResult } from "./paginationResult";
import { linkedSignal, ResourceRef } from "@angular/core";
import { Filter } from "./table/table.component";
import { resourceView } from "./resourceView";

/** One page's request, built from the table's own filter and whatever extra column filters it has. */
export interface PaginationParams {
    pageSize: number,
    pageNumber: number,
    sortDirection?: string,
    sortColumn?: string,
    filters?: Filter[],
}

export function getPaginationResource<T, R = T>(
    // Takes this page's request and returns the generated service call for it. Its rows are R, the raw,
    // pre-parseRow shape (e.g. a generated model with dates as strings): parseRow below turns them into T.
    request: (params: PaginationParams) => Observable<PaginationResult<R>>,
    paginationFilter: PaginationFilter,
    additionalFilters?: () => Filter[] | undefined,
    // Turns each row from its JSON form (e.g. dates as strings) into a T; rows are used as
    // they are when it is missing or returns undefined.
    parseRow?: () => ((row: R) => T) | undefined,
) {
    const resource = rxResource<PaginationResult<T>, PaginationParams>({
        params: () => {
            const params: PaginationParams = {
                pageSize: paginationFilter.pageSize(),
                pageNumber: paginationFilter.pageNumber(),
            };

            // First check if the signals for sortDirection and sortColumn are defined
            if (paginationFilter.sortDirection && paginationFilter.sortColumn) {
                // Now check that both signals return a value, only then set them
                const sortDirection = paginationFilter.sortDirection();
                const sortColumn = paginationFilter.sortColumn();
                if (sortDirection && sortColumn) {
                    params.sortDirection = sortDirection;
                    params.sortColumn = sortColumn;
                }
            }

            const filters = additionalFilters?.()?.filter(f => !!f.value);
            if (filters?.length)
                params.filters = filters;

            return params;
        },
        stream: ({ params }) => request(params).pipe(map(page => {
            const parse = parseRow?.();
            // Without a parseRow the rows are already T (R defaults to T)
            return parse ? { ...page, results: page.results.map(parse) } : page as unknown as PaginationResult<T>;
        })),
        defaultValue: PAGINATION_RESPONSE_DEFAULT,
    });

    const page = resourceView(resource, PAGINATION_RESPONSE_DEFAULT);
    const current = linkedSignal<
    ResourceRef<PaginationResult<T>>,
    PaginationResult<T>
    >({
        source: () => resource,
        computation: (res, prevValue) => {
            // Keep showing the previous page while the next one loads.
            if (res.isLoading() && prevValue) {
                return prevValue.value;
            }
            // An empty page until one loads, and after a failure (callers render resource.error() to explain why)
            return page.value();
        },
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