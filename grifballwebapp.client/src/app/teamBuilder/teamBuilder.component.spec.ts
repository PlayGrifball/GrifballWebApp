import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TeamBuilderComponent } from './teamBuilder.component';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '../account.service';
import { SignalRService } from '../api/signalR.service';
import { of } from 'rxjs';
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
    expect(component.teams).toEqual([]);
    expect(component.playerPool).toEqual([]);
  });

  it('should have captainLocked as true initially', () => {
    expect(component.captainLocked).toBe(true);
  });

  describe('load', () => {
    it('should load teams successfully', () => {
      const mockTeams: TeamResponseDto[] = [
        { teamID: 0, teamName: 'Team 1', captain: { name: 'Captain 1', personID: 1, order: 1 }, players: [] }
      ];
      mockApiClient.getTeams.and.returnValue(of(mockTeams));

      component.load();

      expect(component.teams).toEqual(mockTeams);
    });

    it('should load player pool successfully', () => {
      const mockPlayers: PlayerDto[] = [
        { name: 'Player 1', personID: 2, pick: null, round: null }
      ];
      mockApiClient.getPlayerPool.and.returnValue(of(mockPlayers));

      component.load();

      expect(component.playerPool).toEqual(mockPlayers);
    });

    it('should update captainLocked status', () => {
      mockApiClient.areCaptainsLocked.and.returnValue(of(false));

      component.load();

      expect(component.captainLocked).toBe(false);
    });
  });

  describe('lockCaptainsButtonClicked', () => {
    it('should unlock captains when currently locked', () => {
      component.captainLocked = true;
      mockApiClient.unlockCaptains.and.returnValue(of({}));

      component.lockCaptainsButtonClicked();

      expect(mockApiClient.unlockCaptains).toHaveBeenCalled();
      expect(component.captainLocked).toBe(false);
    });

    it('should lock captains when currently unlocked', () => {
      component.captainLocked = false;
      mockApiClient.lockCaptains.and.returnValue(of({}));

      component.lockCaptainsButtonClicked();

      expect(mockApiClient.lockCaptains).toHaveBeenCalled();
      expect(component.captainLocked).toBe(true);
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
      component.captainLocked = true;
      mockAccountService.isEventOrganizer.and.returnValue(true);

      expect(component.DisableDraggingTeams()).toBe(true);
    });

    it('should return true when user is not event organizer', () => {
      component.captainLocked = false;
      mockAccountService.isEventOrganizer.and.returnValue(false);

      expect(component.DisableDraggingTeams()).toBe(true);
    });

    it('should return false when captains unlocked and user is event organizer', () => {
      component.captainLocked = false;
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
      component.teams = [];
      const team: TeamResponseDto = { teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] };

      expect(component.DisableDraggingPlayerToTeam(team)).toBe(true);
    });

    it('should return false when it is current users turn and their team', () => {
      mockAccountService.isEventOrganizer.and.returnValue(false);
      mockAccountService.personID.and.returnValue(1);
      component.teams = [{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] }];
      component.playerPool = [{ name: 'Player', personID: 2, pick: null, round: null }];
      const team: TeamResponseDto = { teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] };

      expect(component.DisableDraggingPlayerToTeam(team)).toBe(false);
    });
  });

  describe('CurrentCaptainOnDeck', () => {
    it('should return null when no teams exist', () => {
      component.teams = [];

      expect(component.CurrentCaptainOnDeck()).toBeNull();
    });

    it('should return null when player pool is empty', () => {
      component.teams = [{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] }];
      component.playerPool = [];

      expect(component.CurrentCaptainOnDeck()).toBeNull();
    });

    it('should return captain with fewest players', () => {
      component.teams = [
        { teamID: 0, teamName: 'Team 1', captain: { name: 'Cap 1', personID: 1, order: 1 }, players: [{ name: 'P1', personID: 3, pick: 1, round: 1 }] },
        { teamID: 0, teamName: 'Team 2', captain: { name: 'Cap 2', personID: 2, order: 2 }, players: [] }
      ];
      component.playerPool = [{ name: 'Player', personID: 4, pick: null, round: null }];

      const result = component.CurrentCaptainOnDeck();

      expect(result?.personID).toBe(2);
      expect(result?.name).toBe('Cap 2');
    });
  });

  describe('StatusText', () => {
    it('should return no teams message when teams array is empty', () => {
      component.teams = [];

      expect(component.StatusText()).toBe('No Teams Added');
    });

    it('should return empty pool message when player pool is empty', () => {
      component.teams = [{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1 }, players: [] }];
      component.playerPool = [];

      expect(component.StatusText()).toBe('Player pool is empty');
    });

    it('should return captain on deck message', () => {
      component.teams = [{ teamID: 0, teamName: 'Test', captain: { name: 'Captain Name', personID: 1, order: 1 }, players: [] }];
      component.playerPool = [{ name: 'Player', personID: 2, pick: null, round: null }];

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
      component.teams = [team];

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
      const picked = component.playerPool.splice(1, 1)[0];
      component.teams[1].players.push({ ...picked, round: 1 });
      component.teams.reverse();
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
    component.onDragged(team, component.teams, 'move');
    component.onDropInPool({ data: team, dropEffect: 'move', index: 0 } as DndDropEvent, component.playerPool);
  }

  it('posts a RemoveCaptainDto with only seasonID and personID', () => {
    dropTeamInPool(component.teams[1]);

    const req = httpTesting.expectOne('/api/Teams/RemoveCaptain/');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ seasonID: 7, personID: 12 });
    expect(Object.keys(req.request.body).sort()).toEqual(['personID', 'seasonID']);
    expect('orderNumber' in req.request.body).toBe(false);
    expect(req.request.headers.get('SignalRConnectionID')).toBe('conn-42');
    req.flush({});
  });

  it('returns the captain and their players to the pool and re-sequences the remaining captains', () => {
    dropTeamInPool(component.teams[0]);

    const req = httpTesting.expectOne('/api/Teams/RemoveCaptain/');
    expect(req.request.body).toEqual({ seasonID: 7, personID: 11 });
    req.flush({});

    expect(component.teams.map(t => [t.captain.personID, t.captain.order])).toEqual([[12, 1], [13, 2]]);
    expect(component.playerPool.map(p => p.personID).sort()).toEqual([11, 21, 30]);
  });
});
