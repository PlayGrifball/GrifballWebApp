import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { SeasonListItemDto } from '@api/models';
import { SEASONS_PAGE_SIZE, SeasonsStore } from './seasons.store';

const season = (seasonID: number, leagueID: number, year: number, gameVersionID: number | null) =>
  ({ seasonID, leagueID, leagueName: `League ${leagueID}`, year, gameVersionID, gameVersionName: gameVersionID ? `Game ${gameVersionID}` : null }) as SeasonListItemDto;
const page = (...results: SeasonListItemDto[]) => ({ results });
const all = { leagueID: null, gameVersionID: null, format: null, year: null };

describe('SeasonsStore', () => {
  let store: SeasonsStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SeasonsStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(SeasonsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const list = () => http.expectOne(r => r.url === '/api/seasons');

  it('loads every season, with the choices from them', () => {
    store.load(all, true);
    const request = list();
    expect(request.request.params.get('pageSize')).toBe(String(SEASONS_PAGE_SIZE));
    expect(request.request.params.has('leagueID')).toBeFalse();
    request.flush(page(season(1, 2, 2024, 6), season(2, 1, 2026, null), season(3, 1, 2024, 6)));
    expect(store.loaded()).toBeTrue();
    expect(store.seasons()).toHaveSize(3);
    expect(store.leagues()).toEqual([{ id: 1, name: 'League 1' }, { id: 2, name: 'League 2' }]);
    expect(store.games()).toEqual([{ id: 6, name: 'Game 6' }]);
    expect(store.years()).toEqual([2026, 2024]);
  });

  it('loads the seasons a filter picks, leaving the choices, and drops the last filter\'s list', () => {
    store.load({ leagueID: 1, gameVersionID: 6, format: 'Tournament', year: 2024 }, false);
    const first = list();
    expect(first.request.params.get('leagueID')).toBe('1');
    expect(first.request.params.get('gameVersionID')).toBe('6');
    expect(first.request.params.get('format')).toBe('Tournament');
    expect(first.request.params.get('year')).toBe('2024');

    store.load({ leagueID: 2, gameVersionID: null }, false);
    expect(first.cancelled).toBeTrue();
    expect(store.loaded()).toBeFalse();
    list().flush(page(season(1, 2, 2024, 6)));
    expect(store.seasons()).toHaveSize(1);
    expect(store.leagues()).toEqual([]);
  });

  it('is loaded with what it had when the list fails', () => {
    store.load(all, false);
    list().flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.loaded()).toBeTrue();
    expect(store.seasons()).toEqual([]);
  });

  it('loads the choices from every season', () => {
    store.loadOptions();
    const request = list();
    expect(request.request.params.keys()).toEqual(['pageSize']);
    request.flush(page(season(1, 2, 2024, 6)));
    expect(store.leagues()).toEqual([{ id: 2, name: 'League 2' }]);
    expect(store.seasons()).toEqual([]);
  });
});
