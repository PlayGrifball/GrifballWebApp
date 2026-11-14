import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { TeamDto, TeamPageMatchDto } from '@api/models';
import { DateTime } from 'luxon';
import { TeamStore } from './team.store';

const team = { teamID: 7, teamName: 'Red Team' } as TeamDto;
const match = { seasonMatchID: 1, scheduledTime: '2026-04-01T19:00:00Z' } as TeamPageMatchDto;

describe('TeamStore', () => {
  let store: TeamStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [TeamStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(TeamStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks for nothing until it has the team', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.team()).toBeNull();
    expect(store.matchesLoaded()).toBeFalse();
  });

  it('loads the team and its matches, their times read', () => {
    store.teamID.use(signal(7));
    store.seasonID.use(signal(2));
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(team);
    http.expectOne('/api/teams/7/matches').flush([match]);
    expect(store.team()).toEqual(team);
    expect(store.matchesLoaded()).toBeTrue();
    expect(store.matches()[0].scheduledTime instanceof DateTime).toBeTrue();
  });

  it('has no team when there is none', () => {
    store.teamID.use(signal(7));
    store.seasonID.use(signal(2));
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/teams/7/matches').flush([]);
    expect(store.team()).toBeNull();
  });

  it('drops the last team for another, cancelling its requests, and loads again for another season', () => {
    const teamID = signal(7);
    const seasonID = signal(2);
    store.teamID.use(teamID);
    store.seasonID.use(seasonID);
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(team);
    http.expectOne('/api/teams/7/matches').flush([]);

    teamID.set(8);
    TestBed.tick();
    expect(store.team()).toBeNull();
    expect(store.matchesLoaded()).toBeFalse();
    const teamRequest = http.expectOne('/api/teams/8');
    const matchesRequest = http.expectOne('/api/teams/8/matches');
    seasonID.set(3);
    TestBed.tick();
    expect(teamRequest.cancelled).toBeTrue();
    expect(matchesRequest.cancelled).toBeTrue();
    http.expectOne('/api/teams/8').flush(team);
    http.expectOne('/api/teams/8/matches').flush([]);
  });
});
