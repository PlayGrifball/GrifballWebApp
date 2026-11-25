import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { TeamDto, TeamPageMatchDto } from '@api/models';
import { DateTime } from 'luxon';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS } from '@core/live/liveReload';
import { TeamStore } from './team.store';

const team = { teamID: 7, teamName: 'Red Team' } as unknown as TeamDto;
const match = { seasonMatchID: 1, scheduledTime: '2026-04-01T19:00:00Z' } as TeamPageMatchDto;

/** The live service as the store sees it: who watches what, and a message to send them. */
class FakeLive {
  readonly watching: { topics: readonly string[], listener: (message: LiveMessage) => void }[] = [];

  watch(topics: readonly string[], listener: (message: LiveMessage) => void): () => void {
    this.watching.push({ topics, listener });
    return () => undefined;
  }

  send(message: LiveMessage): void {
    for (const watch of this.watching.filter(w => message.topic !== null && w.topics.includes(message.topic)))
      watch.listener(message);
  }
}

describe('TeamStore', () => {
  let store: TeamStore;
  let http: HttpTestingController;
  let live: FakeLive;

  beforeEach(() => {
    live = new FakeLive();
    TestBed.configureTestingModule({ providers: [TeamStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: LiveService, useValue: live }] });
    store = TestBed.inject(TeamStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it has the team', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.team()).toBeNull();
    expect(store.matchesLoaded()).toBeFalse();
    expect(store.teamFirstLoad()).toBeFalse();
  });

  it('loads the team and its matches, their times read', async () => {
    store.teamID.use(signal(7));
    store.seasonID.use(signal(2));
    TestBed.tick();
    expect(store.teamFirstLoad()).toBeTrue();
    expect(store.matchesFirstLoad()).toBeTrue();
    http.expectOne('/api/teams/7').flush(team);
    http.expectOne('/api/teams/7/matches').flush([match]);
    await settle();
    expect(store.team()).toEqual(team);
    expect(store.teamFirstLoad()).toBeFalse();
    expect(store.matchesLoaded()).toBeTrue();
    expect(store.matches()[0].scheduledTime instanceof DateTime).toBeTrue();
  });

  it('has no team when there is none', async () => {
    store.teamID.use(signal(7));
    store.seasonID.use(signal(2));
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/teams/7/matches').flush([]);
    await settle();
    expect(store.team()).toBeNull();
    expect(store.teamFirstLoad()).toBeFalse();
  });

  it('says the matches failed, rather than that there are none, when they cannot be read', async () => {
    store.teamID.use(signal(7));
    store.seasonID.use(signal(2));
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(team);
    http.expectOne('/api/teams/7/matches').flush(null, { status: 500, statusText: 'Server Error' });
    await settle();
    expect(store.matchesFirstLoad()).toBeFalse();
    expect(store.matchesFailed()).toBeTrue();
    expect(store.matchesLoaded()).toBeFalse();
    expect(store.matches()).toEqual([]);
  });

  it('drops the last team for another, its matches too, cancelling its requests, and loads again for another season', async () => {
    const teamID = signal(7);
    const seasonID = signal(2);
    store.teamID.use(teamID);
    store.seasonID.use(seasonID);
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(team);
    http.expectOne('/api/teams/7/matches').flush([match]);
    await settle();

    teamID.set(8);
    TestBed.tick();
    expect(store.team()).toBeNull();
    expect(store.matches()).toEqual([]);
    expect(store.matchesLoaded()).toBeFalse();
    const teamRequest = http.expectOne('/api/teams/8');
    const matchesRequest = http.expectOne('/api/teams/8/matches');
    seasonID.set(3);
    TestBed.tick();
    expect(teamRequest.cancelled).toBeTrue();
    expect(matchesRequest.cancelled).toBeTrue();
    http.expectOne('/api/teams/8').flush(team);
    http.expectOne('/api/teams/8/matches').flush([]);
    await settle();
  });

  it("watches its team's page, and shows the team and its matches pushed with a change, with no request, not another team's", async () => {
    store.teamID.use(signal(7));
    store.seasonID.use(signal(2));
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(team);
    http.expectOne('/api/teams/7/matches').flush([]);
    await settle();
    const renamed = { ...team, teamName: 'Red Rampage' } as TeamDto;

    expect(live.watching.map(w => w.topics)).toEqual([['team:7'], ['teams:2']]);
    live.send({ topic: 'team:7', data: { teamID: 8, team: renamed, matches: [match], playerStats: [] } });
    expect(store.team()).toEqual(team);
    live.send({ topic: 'team:7', data: { teamID: 7, team: renamed, matches: [match], playerStats: [] } });
    expect(store.team()).toEqual(renamed);
    expect(store.matches()[0].scheduledTime instanceof DateTime).toBeTrue();
    live.send({ topic: 'team:7', data: { teamID: 7, team: null, matches: [], playerStats: [] } });
    expect(store.team()).toBeNull();
    expect(store.matchesLoaded()).toBeTrue();
    TestBed.tick();
    http.expectNone(() => true);
  });

  it('reads the team again on a change without its data', async () => {
    store.teamID.use(signal(7));
    store.seasonID.use(signal(2));
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(team);
    http.expectOne('/api/teams/7/matches').flush([]);
    await settle();
    jasmine.clock().install();
    spyOn(Math, 'random').and.returnValue(0);
    try {
      live.send({ topic: 'team:7' });
      jasmine.clock().tick(LIVE_RELOAD_MS);
    } finally {
      jasmine.clock().uninstall();
    }
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(team);
    http.expectOne('/api/teams/7/matches').flush([match]);
    await settle();
    expect(store.matches()).toHaveSize(1);
  });

  it("reads the team again on a change to its season's teams page: its season, its titles, an opponent renamed", async () => {
    store.teamID.use(signal(7));
    store.seasonID.use(signal(2));
    TestBed.tick();
    http.expectOne('/api/teams/7').flush(team);
    http.expectOne('/api/teams/7/matches').flush([]);
    await settle();
    jasmine.clock().install();
    spyOn(Math, 'random').and.returnValue(0);
    try {
      live.send({ topic: 'teams:2', data: { seasonID: 2, summary: null, teams: [] } });
      jasmine.clock().tick(LIVE_RELOAD_MS);
    } finally {
      jasmine.clock().uninstall();
    }
    TestBed.tick();
    http.expectOne('/api/teams/7').flush({ ...team, titles: ['Gold'] });
    http.expectOne('/api/teams/7/matches').flush([]);
    await settle();
    expect(store.team()!.titles).toEqual(['Gold']);
  });
});
