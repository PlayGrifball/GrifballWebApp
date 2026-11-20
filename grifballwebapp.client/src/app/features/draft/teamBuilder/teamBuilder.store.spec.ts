import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { CaptainAddedResponseDto, DraftLiveDto, TeamResponseDto, TeamsPlayerDto } from '@api/models';
import { SeasonService } from '@api/services/season.service';
import { TeamsService } from '@api/services/teams.service';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS, LIVE_SPREAD_MS } from '@core/live/liveReload';
import { of, Subject } from 'rxjs';
import { TeamBuilderStore } from './teamBuilder.store';

const player = (personID: number, name: string): TeamsPlayerDto => ({ name, personID, pick: null, round: null, region: null });
const team = (personID: number, name: string, players: TeamsPlayerDto[] = []): TeamResponseDto =>
  ({ teamID: personID * 10, teamName: `${name}'s Team`, captain: { name, personID, order: 1, region: null }, players });

/** The live service as the page sees it: who watches what, and a message to send them. */
class FakeLive {
  readonly watching: { topics: readonly string[], listener: (message: LiveMessage) => void, stopped: boolean }[] = [];

  watch(topics: readonly string[], listener: (message: LiveMessage) => void): () => void {
    const watch = { topics, listener, stopped: false };
    this.watching.push(watch);
    return () => watch.stopped = true;
  }

  /** Sends `message` to those watching its topic; a null topic (the connection came back) to everyone. */
  send(message: LiveMessage): void {
    for (const watch of this.watching.filter(w => !w.stopped && (message.topic === null || w.topics.includes(message.topic))))
      watch.listener(message);
  }

  get active(): (readonly string[])[] {
    return this.watching.filter(w => !w.stopped).map(w => w.topics);
  }
}

describe('TeamBuilderStore', () => {
  let store: TeamBuilderStore;
  let api: jasmine.SpyObj<TeamsService>;
  let seasons: jasmine.SpyObj<SeasonService>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let live: FakeLive;

  beforeEach(() => {
    jasmine.clock().install();
    // No spread: a reload runs LIVE_RELOAD_MS after a change
    spyOn(Math, 'random').and.returnValue(0);
    api = jasmine.createSpyObj<TeamsService>('TeamsService', [
      'getTeams', 'getPlayerPool', 'areCaptainsLocked', 'lockCaptains', 'unlockCaptains', 'addCaptain', 'resortCaptain',
      'removeCaptain', 'addPlayerToTeam', 'movePlayerToTeam', 'removePlayerFromTeam',
    ]);
    api.getTeams.and.returnValue(of([team(1, 'Cap')]));
    api.getPlayerPool.and.returnValue(of([player(2, 'Pool')]));
    api.areCaptainsLocked.and.returnValue(of(false));
    seasons = jasmine.createSpyObj<SeasonService>('SeasonService', ['getCurrentSeasonId']);
    snackBar = jasmine.createSpyObj<MatSnackBar>('MatSnackBar', ['open']);
    live = new FakeLive();

    TestBed.configureTestingModule({
      providers: [
        TeamBuilderStore,
        { provide: TeamsService, useValue: api },
        { provide: SeasonService, useValue: seasons },
        { provide: MatSnackBar, useValue: snackBar },
        { provide: LiveService, useValue: live },
      ],
    });
    store = TestBed.inject(TeamBuilderStore);
  });

  afterEach(() => jasmine.clock().uninstall());

  /** Someone saved a change to season 5's draft (a notice, without the draft): the store's wait for more changes runs out. */
  const liveChange = () => {
    live.send({ topic: 'draft:5' });
    jasmine.clock().tick(LIVE_RELOAD_MS);
  };
  /** Season 5's draft as the live hub pushes it. */
  const pushed = (overrides: Partial<DraftLiveDto> = {}): DraftLiveDto =>
    ({ seasonID: 5, teams: [team(1, 'Cap'), team(3, 'New')], pool: [], captainsLocked: true, ...overrides });
  const push = (draft: DraftLiveDto) => live.send({ topic: 'draft:5', data: draft });
  /** Long enough for any reload a change could start. */
  const waitOut = () => jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);

  it('shows a season\'s draft: its teams, its pool and whether captains are locked', () => {
    store.show(5);
    expect(api.getTeams).toHaveBeenCalledWith({ seasonID: 5 });
    expect(store.shownSeasonID()).toBe(5);
    expect(store.teams().map(t => t.captain.name)).toEqual(['Cap']);
    expect(store.playerPool().map(p => p.name)).toEqual(['Pool']);
    expect(store.captainLocked()).toBeFalse();
    expect(store.onDeck()).withContext('No one on deck before the captains are locked').toBeNull();
  });

  it('lets only the store change the season shown', () => {
    store.show(5);
    // @ts-expect-error the season shown is set by show(), which resets the draft with it: never from outside
    expect(store.shownSeasonID.set).toBeUndefined();
    expect(store.shownSeasonID()).toBe(5);
  });

  it('shows the current season\'s draft for season 0, once it knows which that is', () => {
    seasons.getCurrentSeasonId.and.returnValue(of(9));
    store.show(0);
    expect(store.shownSeasonID()).toBe(9);
    expect(api.getTeams).toHaveBeenCalledWith({ seasonID: 9 });
  });

  it('applies neither teams nor pool when one of them fails, and says so', () => {
    const pool = new Subject<TeamsPlayerDto[]>();
    api.getPlayerPool.and.returnValue(pool);
    store.show(5);
    pool.error(new HttpErrorResponse({ status: 500 }));
    expect(store.teams()).toEqual([]);
    expect(store.captainLocked()).toBeFalse();
    expect(snackBar.open).toHaveBeenCalledWith('Failed to load the draft', 'Close');
  });

  it('applies a new captain at once, then the team name the server gave it', () => {
    store.show(5);
    const saved = new Subject<CaptainAddedResponseDto>();
    api.addCaptain.and.returnValue(saved);
    store.addCaptain(player(2, 'Pool'), "Pool's Team", 1);
    expect(store.teams().map(t => t.teamName)).toEqual(["Cap's Team", "Pool's Team"]);
    expect(store.playerPool()).toEqual([]);
    expect(api.addCaptain).toHaveBeenCalledWith({ seasonID: 5, body: { seasonID: 5, personID: 2, orderNumber: 2 } });

    saved.next({ teamID: 77, teamName: 'Signup Name' });
    saved.complete();
    expect(store.teams()[1].teamName).toBe('Signup Name');
    expect(store.teams()[1].teamID).toBe(77);
  });

  it('says why a change was refused, and reloads once no other save is on its way', () => {
    store.show(5);
    const first = new Subject<void>();
    const second = new Subject<void>();
    api.addPlayerToTeam.and.returnValues(first, second);
    store.pick(1, 2, 'Cap');
    store.show(5);
    api.getTeams.calls.reset();
    store.pick(1, 2, 'Cap');

    first.error(new HttpErrorResponse({ status: 400, error: { detail: 'Not your turn' } }));
    expect(snackBar.open).toHaveBeenCalledWith('Draft change not saved: Not your turn', 'Close');
    expect(api.getTeams).not.toHaveBeenCalled();

    second.complete();
    expect(api.getTeams).toHaveBeenCalledTimes(1);
  });

  it('drops a load that a save started during, and loads again once the save is done', () => {
    const teams = new Subject<TeamResponseDto[]>();
    api.getTeams.and.returnValue(teams);
    store.show(5);
    const saving = new Subject<void>();
    api.removeCaptain.and.returnValue(saving);
    store.teams.set([team(1, 'Cap')]);
    store.disband(1, 0);

    api.getTeams.and.returnValue(of([team(3, 'Later')]));
    teams.next([team(1, 'Stale')]);
    teams.complete();
    expect(store.teams()).toEqual([]);

    saving.complete();
    expect(store.teams().map(t => t.captain.name)).toEqual(['Later']);
  });

  it('reorders the pool without saving: the server keeps no order', () => {
    store.show(5);
    store.playerPool.set([player(2, 'A'), player(3, 'B')]);
    store.movePoolPlayer(3, 0);
    expect(store.playerPool().map(p => p.name)).toEqual(['B', 'A']);
    expect(api.removePlayerFromTeam).not.toHaveBeenCalled();
  });

  it('watches the shown season\'s draft, and nothing before a season is known', () => {
    TestBed.tick();
    expect(live.active).toEqual([]);

    store.show(5);
    TestBed.tick();
    expect(live.active).toEqual([['draft:5']]);

    store.show(6);
    TestBed.tick();
    expect(live.active).toEqual([['draft:6']]);
  });

  it('loads the draft again once a burst of live changes settles: teams, pool and lock state', () => {
    store.show(5);
    TestBed.tick();
    api.getTeams.and.returnValue(of([team(1, 'Cap'), team(3, 'New')]));
    api.getPlayerPool.and.returnValue(of([]));
    api.areCaptainsLocked.and.returnValue(of(true));
    api.getTeams.calls.reset();

    live.send({ topic: 'draft:5' });
    jasmine.clock().tick(LIVE_RELOAD_MS - 1);
    live.send({ topic: 'draft:5' });
    jasmine.clock().tick(LIVE_RELOAD_MS - 1);
    expect(api.getTeams).not.toHaveBeenCalled();

    jasmine.clock().tick(1);
    expect(api.getTeams).toHaveBeenCalledOnceWith({ seasonID: 5 });
    expect(store.teams().map(t => t.captain.name)).toEqual(['Cap', 'New']);
    expect(store.playerPool()).toEqual([]);
    expect(store.captainLocked()).toBeTrue();
  });

  it('reloads after a live change, but not under a drag: once it ends', () => {
    store.show(5);
    TestBed.tick();
    api.getTeams.calls.reset();
    store.dragStarted();
    liveChange();
    expect(api.getTeams).not.toHaveBeenCalled();

    store.dragEnded();
    expect(api.getTeams).toHaveBeenCalledTimes(1);
  });

  it('reloads after a live change, but not while a save is on its way: once it is done', () => {
    store.show(5);
    TestBed.tick();
    const saving = new Subject<void>();
    api.addPlayerToTeam.and.returnValue(saving);
    store.pick(1, 2, 'Cap');
    api.getTeams.calls.reset();
    liveChange();
    expect(api.getTeams).not.toHaveBeenCalled();

    saving.complete();
    expect(api.getTeams).toHaveBeenCalledTimes(1);
  });

  it('shows a pushed draft at once, with no API call', () => {
    store.show(5);
    TestBed.tick();
    api.getTeams.calls.reset();

    push(pushed());
    expect(store.teams().map(t => t.captain.name)).toEqual(['Cap', 'New']);
    expect(store.playerPool()).toEqual([]);
    expect(store.captainLocked()).toBeTrue();
    waitOut();
    expect(api.getTeams).not.toHaveBeenCalled();
  });

  it('ignores another season\'s draft', () => {
    store.show(5);
    TestBed.tick();
    api.getTeams.calls.reset();

    push(pushed({ seasonID: 6 }));
    waitOut();
    expect(store.teams().map(t => t.captain.name)).toEqual(['Cap']);
    expect(store.captainLocked()).toBeFalse();
    expect(api.getTeams).not.toHaveBeenCalled();
  });

  it('defers a pushed draft under a drag, and loads the draft once the drag ends', () => {
    store.show(5);
    TestBed.tick();
    api.getTeams.calls.reset();
    store.dragStarted();

    push(pushed());
    waitOut();
    expect(store.teams().map(t => t.captain.name)).toEqual(['Cap']);
    expect(api.getTeams).not.toHaveBeenCalled();

    store.dragEnded();
    expect(api.getTeams).toHaveBeenCalledTimes(1);
  });

  it('defers a pushed draft while a save is on its way, and loads the draft once it is done', () => {
    store.show(5);
    TestBed.tick();
    const saving = new Subject<void>();
    api.addPlayerToTeam.and.returnValue(saving);
    store.pick(1, 2, 'Cap');
    api.getTeams.calls.reset();

    push(pushed());
    waitOut();
    expect(store.teams().map(t => t.captain.name)).toEqual(['Cap']);
    expect(api.getTeams).not.toHaveBeenCalled();

    saving.complete();
    expect(api.getTeams).toHaveBeenCalledTimes(1);
  });

  it('drops a load that was on its way when a draft was pushed: the pushed one is newer', () => {
    store.show(5);
    TestBed.tick();
    const loading = new Subject<TeamResponseDto[]>();
    api.getTeams.and.returnValue(loading);
    store.load();

    push(pushed());
    loading.next([team(9, 'Old')]);
    loading.complete();
    expect(store.teams().map(t => t.captain.name)).toEqual(['Cap', 'New']);
    expect(store.captainLocked()).toBeTrue();
  });

  it('loads the draft again when the live connection comes back, once no drag is under way', () => {
    store.show(5);
    TestBed.tick();
    api.getTeams.calls.reset();
    store.dragStarted();

    live.send({ topic: null });
    jasmine.clock().tick(LIVE_RELOAD_MS);
    expect(api.getTeams).not.toHaveBeenCalled();

    store.dragEnded();
    expect(api.getTeams).toHaveBeenCalledOnceWith({ seasonID: 5 });
  });

  it('stops watching once the page is gone', () => {
    store.show(5);
    TestBed.tick();
    TestBed.resetTestingModule();
    expect(live.active).toEqual([]);
  });

  it('locks the captains when unlocked, and unlocks them when locked', () => {
    store.show(5);
    api.lockCaptains.and.returnValue(of(undefined));
    api.unlockCaptains.and.returnValue(of(undefined));
    store.toggleCaptainLock();
    expect(api.lockCaptains).toHaveBeenCalledWith({ seasonID: 5 });
    expect(store.captainLocked()).toBeTrue();
    store.toggleCaptainLock();
    expect(api.unlockCaptains).toHaveBeenCalledWith({ seasonID: 5 });
    expect(store.captainLocked()).toBeFalse();
  });
});
