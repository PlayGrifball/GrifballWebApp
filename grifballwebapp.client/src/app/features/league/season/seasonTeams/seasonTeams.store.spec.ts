import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { SeasonSummaryDto, SeasonTeamDto } from '@api/models';
import { SeasonTeamsStore } from './seasonTeams.store';

const team = (teamName: string) => ({ teamName, players: [] }) as unknown as SeasonTeamDto;
const summary = { isHistorical: true } as SeasonSummaryDto;

describe('SeasonTeamsStore', () => {
  let store: SeasonTeamsStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SeasonTeamsStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(SeasonTeamsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("loads the season's teams and summary", () => {
    store.show(4);
    expect(store.seasonID()).toBe(4);
    expect(store.loading()).toBeTrue();
    http.expectOne('/api/seasons/4/summary').flush(summary);
    http.expectOne('/api/seasons/4/teams').flush([team('Alpha')]);
    expect(store.loading()).toBeFalse();
    expect(store.summary()).toEqual(summary);
    expect(store.teams().map(t => t.teamName)).toEqual(['Alpha']);
  });

  it('shows the teams without the summary when it fails', () => {
    store.show(4);
    http.expectOne('/api/seasons/4/summary').flush('x', { status: 500, statusText: 'Server Error' });
    http.expectOne('/api/seasons/4/teams').flush([team('Alpha')]);
    expect(store.summary()).toBeNull();
    expect(store.teams()).toHaveSize(1);
    expect(store.error()).toBeNull();
  });

  it('tells a season not found from a failed load', () => {
    store.show(999);
    http.expectOne('/api/seasons/999/summary').flush('x', { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/seasons/999/teams').flush('x', { status: 404, statusText: 'Not Found' });
    expect(store.error()).toBe('Season not found');
    expect(store.loading()).toBeFalse();

    store.show(5);
    expect(store.error()).toBeNull();
    http.expectOne('/api/seasons/5/summary').flush(summary);
    http.expectOne('/api/seasons/5/teams').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.error()).toBe('Failed to load season teams');
  });

  it("forgets the last season, and drops its answers, for a new one", () => {
    store.show(4);
    const oldTeams = http.expectOne('/api/seasons/4/teams');
    http.expectOne('/api/seasons/4/summary').flush(summary);
    store.show(5);
    expect(oldTeams.cancelled).toBeTrue();
    expect(store.summary()).toBeNull();
    http.expectOne('/api/seasons/5/summary').flush(summary);
    http.expectOne('/api/seasons/5/teams').flush([team('Beta')]);
    expect(store.teams().map(t => t.teamName)).toEqual(['Beta']);
  });
});
