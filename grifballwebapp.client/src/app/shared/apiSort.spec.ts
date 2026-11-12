import { apiSortDirection } from './apiSort';

describe('apiSortDirection', () => {
  it('names Material\'s directions as the API does, and no sort as none', () => {
    expect(apiSortDirection('asc')).toBe('Asc');
    expect(apiSortDirection('desc')).toBe('Desc');
    expect(apiSortDirection('')).toBeUndefined();
    expect(apiSortDirection(undefined)).toBeUndefined();
  });
});
