import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { CaptainAddedResponseDto, TeamResponseDto, TeamsPlayerDto } from '@api/models';
import { SeasonService } from '@api/services/season.service';
import { TeamsService } from '@api/services/teams.service';
import { SignalRService } from '@core/signalr/signalR.service';
import { of, Subject } from 'rxjs';
import { TeamBuilderStore } from './teamBuilder.store';

const player = (personID: number, name: string): TeamsPlayerDto => ({ name, personID, pick: null, round: null, region: null });
const team = (personID: number, name: string, players: TeamsPlayerDto[] = []): TeamResponseDto =>
  ({ teamID: personID * 10, teamName: `${name}'s Team`, captain: { name, personID, order: 1, region: null }, players });

describe('TeamBuilderStore', () => {
  let store: TeamBuilderStore;
  let api: jasmine.SpyObj<TeamsService>;
  let seasons: jasmine.SpyObj<SeasonService>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let signalR: jasmine.SpyObj<SignalRService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<TeamsService>('TeamsService', [
      'getTeams', 'getPlayerPool', 'areCaptainsLocked', 'lockCaptains', 'unlockCaptains', 'addCaptain', 'resortCaptain',
      'removeCaptain', 'addPlayerToTeam', 'movePlayerToTeam', 'removePlayerFromTeam',
    ]);
    api.getTeams.and.returnValue(of([team(1, 'Cap')]));
    api.getPlayerPool.and.returnValue(of([player(2, 'Pool')]));
    api.areCaptainsLocked.and.returnValue(of(false));
    seasons = jasmine.createSpyObj<SeasonService>('SeasonService', ['getCurrentSeasonId']);
    snackBar = jasmine.createSpyObj<MatSnackBar>('MatSnackBar', ['open']);
    signalR = jasmine.createSpyObj<SignalRService>('SignalRService', [
      'addCaptain', 'resortCaptain', 'removeCaptain', 'addPlayerToTeam', 'movePlayerToTeam', 'removePlayerFromTeam',
      'lockCaptains', 'unlockCaptains', 'connected',
    ]);
    // Each registration returns its own unregister function, like the real service
    for (const name of Object.keys(signalR) as (keyof SignalRService)[]) {
      const method = signalR[name] as unknown as jasmine.Spy;
      if (jasmine.isSpy(method))
        method.and.callFake(() => jasmine.createSpy(`off ${String(name)}`));
    }
    (signalR as { hubConnection: unknown }).hubConnection = { connectionId: 'conn' };

    TestBed.configureTestingModule({
      providers: [
        TeamBuilderStore,
        { provide: TeamsService, useValue: api },
        { provide: SeasonService, useValue: seasons },
        { provide: MatSnackBar, useValue: snackBar },
        { provide: SignalRService, useValue: signalR },
      ],
    });
    store = TestBed.inject(TeamBuilderStore);
  });

  /** The callback the store registered with a SignalRService method. */
  const handler = <T>(method: jasmine.Spy): ((value: T) => void) => method.calls.mostRecent().args[0];

  it('shows a season\'s draft: its teams, its pool and whether captains are locked', () => {
    store.show(5);
    expect(api.getTeams).toHaveBeenCalledWith({ seasonID: 5 });
    expect(store.shownSeasonID()).toBe(5);
    expect(store.teams().map(t => t.captain.name)).toEqual(['Cap']);
    expect(store.playerPool().map(p => p.name)).toEqual(['Pool']);
    expect(store.captainLocked()).toBeFalse();
    expect(store.onDeck()!.name).toBe('Cap');
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
    expect(api.addCaptain).toHaveBeenCalledWith({ seasonID: 5, signalRConnectionID: 'conn', body: { seasonID: 5, personID: 2, orderNumber: 2 } });

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

  it('follows other users\' changes for the season shown, and no other', () => {
    store.show(5);
    store.listen();
    handler<number>(signalR.lockCaptains)(6);
    expect(store.captainLocked()).toBeFalse();
    handler<number>(signalR.lockCaptains)(5);
    expect(store.captainLocked()).toBeTrue();
  });

  it('reloads after the hub reconnects, but not under a drag: once it ends', () => {
    store.show(5);
    store.listen();
    api.getTeams.calls.reset();
    store.dragStarted();
    handler<void>(signalR.connected)();
    expect(api.getTeams).not.toHaveBeenCalled();

    store.dragEnded();
    expect(api.getTeams).toHaveBeenCalledTimes(1);
  });

  it('locks the captains when unlocked, and unlocks them when locked', () => {
    store.show(5);
    api.lockCaptains.and.returnValue(of(undefined));
    api.unlockCaptains.and.returnValue(of(undefined));
    store.toggleCaptainLock();
    expect(api.lockCaptains).toHaveBeenCalledWith({ seasonID: 5, signalRConnectionID: 'conn' });
    expect(store.captainLocked()).toBeTrue();
    store.toggleCaptainLock();
    expect(api.unlockCaptains).toHaveBeenCalledWith({ seasonID: 5, signalRConnectionID: 'conn' });
    expect(store.captainLocked()).toBeFalse();
  });
});
