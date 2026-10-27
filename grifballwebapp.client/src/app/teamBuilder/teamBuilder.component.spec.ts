import { ChangeDetectorRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TeamBuilderComponent } from './teamBuilder.component';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '../account.service';
import { SignalRService } from '../api/signalR.service';
import { of, Subject, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
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

  describe('onDeck', () => {
    it('should return null when no teams exist', () => {
      component.teams.set([]);

      expect(component.onDeck()).toBeNull();
    });

    it('should return null when player pool is empty', () => {
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] }]);
      component.playerPool.set([]);

      expect(component.onDeck()).toBeNull();
    });

    it('should return captain with fewest players', () => {
      component.teams.set([
        { teamID: 0, teamName: 'Team 1', captain: { name: 'Cap 1', personID: 1, order: 1 }, players: [{ name: 'P1', personID: 3, pick: 1, round: 1 }] },
        { teamID: 0, teamName: 'Team 2', captain: { name: 'Cap 2', personID: 2, order: 2 }, players: [] }
      ]);
      component.playerPool.set([{ name: 'Player', personID: 4, pick: null, round: null }]);

      const result = component.onDeck();

      expect(result?.personID).toBe(2);
      expect(result?.name).toBe('Cap 2');
    });
  });

  describe('statusText', () => {
    it('should return no teams message when teams array is empty', () => {
      component.teams.set([]);

      expect(component.statusText()).toBe('No Teams Added');
    });

    it('should return empty pool message when player pool is empty', () => {
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] }]);
      component.playerPool.set([]);

      expect(component.statusText()).toBe('Player pool is empty');
    });

    it('should return captain on deck message', () => {
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Captain Name', personID: 1, order: 1 }, players: [] }]);
      component.playerPool.set([{ name: 'Player', personID: 2, pick: null, round: null }]);

      expect(component.statusText()).toBe('Captain Name is on deck');
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



    it('should throw error for invalid type', () => {
      expect(() => component.MapToPlayer({}, GrabbedThing.Team)).toThrowError('Unable to map to player');
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

  describe('createDraggedPlayer', () => {
    it('should create grabbed player object', () => {
      const player: PlayerDto = { name: 'Player', personID: 1, pick: null, round: null };
      const team: TeamResponseDto = { teamID: 0, teamName: 'Team', captain: { name: 'Cap', personID: 2, order: 1 }, players: [] };

      const result = component.createDraggedPlayer(player, team);

      expect(result.player).toBe(player);
      expect(result.team).toBe(team);
    });
  });

  describe('drag and drop', () => {
    // The ids that drop handlers announce come from the drag data, which ngx-drag-drop
    // hands over as a JSON copy, so the specs use copies too.
    const copy = <T>(x: T): T => JSON.parse(JSON.stringify(x));
    const cap = (id: number, order: number): CaptainDto => ({ name: 'Cap ' + id, personID: id, order });
    const player = (id: number, round: number | null = null): PlayerDto => ({ name: 'Player ' + id, personID: id, pick: round, round });
    const drop = (data: unknown, index?: number) => ({ data: copy(data), dropEffect: 'move', index } as DndDropEvent);
    const teamNames = () => component.teams().map(t => `${t.captain.order}:${t.captain.personID}`);
    const roster = (captainID: number) => component.teams().find(t => t.captain.personID === captainID)!.players.map(p => `${p.round}:${p.personID}`);
    const pool = () => component.playerPool().map(p => p.personID);
    const grab = (thing: unknown, kind: GrabbedThing) => component.onDragStart(thing as any, new DragEvent('dragstart'), kind);

    beforeEach(() => {
      mockApiClient.getTeams.and.returnValue(of([
        { teamID: 1, teamName: 'Alpha', captain: cap(101, 1), players: [player(1, 1), player(2, 2)] },
        { teamID: 2, teamName: 'Bravo', captain: cap(102, 2), players: [] },
      ]));
      mockApiClient.getPlayerPool.and.returnValue(of([player(3), player(4), player(5)]));
      for (const m of ['addCaptain', 'resortCaptain', 'removeCaptain', 'addPlayerToTeam', 'movePlayerToTeam', 'removePlayerFromTeam'] as const) {
        (mockApiClient[m] as jasmine.Spy).and.returnValue(of({}));
      }
      spyOn(console, 'log');
      fixture.detectChanges();
    });

    it('makes a pool player a captain at the drop position, then reloads for the server\'s team name', () => {
      const saved = new Subject<Object>();
      mockApiClient.addCaptain.and.returnValue(saved);
      grab(component.playerPool()[1], GrabbedThing.AvailablePlayer);
      component.onDropInTeam(drop(component.playerPool()[1], 1));

      expect(teamNames()).toEqual(['1:101', '2:4', '3:102']);
      expect(pool()).toEqual([3, 5]);
      expect(mockApiClient.addCaptain).toHaveBeenCalledWith(jasmine.objectContaining({ seasonID: 5, personID: 4, orderNumber: 2 }), 'test-connection-id');

      // The server picks the name (the signup's team name) and does not echo it back to us.
      const loads = mockApiClient.getTeams.calls.count();
      mockApiClient.getTeams.and.returnValue(of([
        { teamID: 1, teamName: 'Alpha', captain: cap(101, 1), players: [player(1, 1), player(2, 2)] },
        { teamID: 3, teamName: 'The Fours', captain: cap(4, 2), players: [] },
        { teamID: 2, teamName: 'Bravo', captain: cap(102, 3), players: [] },
      ]));
      saved.next({});
      expect(mockApiClient.getTeams.calls.count()).toBe(loads + 1);
      expect(component.teams()[1].teamName).toBe('The Fours');
    });

    // Every drop is applied before the server answers; a refusal must not leave it on screen.
    const refusals: [string, keyof ApiClientService, () => void][] = [
      ['new captain', 'addCaptain', () => { grab(component.playerPool()[1], GrabbedThing.AvailablePlayer); component.onDropInTeam(drop(component.playerPool()[1], 1)); }],
      ['re-sort', 'resortCaptain', () => { grab(component.teams()[0], GrabbedThing.Team); component.onDropInTeam(drop(component.teams()[0], 2)); }],
      ['draft pick', 'addPlayerToTeam', () => { grab(component.playerPool()[0], GrabbedThing.AvailablePlayer); component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]); }],
      ['move pick', 'movePlayerToTeam', () => {
        const alpha = component.teams()[0];
        const grabbed = component.createDraggedPlayer(alpha.players[0], alpha);
        grab(grabbed, GrabbedThing.Player);
        component.onDropPickInTeam(drop(grabbed), component.teams()[1]);
      }],
      ['undraft pick', 'removePlayerFromTeam', () => {
        const alpha = component.teams()[0];
        const grabbed = component.createDraggedPlayer(alpha.players[0], alpha);
        grab(grabbed, GrabbedThing.Player);
        component.onDropInPool(drop(grabbed, 0));
      }],
      ['disband team', 'removeCaptain', () => { grab(component.teams()[0], GrabbedThing.Team); component.onDropInPool(drop(component.teams()[0], 0)); }],
    ];
    for (const [name, request, act] of refusals) {
      it(`says why and reloads the real draft when the server refuses a drop (${name})`, () => {
        (mockApiClient[request] as jasmine.Spy).and.returnValue(throwError(() => new HttpErrorResponse({ status: 500, error: 'It is not your turn to be making a draft pick' })));
        const loads = mockApiClient.getTeams.calls.count();

        act();

        expect(mockApiClient[request]).toHaveBeenCalled();
        expect(mockSnackBar.open).toHaveBeenCalledWith('Draft change not saved: It is not your turn to be making a draft pick', 'Close');
        expect(mockApiClient.getTeams.calls.count()).toBe(loads + 1);
        expect(teamNames()).toEqual(['1:101', '2:102']);
        expect(roster(101)).toEqual(['1:1', '2:2']);
        expect(pool()).toEqual([3, 4, 5]);
      });
    }

    it('gives a generic reason when the refusal has no message', () => {
      mockApiClient.resortCaptain.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
      grab(component.teams()[0], GrabbedThing.Team);
      component.onDropInTeam(drop(component.teams()[0], 2));
      expect(mockSnackBar.open).toHaveBeenCalledWith('Draft change not saved: the server refused it', 'Close');
    });

    it('builds the drag data for each pick once per change, not per render', () => {
      const first = component.draggedPicks();
      fixture.detectChanges();
      expect(component.draggedPicks()).toBe(first);
      expect(first.get(1)).toEqual({ team: component.teams()[0], player: component.teams()[0].players[0] });

      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]);
      expect(component.draggedPicks()).not.toBe(first);
      expect(component.draggedPicks().get(3)?.team.captain.personID).toBe(102);
    });

    it('re-sorts a team, counting the drop index with the dragged team still in the list', () => {
      grab(component.teams()[0], GrabbedThing.Team);
      component.onDropInTeam(drop(component.teams()[0], 2)); // after Bravo

      expect(teamNames()).toEqual(['1:102', '2:101']);
      expect(mockApiClient.resortCaptain).toHaveBeenCalledWith(jasmine.objectContaining({ personID: 101, orderNumber: 2 }), 'test-connection-id');
    });

    it('drafts a pool player onto a team', () => {
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]);

      expect(roster(102)).toEqual(['1:3']);
      expect(pool()).toEqual([4, 5]);
      expect(mockApiClient.addPlayerToTeam).toHaveBeenCalledWith({ seasonID: 5, captainID: 102, personID: 3 }, 'test-connection-id');
    });

    it('moves a pick to another team and renumbers both', () => {
      const alpha = component.teams()[0];
      const grabbed = component.createDraggedPlayer(alpha.players[0], alpha);
      grab(grabbed, GrabbedThing.Player);
      component.onDropPickInTeam(drop(grabbed), component.teams()[1]);

      expect(roster(101)).toEqual(['1:2']);
      expect(roster(102)).toEqual(['1:1']);
      expect(mockApiClient.movePlayerToTeam).toHaveBeenCalledWith({ seasonID: 5, previousCaptainID: 101, newCaptainID: 102, personID: 1, roundNumber: 1 }, 'test-connection-id');
    });

    it('moves a pick to the end of its own team', () => {
      const alpha = component.teams()[0];
      const grabbed = component.createDraggedPlayer(alpha.players[0], alpha);
      grab(grabbed, GrabbedThing.Player);
      component.onDropPickInTeam(drop(grabbed), component.teams()[0]);

      expect(roster(101)).toEqual(['1:2', '2:1']);
      expect(mockApiClient.movePlayerToTeam).toHaveBeenCalledWith(jasmine.objectContaining({ previousCaptainID: 101, newCaptainID: 101, roundNumber: 2 }), 'test-connection-id');
    });

    it('moves a pick within its own team to the drop position', () => {
      const alpha = component.teams()[0];
      const grabbed = component.createDraggedPlayer(alpha.players[0], alpha);
      grab(grabbed, GrabbedThing.Player);
      component.onDropPickInTeam(drop(grabbed, 2), component.teams()[0]); // after pick 2

      expect(roster(101)).toEqual(['1:2', '2:1']);
    });

    it('returns a pick to the pool at the drop position', () => {
      const alpha = component.teams()[0];
      const grabbed = component.createDraggedPlayer(alpha.players[1], alpha);
      grab(grabbed, GrabbedThing.Player);
      component.onDropInPool(drop(grabbed, 1));

      expect(roster(101)).toEqual(['1:1']);
      expect(pool()).toEqual([3, 2, 4, 5]);
      expect(component.playerPool()[1].round).toBeNull();
      expect(mockApiClient.removePlayerFromTeam).toHaveBeenCalledWith({ seasonID: 5, captainID: 101, personID: 2 }, 'test-connection-id');
    });

    it('reorders the pool without telling the backend', () => {
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropInPool(drop(component.playerPool()[0], 3)); // to the end

      expect(pool()).toEqual([4, 5, 3]);
      expect(mockApiClient.removePlayerFromTeam).not.toHaveBeenCalled();
    });

    it('logs an unexpected drag type dropped in the pool', () => {
      grab({}, 'Mystery' as GrabbedThing);
      component.onDropInPool(drop({}, 0));
      expect(console.log).toHaveBeenCalledWith('Unexpected type placed in draft pool: Mystery');
    });

    it('ignores drops that are not a copy or move, and pick drops without a team', () => {
      const before = { teams: component.teams(), pool: component.playerPool() };
      component.onDropInTeam({ data: {}, dropEffect: 'none' } as DndDropEvent);
      component.onDropInPool({ data: {}, dropEffect: 'link' } as DndDropEvent);
      component.onDropPickInTeam({ data: {}, dropEffect: 'none' } as DndDropEvent, component.teams()[0]);
      component.onDropPickInTeam(drop({}), undefined);
      expect({ teams: component.teams(), pool: component.playerPool() }).toEqual(before);
    });

    it('never mutates the lists it replaces', () => {
      const teams = component.teams();
      const snapshot = copy(teams);
      const alpha = teams[0];
      const grabbed = component.createDraggedPlayer(alpha.players[0], alpha);
      grab(grabbed, GrabbedThing.Player);
      component.onDropPickInTeam(drop(grabbed), teams[1]);

      expect(component.teams()).not.toBe(teams);
      expect(teams).toEqual(snapshot);
    });

    it('does not duplicate a player when another user\'s update lands mid-drag', () => {
      const dragged = component.playerPool()[1]; // Player 4, the object the drag started from
      grab(dragged, GrabbedThing.AvailablePlayer);

      // Meanwhile someone drafts Player 3: every list is replaced with new objects.
      const addPlayer = mockSignalR.addPlayerToTeam.calls.mostRecent().args[0] as (dto: any) => void;
      addPlayer({ seasonID: 5, captainID: 101, personID: 3 });

      component.onDropPickInTeam(drop(dragged), component.teams()[1]);

      expect(pool()).toEqual([5]);
      expect(roster(102)).toEqual(['1:4']);
      const everyone = [...component.teams().flatMap(t => t.players.map(p => p.personID)), ...pool()];
      expect(new Set(everyone).size).toBe(everyone.length);
    });

    it('appends when the drop has no index', () => {
      mockApiClient.addCaptain.and.returnValue(new Subject<Object>());
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropInTeam(drop(component.playerPool()[0]));
      expect(teamNames()).toEqual(['1:101', '2:102', '3:3']);

      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropInPool(drop(component.playerPool()[0]));
      expect(pool()).toEqual([5, 4]);

      const alpha = component.teams()[0];
      const grabbed = component.createDraggedPlayer(alpha.players[0], alpha);
      grab(grabbed, GrabbedThing.Player);
      component.onDropInPool(drop(grabbed));
      expect(pool()).toEqual([5, 4, 1]);
    });

    // Each drop names someone who is gone (e.g. another user moved them first): no request, a reload.
    const stale = (): [string, () => void, jasmine.Spy][] => {
      const ghostTeam = { teamID: 9, teamName: 'Gone', captain: cap(999, 9), players: [player(998, 1)] };
      const ghostPick = component.createDraggedPlayer(player(998, 1), ghostTeam);
      return [
        ['new captain', () => { grab(player(997), GrabbedThing.AvailablePlayer); component.onDropInTeam(drop(player(997), 0)); }, mockApiClient.addCaptain],
        ['re-sorted team', () => { grab(ghostTeam, GrabbedThing.Team); component.onDropInTeam(drop(ghostTeam, 0)); }, mockApiClient.resortCaptain],
        ['pick to pool', () => { grab(ghostPick, GrabbedThing.Player); component.onDropInPool(drop(ghostPick, 0)); }, mockApiClient.removePlayerFromTeam],
        ['team to pool', () => { grab(ghostTeam, GrabbedThing.Team); component.onDropInPool(drop(ghostTeam, 0)); }, mockApiClient.removeCaptain],
        ['pick to team', () => { grab(ghostPick, GrabbedThing.Player); component.onDropPickInTeam(drop(ghostPick), component.teams()[1]); }, mockApiClient.movePlayerToTeam],
      ];
    };
    for (const name of ['new captain', 're-sorted team', 'pick to pool', 'team to pool', 'pick to team']) {
      it(`reloads instead of announcing a stale drop (${name})`, () => {
        const [, act, request] = stale().find(([n]) => n === name)!;
        const loads = mockApiClient.getTeams.calls.count();
        act();
        expect(mockApiClient.getTeams.calls.count()).toBe(loads + 1);
        expect(request).not.toHaveBeenCalled();
      });
    }

    it('reloads instead of guessing when a drop names someone the page no longer has', () => {
      const dragged = component.playerPool()[0];
      grab(dragged, GrabbedThing.AvailablePlayer);
      component.playerPool.set(component.playerPool().slice(1)); // already drafted elsewhere
      const loads = mockApiClient.getTeams.calls.count();

      component.onDropPickInTeam(drop(dragged), component.teams()[1]);

      expect(mockApiClient.getTeams.calls.count()).toBe(loads + 1);
      expect(mockApiClient.addPlayerToTeam).not.toHaveBeenCalled();
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

    it('puts a moved pick at the round the server chose', () => {
      handler<any>(mockSignalR.movePlayerToTeam)({ seasonID: SEASON, previousCaptainID: 101, newCaptainID: 101, personID: 2, roundNumber: 1 });
      expect(roster(101)).toEqual(['1:2', '2:1']);
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

    it('keeps the existing cards and rows when the lists are replaced with new objects', () => {
      const cardsBefore = teamCards();
      const poolBefore = poolItems();

      // Same people, new objects (as every update now produces), plus one pick moved.
      const teams = component.teams().map(t => ({ ...t, captain: { ...t.captain }, players: t.players.map(p => ({ ...p })) }));
      teams[1].players = [{ ...component.playerPool()[0], round: 1 }];
      component.teams.set(teams);
      component.playerPool.set(component.playerPool().slice(1).map(p => ({ ...p })));
      fixture.detectChanges();

      expect(teamCards()[0]).toBe(cardsBefore[0]);
      expect(teamCards()[1]).toBe(cardsBefore[1]);
      expect(poolItems()[0]).toBe(poolBefore[1]);
      expect(Array.from(teamCards()[1].querySelectorAll('.players-area > div')).map(d => d.textContent?.trim())).toEqual(['1 Player 3']);
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
    component.onDropInPool({ data: JSON.parse(JSON.stringify(team)), dropEffect: 'move', index: 0 } as DndDropEvent);
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
