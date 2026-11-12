import { map, Observable } from "rxjs";
import { rxResource } from "@angular/core/rxjs-interop";
import { PaginationResult } from "./paginationResult";
import { linkedSignal, ResourceRef } from "@angular/core";
import { Filter } from "./table/table.component";

/** One page's request, built from the table's own filter and whatever extra column filters it has. */
export interface PaginationParams {
    pageSize: number,
    pageNumber: number,
    sortDirection?: string,
    sortColumn?: string,
    filters?: Filter[],
}

export function getPaginationResource<T>(
    // Takes this page's request and returns the generated service call for it. Its rows are the raw,
    // pre-parseRow shape (e.g. a generated model with dates as strings), hence any: parseRow below is
    // what actually turns them into T, same as the raw JSON this used to parse directly.
    request: (params: PaginationParams) => Observable<PaginationResult<any>>,
    paginationFilter: PaginationFilter,
    additionalFilters?: () => Filter[] | undefined,
    // Turns each row from its JSON form (e.g. dates as strings) into a T; rows are used as
    // they are when it is missing or returns undefined.
    parseRow?: () => ((row: any) => T) | undefined,
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
            return parse ? { ...page, results: page.results.map(parse) } : page;
        })),
        defaultValue: PAGINATION_RESPONSE_DEFAULT,
    });

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
            // value() throws while the resource is in the error state (Angular 20+),
            // so only read it when hasValue() is true and fall back to an empty page.
            // Callers render resource.error() to explain why the table is empty.
            return res.hasValue() ? res.value() : PAGINATION_RESPONSE_DEFAULT;
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