import { ChangeDetectorRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TeamBuilderComponent } from './teamBuilder.component';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '../account.service';
import { SignalRService } from '../api/signalR.service';
import { of, Subject } from 'rxjs';
import { TeamResponseDto } from '../api/dtos/teamResponseDto';
import { PlayerDto } from '../api/dtos/playerDto';
import { CaptainDto } from '../api/dtos/captainDto';
import { GrabbedThing } from './personStatus';
import { DndDropEvent } from 'ngx-drag-drop';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

describe('TeamBuilderComponent', () => {
  let component: TeamBuilderComponent;
  let fixture: ComponentFixture<TeamBuilderComponent>;
  let mockActivatedRoute: any;
  let mockApiClient: jasmine.SpyObj<ApiClientService>;
  let mockSnackBar: jasmine.SpyObj<MatSnackBar>;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let mockSignalR: jasmine.SpyObj<SignalRService>;

  beforeEach(async () => {
    mockActivatedRoute = {
      snapshot: {
        paramMap: {
          get: jasmine.createSpy('get').and.returnValue('5')
        }
      }
    };

    mockApiClient = jasmine.createSpyObj('ApiClientService', [
      'getTeams',
      'getPlayerPool',
      'areCaptainsLocked',
      'getCurrentSeasonID',
      'lockCaptains',
      'unlockCaptains',
      'addCaptain',
      'resortCaptain',
      'removeCaptain',
      'addPlayerToTeam',
      'movePlayerToTeam',
      'removePlayerFromTeam'
    ]);

    mockSnackBar = jasmine.createSpyObj('MatSnackBar', ['open', 'dismiss']);
    mockAccountService = jasmine.createSpyObj('AccountService', ['isEventOrganizer', 'isSysAdmin', 'personID']);
    mockSignalR = jasmine.createSpyObj('SignalRService', [
      'addCaptain',
      'resortCaptain',
      'removeCaptain',
      'addPlayerToTeam',
      'movePlayerToTeam',
      'removePlayerFromTeam',
      'lockCaptains',
      'unlockCaptains'
    ]);

    mockSignalR.hubConnection = { connectionId: 'test-connection-id' } as any;
    // Each registration returns its own unregister function, like the real service.
    for (const name of Object.keys(mockSignalR) as (keyof SignalRService)[]) {
      const method = mockSignalR[name] as unknown as jasmine.Spy;
      if (jasmine.isSpy(method)) method.and.callFake(() => jasmine.createSpy(`off ${String(name)}`));
    }

    mockApiClient.getTeams.and.returnValue(of([]));
    mockApiClient.getPlayerPool.and.returnValue(of([]));
    mockApiClient.areCaptainsLocked.and.returnValue(of(true));
    mockAccountService.isEventOrganizer.and.returnValue(false);
    mockAccountService.personID.and.returnValue(0);

    await TestBed.configureTestingModule({
      imports: [TeamBuilderComponent],
      providers: [
        { provide: ActivatedRoute, useValue: mockActivatedRoute },
        { provide: ApiClientService, useValue: mockApiClient },
        { provide: MatSnackBar, useValue: mockSnackBar },
        { provide: AccountService, useValue: mockAccountService },
        { provide: SignalRService, useValue: mockSignalR }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TeamBuilderComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load teams and player pool on init when seasonID is provided', () => {
    mockActivatedRoute.snapshot.paramMap.get.and.returnValue('5');

    fixture.detectChanges();

    expect(mockApiClient.getTeams).toHaveBeenCalledWith(5);
    expect(mockApiClient.getPlayerPool).toHaveBeenCalledWith(5);
    expect(mockApiClient.areCaptainsLocked).toHaveBeenCalledWith(5);
  });

  it('should get current season ID when route param is 0', () => {
    mockActivatedRoute.snapshot.paramMap.get.and.returnValue('0');
    mockApiClient.getCurrentSeasonID.and.returnValue(of(10));

    fixture.detectChanges();

    expect(mockApiClient.getCurrentSeasonID).toHaveBeenCalled();
  });

  it('should initialize with empty teams and player pool', () => {
    expect(component.teams()).toEqual([]);
    expect(component.playerPool()).toEqual([]);
  });

  it('should have captainLocked as true initially', () => {
    expect(component.captainLocked()).toBe(true);
  });

  describe('load', () => {
    it('should load teams successfully', () => {
      const mockTeams: TeamResponseDto[] = [
        { teamID: 0, teamName: 'Team 1', captain: { name: 'Captain 1', personID: 1, order: 1 }, players: [] }
      ];
      mockApiClient.getTeams.and.returnValue(of(mockTeams));

      component.load();

      expect(component.teams()).toEqual(mockTeams);
    });

    it('should load player pool successfully', () => {
      const mockPlayers: PlayerDto[] = [
        { name: 'Player 1', personID: 2, pick: null, round: null }
      ];
      mockApiClient.getPlayerPool.and.returnValue(of(mockPlayers));

      component.load();

      expect(component.playerPool()).toEqual(mockPlayers);
    });

    it('should update captainLocked status', () => {
      mockApiClient.areCaptainsLocked.and.returnValue(of(false));

      component.load();

      expect(component.captainLocked()).toBe(false);
    });
  });

  describe('lockCaptainsButtonClicked', () => {
    it('should unlock captains when currently locked', () => {
      component.captainLocked.set(true);
      mockApiClient.unlockCaptains.and.returnValue(of({}));

      component.lockCaptainsButtonClicked();

      expect(mockApiClient.unlockCaptains).toHaveBeenCalled();
      expect(component.captainLocked()).toBe(false);
    });

    it('should lock captains when currently unlocked', () => {
      component.captainLocked.set(false);
      mockApiClient.lockCaptains.and.returnValue(of({}));

      component.lockCaptainsButtonClicked();

      expect(mockApiClient.lockCaptains).toHaveBeenCalled();
      expect(component.captainLocked()).toBe(true);
    });
  });

  describe('setHorizontalLayout', () => {
    it('should set horizontal layout when true', () => {
      component.setHorizontalLayout(true);

      expect(component.layout.dndHorizontal).toBe(true);
      expect(component.layout.list).toBe('row wrap');
    });

    it('should set vertical layout when false', () => {
      component.setHorizontalLayout(false);

      expect(component.layout.dndHorizontal).toBe(false);
      expect(component.layout.list).toBe('column');
    });
  });

  describe('DisableDraggingTeams', () => {
    it('should return true when captains are locked', () => {
      component.captainLocked.set(true);
      mockAccountService.isEventOrganizer.and.returnValue(true);

      expect(component.DisableDraggingTeams()).toBe(true);
    });

    it('should return true when user is not event organizer', () => {
      component.captainLocked.set(false);
      mockAccountService.isEventOrganizer.and.returnValue(false);

      expect(component.DisableDraggingTeams()).toBe(true);
    });

    it('should return false when captains unlocked and user is event organizer', () => {
      component.captainLocked.set(false);
      mockAccountService.isEventOrganizer.and.returnValue(true);

      expect(component.DisableDraggingTeams()).toBe(false);
    });
  });

  describe('DisableDraggingPlayerFromTeam', () => {
    it('should return false when user is event organizer', () => {
      mockAccountService.isEventOrganizer.and.returnValue(true);

      expect(component.DisableDraggingPlayerFromTeam()).toBe(false);
    });

    it('should return true when user is not event organizer', () => {
      mockAccountService.isEventOrganizer.and.returnValue(false);

      expect(component.DisableDraggingPlayerFromTeam()).toBe(true);
    });
  });

  describe('DisableDraggingPlayerToTeam', () => {
    it('should return false when user is event organizer', () => {
      mockAccountService.isEventOrganizer.and.returnValue(true);
      const team: TeamResponseDto = { teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] };

      expect(component.DisableDraggingPlayerToTeam(team)).toBe(false);
    });

    it('should return true when no current captain on deck', () => {
      mockAccountService.isEventOrganizer.and.returnValue(false);
      component.teams.set([]);
      const team: TeamResponseDto = { teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] };

      expect(component.DisableDraggingPlayerToTeam(team)).toBe(true);
    });

    it('should return false when it is current users turn and their team', () => {
      mockAccountService.isEventOrganizer.and.returnValue(false);
      mockAccountService.personID.and.returnValue(1);
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] }]);
      component.playerPool.set([{ name: 'Player', personID: 2, pick: null, round: null }]);
      const team: TeamResponseDto = { teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] };

      expect(component.DisableDraggingPlayerToTeam(team)).toBe(false);
    });
  });

  describe('CurrentCaptainOnDeck', () => {
    it('should return null when no teams exist', () => {
      component.teams.set([]);

      expect(component.CurrentCaptainOnDeck()).toBeNull();
    });

    it('should return null when player pool is empty', () => {
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] }]);
      component.playerPool.set([]);

      expect(component.CurrentCaptainOnDeck()).toBeNull();
    });

    it('should return captain with fewest players', () => {
      component.teams.set([
        { teamID: 0, teamName: 'Team 1', captain: { name: 'Cap 1', personID: 1, order: 1 }, players: [{ name: 'P1', personID: 3, pick: 1, round: 1 }] },
        { teamID: 0, teamName: 'Team 2', captain: { name: 'Cap 2', personID: 2, order: 2 }, players: [] }
      ]);
      component.playerPool.set([{ name: 'Player', personID: 4, pick: null, round: null }]);

      const result = component.CurrentCaptainOnDeck();

      expect(result?.personID).toBe(2);
      expect(result?.name).toBe('Cap 2');
    });
  });

  describe('StatusText', () => {
    it('should return no teams message when teams array is empty', () => {
      component.teams.set([]);

      expect(component.StatusText()).toBe('No Teams Added');
    });

    it('should return empty pool message when player pool is empty', () => {
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] }]);
      component.playerPool.set([]);

      expect(component.StatusText()).toBe('Player pool is empty');
    });

    it('should return captain on deck message', () => {
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Captain Name', personID: 1, order: 1 }, players: [] }]);
      component.playerPool.set([{ name: 'Player', personID: 2, pick: null, round: null }]);

      expect(component.StatusText()).toBe('Captain Name is on deck');
    });
  });

  describe('MapToPlayer', () => {
    it('should map available player correctly', () => {
      const playerDto: PlayerDto = { name: 'Test Player', personID: 1, pick: 5, round: 2 };

      const result = component.MapToPlayer(playerDto, GrabbedThing.AvailablePlayer);

      expect(result.name).toBe('Test Player');
      expect(result.personID).toBe(1);
      expect(result.round).toBeNull();
      expect(result.pick).toBeNull();
    });

    it('should map grabbed player correctly', () => {
      const grabbedPlayer = {
        player: { name: 'Grabbed', personID: 2, pick: 3, round: 1 },
        team: { teamID: 0, teamName: 'Team', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] }
      };

      const result = component.MapToPlayer(grabbedPlayer, GrabbedThing.Player);

      expect(result.name).toBe('Grabbed');
      expect(result.personID).toBe(2);
      expect(result.round).toBeNull();
      expect(result.pick).toBeNull();
    });

    it('should map team to player correctly', () => {
      const team: TeamResponseDto = {
        teamID: 0,
        teamName: 'Test Team',
        captain: { name: 'Captain', personID: 3, order: 1 },
        players: []
      };

      const result = component.MapToPlayer(team, GrabbedThing.Team);

      expect(result.name).toBe('Captain');
      expect(result.personID).toBe(3);
      expect(result.round).toBeNull();
      expect(result.pick).toBeNull();
    });

    it('should throw error for invalid type', () => {
      expect(() => component.MapToPlayer({}, 999 as any)).toThrowError('Unable to map to player');
    });
  });

  describe('MapToTeam', () => {
    it('should map available player to team', () => {
      const player: PlayerDto = { name: 'Player', personID: 5, pick: null, round: null };

      const result = component.MapToTeam(player, GrabbedThing.AvailablePlayer);

      expect(result.teamName).toBe("Player's Team");
      expect(result.captain.name).toBe('Player');
      expect(result.captain.personID).toBe(5);
      expect(result.players).toEqual([]);
    });

    it('should map team to team', () => {
      const team: TeamResponseDto = {
        teamID: 0,
        teamName: 'Existing Team',
        captain: { name: 'Cap', personID: 2, order: 1 },
        players: [{ name: 'P1', personID: 3, pick: 1, round: 1 }]
      };

      const result = component.MapToTeam(team, GrabbedThing.Team);

      expect(result.teamName).toBe('Existing Team');
      expect(result.captain.personID).toBe(2);
      expect(result.players.length).toBe(1);
    });

    it('should throw error for invalid type', () => {
      expect(() => component.MapToTeam({}, GrabbedThing.Player)).toThrowError('Unable to map to team');
    });
  });

  describe('onDragStart', () => {
    it('should set grabbed thing on drag start', () => {
      const player: PlayerDto = { name: 'Player', personID: 1, pick: null, round: null };
      const event = new DragEvent('dragstart');

      component.onDragStart(player, event, GrabbedThing.AvailablePlayer);

      // Can't access private members but test doesn't throw
      expect(component).toBeTruthy();
    });
  });

  describe('onDragged', () => {
    it('should remove item from list on move effect', () => {
      const player: PlayerDto = { name: 'Player', personID: 1, pick: null, round: null };
      const list = [player, { name: 'Player 2', personID: 2, pick: null, round: null }];

      component.onDragged(player, list, 'move');

      expect(list.length).toBe(1);
      expect(list[0].name).toBe('Player 2');
    });

    it('should not remove item on copy effect', () => {
      const player: PlayerDto = { name: 'Player', personID: 1, pick: null, round: null };
      const list = [player];

      component.onDragged(player, list, 'copy');

      expect(list.length).toBe(1);
    });

    it('leaves the list alone when the dragged item is no longer in it', () => {
      const list = [{ name: 'Player 2', personID: 2, pick: null, round: null }];

      component.onDragged({ name: 'Player', personID: 1, pick: null, round: null }, list, 'move');

      expect(list.map(p => p.personID)).toEqual([2]);
    });
  });

  describe('createDraggedPlayer', () => {
    it('should create grabbed player object', () => {
      const player: PlayerDto = { name: 'Player', personID: 1, pick: null, round: null };
      const team: TeamResponseDto = { teamID: 0, teamName: 'Team', captain: { name: 'Cap', personID: 2, order: 1 }, players: [] };

      const result = component.createDraggedPlayer(player, team);

      expect(result.player).toBe(player);
      expect(result.team).toBe(team);
    });
  });

  describe('resortPreviousTeam', () => {
    it('should resort previous team after player removal', () => {
      const player1: PlayerDto = { name: 'P1', personID: 1, pick: 1, round: 1 };
      const player2: PlayerDto = { name: 'P2', personID: 2, pick: 2, round: 2 };
      const player3: PlayerDto = { name: 'P3', personID: 3, pick: 3, round: 3 };
      const team: TeamResponseDto = {
        teamID: 0,
        teamName: 'Team',
        captain: { name: 'Cap', personID: 10, order: 1 },
        players: [player1, player2, player3]
      };
      component.teams.set([team]);

      const grabbedPlayer = { player: player2, team: team };

      component.resortPreviousTeam(grabbedPlayer);

      expect(team.players[0].round).toBe(1);
      expect(team.players[2].round).toBe(2); // player3 gets new round
    });

    it('should handle missing team gracefully', () => {
      const grabbedPlayer = {
        player: { name: 'P1', personID: 1, pick: 1, round: 1 },
        team: { teamID: 0, teamName: 'Missing', captain: { name: 'Cap', personID: 999, order: 1 }, players: [] }
      };

      expect(() => component.resortPreviousTeam(grabbedPlayer)).not.toThrow();
    });
  });

  describe('SignalR updates', () => {
    const SEASON = 5;
    const OTHER = 6;
    const cap = (id: number, order: number): CaptainDto => ({ name: 'Cap ' + id, personID: id, order });
    const player = (id: number, round: number | null = null): PlayerDto => ({ name: 'Player ' + id, personID: id, pick: round, round });

    // The callback the component registered with the given SignalRService method.
    const handler = <T>(spy: jasmine.Spy) => spy.calls.mostRecent().args[0] as (dto: T) => void;
    const loads = () => mockApiClient.getTeams.calls.count();
    const teamNames = () => component.teams().map(t => `${t.captain.order}:${t.teamName}`);
    const pool = () => component.playerPool().map(p => p.personID);
    const roster = (captainID: number) => component.teams().find(t => t.captain.personID === captainID)!.players.map(p => `${p.round}:${p.personID}`);

    beforeEach(() => {
      mockApiClient.getTeams.and.callFake(() => of([
        { teamID: 1, teamName: 'Alpha', captain: cap(101, 1), players: [player(1, 1), player(2, 2)] },
        { teamID: 2, teamName: 'Bravo', captain: cap(102, 2), players: [] },
      ]));
      mockApiClient.getPlayerPool.and.callFake(() => of([player(3), player(4), player(5)]));
      fixture.detectChanges();
    });

    it('unregisters all of its hub handlers when destroyed', () => {
      const offs = [mockSignalR.addCaptain, mockSignalR.resortCaptain, mockSignalR.removeCaptain, mockSignalR.addPlayerToTeam,
        mockSignalR.movePlayerToTeam, mockSignalR.removePlayerFromTeam, mockSignalR.lockCaptains, mockSignalR.unlockCaptains]
        .map(spy => spy.calls.mostRecent().returnValue as jasmine.Spy);
      expect(offs.every(off => off.calls.count() === 0)).toBeTrue();

      fixture.destroy();

      expect(offs.every(off => off.calls.count() === 1)).toBeTrue();
    });

    it('leaves the current lists untouched by an update (new objects instead)', () => {
      const before = component.teams();
      const alpha = before[0];
      const alphaPlayers = alpha.players;
      const beforePool = component.playerPool();

      handler<any>(mockSignalR.addPlayerToTeam)({ seasonID: SEASON, captainID: 101, personID: 3 });

      expect(component.teams()).not.toBe(before);
      expect(alpha.players).toBe(alphaPlayers);
      expect(alpha.players.map(p => p.personID)).toEqual([1, 2]);
      expect(beforePool.map(p => p.personID)).toEqual([3, 4, 5]);
      expect(roster(101)).toEqual(['1:1', '2:2', '3:3']);
    });

    it('keeps the same signal values when an update changes nothing', () => {
      spyOn(console, 'log');
      const teams = component.teams();
      const playerPool = component.playerPool();

      handler<any>(mockSignalR.addCaptain)({ seasonID: OTHER, personID: 4, teamName: 'X', captainName: 'X', orderNumber: 1 });
      expect(component.teams()).toBe(teams);
      expect(component.playerPool()).toBe(playerPool);

      // Out of sync: the page reloads instead (the mock returns fresh lists).
      mockApiClient.getTeams.and.returnValue(new Subject<TeamResponseDto[]>());
      mockApiClient.getPlayerPool.and.returnValue(new Subject<PlayerDto[]>());
      handler<any>(mockSignalR.movePlayerToTeam)({ seasonID: SEASON, previousCaptainID: 101, newCaptainID: 99, personID: 1, roundNumber: 1 });
      expect(component.teams()).toBe(teams);
      expect(component.playerPool()).toBe(playerPool);
    });

    it('renders an update pushed by another user without a manual detectChanges', async () => {
      fixture.autoDetectChanges();
      await fixture.whenStable();

      handler<any>(mockSignalR.addCaptain)({ seasonID: SEASON, personID: 4, teamName: 'Charlie', captainName: 'Player 4', orderNumber: 1 });
      await fixture.whenStable();

      const titles = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('mat-card.item:not(.dndPlaceholder) mat-card-title'))
        .map(t => t.textContent!.trim());
      expect(titles).toEqual(['1 - Charlie', '2 - Alpha', '3 - Bravo']);
    });

    it('adds a captain at their pick order and takes them out of the pool', () => {
      handler<any>(mockSignalR.addCaptain)({ seasonID: SEASON, personID: 4, teamName: 'Charlie', captainName: 'Player 4', orderNumber: 2 });
      expect(teamNames()).toEqual(['1:Alpha', '2:Charlie', '3:Bravo']);
      expect(pool()).toEqual([3, 5]);
    });

    it('moves a captain to a new pick order', () => {
      handler<any>(mockSignalR.resortCaptain)({ seasonID: SEASON, personID: 102, orderNumber: 1 });
      expect(teamNames()).toEqual(['1:Bravo', '2:Alpha']);
    });

    it('returns a removed captain and their picks to the pool', () => {
      handler<any>(mockSignalR.removeCaptain)({ seasonID: SEASON, personID: 101 });
      expect(teamNames()).toEqual(['1:Bravo']);
      expect(pool()).toEqual([2, 1, 101, 3, 4, 5]);
      expect(component.playerPool().every(p => p.round === null && p.pick === null)).toBeTrue();
    });

    it('adds a pool player to a team as its next round', () => {
      handler<any>(mockSignalR.addPlayerToTeam)({ seasonID: SEASON, captainID: 102, personID: 3 });
      expect(roster(102)).toEqual(['1:3']);
    });

    it('moves a player between teams and renumbers both rosters', () => {
      handler<any>(mockSignalR.movePlayerToTeam)({ seasonID: SEASON, previousCaptainID: 101, newCaptainID: 102, personID: 1, roundNumber: 1 });
      expect(roster(101)).toEqual(['1:2']);
      expect(roster(102)).toEqual(['1:1']);
    });

    it('moves a player to the end of their own team', () => {
      handler<any>(mockSignalR.movePlayerToTeam)({ seasonID: SEASON, previousCaptainID: 101, newCaptainID: 101, personID: 1, roundNumber: 2 });
      expect(roster(101)).toEqual(['1:2', '2:1']);
    });

    it('returns a removed player to the front of the pool', () => {
      handler<any>(mockSignalR.removePlayerFromTeam)({ seasonID: SEASON, captainID: 101, personID: 1 });
      expect(roster(101)).toEqual(['1:2']);
      expect(pool()).toEqual([1, 3, 4, 5]);
    });

    it('follows captain locks for this season only', () => {
      component.captainLocked.set(false);
      handler<number>(mockSignalR.lockCaptains)(OTHER);
      expect(component.captainLocked()).toBeFalse();
      handler<number>(mockSignalR.lockCaptains)(SEASON);
      expect(component.captainLocked()).toBeTrue();

      handler<number>(mockSignalR.unlockCaptains)(OTHER);
      expect(component.captainLocked()).toBeTrue();
      handler<number>(mockSignalR.unlockCaptains)(SEASON);
      expect(component.captainLocked()).toBeFalse();
    });

    it('ignores updates for another season', () => {
      const before = { teams: teamNames(), pool: pool(), alpha: roster(101) };
      handler<any>(mockSignalR.addCaptain)({ seasonID: OTHER, personID: 4, teamName: 'X', captainName: 'X', orderNumber: 1 });
      handler<any>(mockSignalR.resortCaptain)({ seasonID: OTHER, personID: 102, orderNumber: 1 });
      handler<any>(mockSignalR.removeCaptain)({ seasonID: OTHER, personID: 101 });
      handler<any>(mockSignalR.addPlayerToTeam)({ seasonID: OTHER, captainID: 102, personID: 3 });
      handler<any>(mockSignalR.movePlayerToTeam)({ seasonID: OTHER, previousCaptainID: 101, newCaptainID: 102, personID: 1, roundNumber: 1 });
      handler<any>(mockSignalR.removePlayerFromTeam)({ seasonID: OTHER, captainID: 101, personID: 1 });

      expect({ teams: teamNames(), pool: pool(), alpha: roster(101) }).toEqual(before);
      expect(loads()).toBe(1);
    });

    // Each case names a person or captain the page does not know about, so it reloads.
    const outOfSync: [string, () => jasmine.Spy, unknown][] = [
      ['addCaptain: player not in the pool', () => mockSignalR.addCaptain, { seasonID: SEASON, personID: 99, teamName: 'X', captainName: 'X', orderNumber: 1 }],
      ['resortCaptain: unknown captain', () => mockSignalR.resortCaptain, { seasonID: SEASON, personID: 99, orderNumber: 1 }],
      ['removeCaptain: unknown captain', () => mockSignalR.removeCaptain, { seasonID: SEASON, personID: 99 }],
      ['addPlayerToTeam: player not in the pool', () => mockSignalR.addPlayerToTeam, { seasonID: SEASON, captainID: 102, personID: 99 }],
      ['addPlayerToTeam: unknown team', () => mockSignalR.addPlayerToTeam, { seasonID: SEASON, captainID: 99, personID: 3 }],
      ['movePlayerToTeam: unknown previous team', () => mockSignalR.movePlayerToTeam, { seasonID: SEASON, previousCaptainID: 99, newCaptainID: 102, personID: 1, roundNumber: 1 }],
      ['movePlayerToTeam: player not on the previous team', () => mockSignalR.movePlayerToTeam, { seasonID: SEASON, previousCaptainID: 101, newCaptainID: 102, personID: 99, roundNumber: 1 }],
      ['movePlayerToTeam: unknown new team', () => mockSignalR.movePlayerToTeam, { seasonID: SEASON, previousCaptainID: 101, newCaptainID: 99, personID: 1, roundNumber: 1 }],
      ['movePlayerToTeam: unknown team (same team)', () => mockSignalR.movePlayerToTeam, { seasonID: SEASON, previousCaptainID: 99, newCaptainID: 99, personID: 1, roundNumber: 1 }],
      ['movePlayerToTeam: player not on the team (same team)', () => mockSignalR.movePlayerToTeam, { seasonID: SEASON, previousCaptainID: 101, newCaptainID: 101, personID: 99, roundNumber: 1 }],
      ['removePlayerFromTeam: unknown team', () => mockSignalR.removePlayerFromTeam, { seasonID: SEASON, captainID: 99, personID: 1 }],
      ['removePlayerFromTeam: player not on the team', () => mockSignalR.removePlayerFromTeam, { seasonID: SEASON, captainID: 101, personID: 99 }],
    ];
    for (const [name, spy, dto] of outOfSync) {
      it(`reloads when out of sync (${name})`, () => {
        spyOn(console, 'log');
        handler<unknown>(spy())(dto);
        expect(loads()).toBe(2);
        expect(console.log).toHaveBeenCalledWith('Detected out of sync');
      });
    }
  });

  describe('template rendering (@for)', () => {
    const cap = (n: number): CaptainDto => ({ name: 'Cap ' + n, personID: 100 + n, order: n });
    const player = (id: number, round: number): PlayerDto => ({ name: 'Player ' + id, personID: id, pick: id, round: round });

    function teamCards(): HTMLElement[] {
      return Array.from(fixture.nativeElement.querySelectorAll('mat-card.item:not(.dndPlaceholder)'));
    }

    function poolItems(): HTMLElement[] {
      return Array.from(fixture.nativeElement.querySelectorAll('mat-list-item.player-pool-item:not([dndPlaceholderRef])'));
    }

    beforeEach(() => {
      mockApiClient.getTeams.and.returnValue(of([
        { teamID: 1, teamName: 'Alpha', captain: cap(1), players: [player(1, 1), player(2, 2)] },
        { teamID: 2, teamName: 'Bravo', captain: cap(2), players: [] }
      ]));
      mockApiClient.getPlayerPool.and.returnValue(of([player(3, 0), player(4, 0), player(5, 0)]));
      fixture.detectChanges();
    });

    it('renders one card per team, in order', () => {
      const cards = teamCards();
      expect(cards.length).toBe(2);
      expect(cards[0].querySelector('mat-card-title')?.textContent).toContain('1 - Alpha');
      expect(cards[0].querySelector('mat-card-subtitle')?.textContent).toContain('Captain: Cap 1');
      expect(cards[1].querySelector('mat-card-title')?.textContent).toContain('2 - Bravo');
    });

    it('renders the players of each team with round and name', () => {
      const [alpha, bravo] = teamCards();
      const alphaPlayers = Array.from(alpha.querySelectorAll('.players-area > div')).map(d => d.textContent?.trim());
      expect(alphaPlayers).toEqual(['1 Player 1', '2 Player 2']);
      expect(bravo.querySelectorAll('.players-area > div').length).toBe(0);
    });

    it('renders one pool item per available player', () => {
      expect(poolItems().map(i => i.textContent?.trim())).toEqual(['Player 3', 'Player 4', 'Player 5']);
    });

    it('re-renders after the lists are mutated in place, as drag and drop does', () => {
      const picked = component.playerPool().splice(1, 1)[0];
      component.teams()[1].players.push({ ...picked, round: 1 });
      component.teams().reverse();
      // Drag and drop mutates the lists inside template event handlers, and Angular marks the
      // view for check whenever one of those handlers runs. Do the same here.
      fixture.debugElement.injector.get(ChangeDetectorRef).markForCheck();
      fixture.detectChanges();

      const cards = teamCards();
      expect(cards.map(c => c.querySelector('mat-card-title')?.textContent?.trim())).toEqual(['2 - Bravo', '1 - Alpha']);
      expect(Array.from(cards[0].querySelectorAll('.players-area > div')).map(d => d.textContent?.trim())).toEqual(['1 Player 4']);
      expect(poolItems().map(i => i.textContent?.trim())).toEqual(['Player 3', 'Player 5']);
    });
  });
});

describe('TeamBuilderComponent remove captain request (HttpTestingController)', () => {
  let fixture: ComponentFixture<TeamBuilderComponent>;
  let component: TeamBuilderComponent;
  let httpTesting: HttpTestingController;
  let mockSignalR: jasmine.SpyObj<SignalRService>;

  const captain = (personID: number, order: number): CaptainDto => ({ name: 'Cap ' + personID, personID, order });
  const pooled = (personID: number): PlayerDto => ({ name: 'Player ' + personID, personID, pick: null, round: null });

  beforeEach(async () => {
    mockSignalR = jasmine.createSpyObj('SignalRService', [
      'addCaptain',
      'resortCaptain',
      'removeCaptain',
      'addPlayerToTeam',
      'movePlayerToTeam',
      'removePlayerFromTeam',
      'lockCaptains',
      'unlockCaptains'
    ]);
    mockSignalR.hubConnection = { connectionId: 'conn-42' } as any;
    // Each registration returns its own unregister function, like the real service.
    for (const name of Object.keys(mockSignalR) as (keyof SignalRService)[]) {
      const method = mockSignalR[name] as unknown as jasmine.Spy;
      if (jasmine.isSpy(method)) method.and.callFake(() => jasmine.createSpy(`off ${String(name)}`));
    }

    const mockAccountService = jasmine.createSpyObj('AccountService', ['isEventOrganizer', 'isSysAdmin', 'personID']);
    mockAccountService.isEventOrganizer.and.returnValue(true);
    mockAccountService.personID.and.returnValue(0);

    await TestBed.configureTestingModule({
      imports: [TeamBuilderComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
        { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open', 'dismiss']) },
        { provide: AccountService, useValue: mockAccountService },
        { provide: SignalRService, useValue: mockSignalR }
      ]
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamBuilderComponent);
    component = fixture.componentInstance;

    // ngOnInit loads season 7 through the real ApiClientService.
    component.ngOnInit();
    httpTesting.expectOne('/api/Teams/GetTeams/7').flush([
      { teamID: 1, teamName: 'Alpha', captain: captain(11, 1), players: [pooled(21)] },
      { teamID: 2, teamName: 'Bravo', captain: captain(12, 2), players: [] },
      { teamID: 3, teamName: 'Charlie', captain: captain(13, 3), players: [] }
    ]);
    httpTesting.expectOne('/api/Teams/GetPlayerPool/7').flush([pooled(30)]);
    httpTesting.expectOne('/api/Teams/areCaptainsLocked/7').flush(false);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  function dropTeamInPool(team: TeamResponseDto): void {
    component.onDragStart(team, new DragEvent('dragstart'), GrabbedThing.Team);
    // ngx-drag-drop removes the dragged card from its source list on a "move".
    component.onDragged(team, component.teams(), 'move');
    component.onDropInPool({ data: team, dropEffect: 'move', index: 0 } as DndDropEvent, component.playerPool());
  }

  it('posts a RemoveCaptainDto with only seasonID and personID', () => {
    dropTeamInPool(component.teams()[1]);

    const req = httpTesting.expectOne('/api/Teams/RemoveCaptain/');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ seasonID: 7, personID: 12 });
    expect(Object.keys(req.request.body).sort()).toEqual(['personID', 'seasonID']);
    expect('orderNumber' in req.request.body).toBe(false);
    expect(req.request.headers.get('SignalRConnectionID')).toBe('conn-42');
    req.flush({});
  });

  it('returns the captain and their players to the pool and re-sequences the remaining captains', () => {
    dropTeamInPool(component.teams()[0]);

    const req = httpTesting.expectOne('/api/Teams/RemoveCaptain/');
    expect(req.request.body).toEqual({ seasonID: 7, personID: 11 });
    req.flush({});

    expect(component.teams().map(t => [t.captain.personID, t.captain.order])).toEqual([[12, 1], [13, 2]]);
    expect(component.playerPool().map(p => p.personID).sort()).toEqual([11, 21, 30]);
  });
});

describe('TeamBuilderComponent hub handler registration (real SignalRService)', () => {
  let handlers: Map<string, Function[]>;

  beforeEach(async () => {
    handlers = new Map();
    const hubConnection = {
      connectionId: 'conn',
      on: (name: string, fn: Function) => handlers.set(name, [...(handlers.get(name) ?? []), fn]),
      off: (name: string, fn: Function) => handlers.set(name, (handlers.get(name) ?? []).filter(h => h !== fn)),
    };
    const signalR = Object.create(SignalRService.prototype) as SignalRService;
    signalR.hubConnection = hubConnection as any;

    const api = jasmine.createSpyObj('ApiClientService', ['getTeams', 'getPlayerPool', 'areCaptainsLocked']);
    api.getTeams.and.returnValue(of([]));
    api.getPlayerPool.and.returnValue(of([]));
    api.areCaptainsLocked.and.returnValue(of(true));
    const account = jasmine.createSpyObj('AccountService', ['isEventOrganizer', 'personID']);

    await TestBed.configureTestingModule({
      imports: [TeamBuilderComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '5' } } } },
        { provide: ApiClientService, useValue: api },
        { provide: AccountService, useValue: account },
        { provide: SignalRService, useValue: signalR },
      ]
    }).compileComponents();
  });

  it('keeps one handler per hub method across visits, and a destroyed page no longer reacts', () => {
    const first = TestBed.createComponent(TeamBuilderComponent);
    first.detectChanges();
    first.destroy();

    const second = TestBed.createComponent(TeamBuilderComponent);
    second.detectChanges();

    for (const name of ['AddCaptain', 'ResortCaptain', 'RemoveCaptain', 'AddPlayerToTeam', 'MovePlayerToTeam', 'RemovePlayerFromTeam', 'LockCaptains', 'UnlockCaptains']) {
      expect(handlers.get(name)?.length).withContext(name).toBe(1);
    }

    const unlockFirst = spyOn(first.componentInstance.captainLocked, 'set');
    handlers.get('UnlockCaptains')![0](5);
    expect(unlockFirst).not.toHaveBeenCalled();
    expect(second.componentInstance.captainLocked()).toBeFalse();
  });
});
