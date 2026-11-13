import type { SortDirection } from '@api/models';

/** Material's sort direction ('asc', 'desc', or '' for none) as a list endpoint takes it; none when the column isn't sorted. */
export function apiSortDirection(direction: string | undefined): SortDirection | undefined {
  if (direction === 'asc')
    return 'Asc';
  return direction === 'desc' ? 'Desc' : undefined;
}
