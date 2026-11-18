import type { CaptainAddedResponseDto, DraftLiveDto } from '@api/models';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TeamBuilderComponent } from './teamBuilder.component';
import { provideRouter } from '@angular/router';
import { SeasonService } from '@api/services/season.service';
import { TeamsService } from '@api/services/teams.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AccountService } from '@core/auth/account.service';
import { LiveService, type LiveMessage } from '@core/live/live.service';
import { LIVE_RELOAD_MS, LIVE_SPREAD_MS } from '@core/live/liveReload';
import { config as rxjsConfig, of, Subject, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import type { TeamResponseDto } from '@api/models';
import type { TeamsPlayerDto } from '@api/models';
import type { CaptainDto } from '@api/models';
import { GrabbedThing, GrabbedThingTypes } from './personStatus';
import { DndDropEvent } from 'ngx-drag-drop';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

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

describe('TeamBuilderComponent', () => {
  let component: TeamBuilderComponent;
  let fixture: ComponentFixture<TeamBuilderComponent>;
  let mockDraft: jasmine.SpyObj<TeamsService>;
  let mockSeason: jasmine.SpyObj<SeasonService>;
  let mockSnackBar: jasmine.SpyObj<MatSnackBar>;
  let mockAccountService: jasmine.SpyObj<AccountService>;
  let live: FakeLive;

  beforeEach(async () => {
    mockDraft = jasmine.createSpyObj<TeamsService>('TeamsService', [
      'getTeams',
      'getPlayerPool',
      'areCaptainsLocked',
      'lockCaptains',
      'unlockCaptains',
      'addCaptain',
      'resortCaptain',
      'removeCaptain',
      'addPlayerToTeam',
      'movePlayerToTeam',
      'removePlayerFromTeam'
    ]);
    mockSeason = jasmine.createSpyObj<SeasonService>('SeasonService', ['getCurrentSeasonId']);

    mockSnackBar = jasmine.createSpyObj('MatSnackBar', ['open', 'dismiss']);
    mockAccountService = jasmine.createSpyObj('AccountService', ['isEventOrganizer', 'isSysAdmin', 'personID']);
    live = new FakeLive();

    mockDraft.getTeams.and.returnValue(of([]));
    mockDraft.getPlayerPool.and.returnValue(of([]));
    mockDraft.areCaptainsLocked.and.returnValue(of(true));
    mockAccountService.isEventOrganizer.and.returnValue(false);
    mockAccountService.personID.and.returnValue(0);

    await TestBed.configureTestingModule({
      imports: [TeamBuilderComponent],
      providers: [
        provideRouter([]),
        { provide: TeamsService, useValue: mockDraft },
        { provide: SeasonService, useValue: mockSeason },
        { provide: MatSnackBar, useValue: mockSnackBar },
        { provide: AccountService, useValue: mockAccountService },
        { provide: LiveService, useValue: live }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TeamBuilderComponent);
    component = fixture.componentInstance;
    // As the router gives it: the :seasonID of the route, as text
    fixture.componentRef.setInput('seasonID', '5');
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('watches the draft only once the page initialises, in the pass that sets its season', () => {
    // Created but not yet drawn: the season isn't set yet, so there is nothing to watch
    expect(live.active).toEqual([]);

    fixture.detectChanges();
    expect(live.active).toEqual([['draft:5']]);
    expect(mockDraft.getTeams).not.toHaveBeenCalledWith({ seasonID: 0 });
  });

  it('should load teams and player pool on init when seasonID is provided', () => {
    fixture.detectChanges();

    expect(mockDraft.getTeams).toHaveBeenCalledWith({ seasonID: 5 });
    expect(mockDraft.getPlayerPool).toHaveBeenCalledWith({ seasonID: 5 });
    expect(mockDraft.areCaptainsLocked).toHaveBeenCalledWith({ seasonID: 5 });
  });

  it('heads the page as the draft board, with one link back to the rosters page', () => {
    fixture.detectChanges();

    const head: HTMLElement = fixture.nativeElement.querySelector('.hud-page-head');
    expect(head.querySelector('h1')!.textContent!.trim()).toBe('Draft Board');
    const links = Array.from(head.querySelectorAll('a'));
    expect(links.map(a => a.getAttribute('href'))).toEqual(['/season/5/teams']);
    expect(links[0].textContent!.trim()).toBe('> Back to Teams & Rosters');
    expect(head.textContent).not.toContain('`r`n');
  });

  it('should get current season ID when route param is 0', () => {
    fixture.componentRef.setInput('seasonID', '0');
    mockSeason.getCurrentSeasonId.and.returnValue(of(10));

    fixture.detectChanges();

    expect(mockSeason.getCurrentSeasonId).toHaveBeenCalled();
  });

  describe('when the route moves to another season', () => {
    const team = (teamID: number, name: string): TeamResponseDto =>
      ({ teamID, teamName: name, captain: { name: 'Captain ' + name, personID: teamID, order: 1, region: null }, players: [] });

    it("loads that season's draft in place of the one shown", () => {
      mockDraft.getTeams.and.callFake(p => of([team(p.seasonID, 'Season ' + p.seasonID)]));
      mockDraft.getPlayerPool.and.callFake(p => of([{ name: 'Pool ' + p.seasonID, personID: 100 + p.seasonID, pick: null, round: null, region: null }]));
      mockDraft.areCaptainsLocked.and.callFake(p => of(p.seasonID === 5));
      fixture.detectChanges();
      expect(component.teams().map(t => t.teamName)).toEqual(['Season 5']);

      fixture.componentRef.setInput('seasonID', '6');
      fixture.detectChanges();

      expect(mockDraft.getTeams).toHaveBeenCalledWith({ seasonID: 6 });
      expect(component.teams().map(t => t.teamName)).toEqual(['Season 6']);
      expect(component.playerPool().map(p => p.name)).toEqual(['Pool 6']);
      expect(component.captainLocked()).toBeFalse();
      const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a')).map(a => a.getAttribute('href'));
      expect(links).toContain('/season/6/teams');
      expect(links).not.toContain('/season/5/teams');
    });

    it("drops the previous season's draft when it arrives late", () => {
      const late = new Subject<TeamResponseDto[]>();
      mockDraft.getTeams.and.callFake(p => p.seasonID === 5 ? late : of([team(6, 'Season 6')]));
      fixture.detectChanges();

      fixture.componentRef.setInput('seasonID', '0');
      mockSeason.getCurrentSeasonId.and.returnValue(new Subject<number>());
      fixture.detectChanges();
      late.next([team(5, 'Season 5')]);
      late.complete();

      // Waiting for the current season: nothing of season 5 is shown
      expect(component.teams()).toEqual([]);
      expect(component.playerPool()).toEqual([]);
      expect(component.captainLocked()).toBeTrue();
    });

    it("drops the current season's ID when the route moves on before it arrives", () => {
      const current = new Subject<number>();
      mockSeason.getCurrentSeasonId.and.returnValue(current);
      fixture.componentRef.setInput('seasonID', '0');
      fixture.detectChanges();

      fixture.componentRef.setInput('seasonID', '6');
      fixture.detectChanges();
      current.next(10);

      expect(mockDraft.getTeams).not.toHaveBeenCalledWith({ seasonID: 10 });
      expect(mockDraft.getTeams).toHaveBeenCalledWith({ seasonID: 6 });
    });

    it("watches that season's draft in place of the previous one's", () => {
      fixture.detectChanges();
      fixture.componentRef.setInput('seasonID', '6');
      fixture.detectChanges();

      expect(live.active).toEqual([['draft:6']]);
    });
  });

  it('should load the current season when route param is 0', () => {
    fixture.componentRef.setInput('seasonID', '0');
    mockSeason.getCurrentSeasonId.and.returnValue(of(10));

    fixture.detectChanges();

    expect(mockDraft.getTeams).toHaveBeenCalledWith({ seasonID: 10 });
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
        { teamID: 0, teamName: 'Team 1', captain: { name: 'Captain 1', personID: 1, order: 1, region: null }, players: [] }
      ];
      mockDraft.getTeams.and.returnValue(of(mockTeams));

      component.load();

      expect(component.teams()).toEqual(mockTeams);
    });

    it('should load player pool successfully', () => {
      const mockPlayers: TeamsPlayerDto[] = [
        { name: 'Player 1', personID: 2, pick: null, round: null, region: null }
      ];
      mockDraft.getPlayerPool.and.returnValue(of(mockPlayers));

      component.load();

      expect(component.playerPool()).toEqual(mockPlayers);
    });

    it('should update captainLocked status', () => {
      mockDraft.areCaptainsLocked.and.returnValue(of(false));

      component.load();

      expect(component.captainLocked()).toBe(false);
    });
  });

  describe('lockCaptainsButtonClicked', () => {
    it('should unlock captains when currently locked', () => {
      component.captainLocked.set(true);
      mockDraft.unlockCaptains.and.returnValue(of(undefined));

      component.lockCaptainsButtonClicked();

      expect(mockDraft.unlockCaptains).toHaveBeenCalled();
      expect(component.captainLocked()).toBe(false);
    });

    it('should lock captains when currently unlocked', () => {
      component.captainLocked.set(false);
      mockDraft.lockCaptains.and.returnValue(of(undefined));

      component.lockCaptainsButtonClicked();

      expect(mockDraft.lockCaptains).toHaveBeenCalled();
      expect(component.captainLocked()).toBe(true);
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
      const team: TeamResponseDto = { teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1, region: null }, players: [] };

      expect(component.DisableDraggingPlayerToTeam(team)).toBe(false);
    });

    it('should return true when no current captain on deck', () => {
      mockAccountService.isEventOrganizer.and.returnValue(false);
      component.teams.set([]);
      const team: TeamResponseDto = { teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1, region: null }, players: [] };

      expect(component.DisableDraggingPlayerToTeam(team)).toBe(true);
    });

    it('should return false when it is current users turn and their team', () => {
      mockAccountService.isEventOrganizer.and.returnValue(false);
      mockAccountService.personID.and.returnValue(1);
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1, region: null }, players: [] }]);
      component.playerPool.set([{ name: 'Player', personID: 2, pick: null, round: null, region: null }]);
      const team: TeamResponseDto = { teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1, region: null }, players: [] };

      expect(component.DisableDraggingPlayerToTeam(team)).toBe(false);
    });
  });

  describe('onDeck', () => {
    it('should return null when no teams exist', () => {
      component.teams.set([]);

      expect(component.onDeck()).toBeNull();
    });

    it('should return null when player pool is empty', () => {
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1, region: null }, players: [] }]);
      component.playerPool.set([]);

      expect(component.onDeck()).toBeNull();
    });

    it('should return captain with fewest players', () => {
      component.teams.set([
        { teamID: 0, teamName: 'Team 1', captain: { name: 'Cap 1', personID: 1, order: 1, region: null }, players: [{ name: 'P1', personID: 3, pick: 1, round: 1, region: null }] },
        { teamID: 0, teamName: 'Team 2', captain: { name: 'Cap 2', personID: 2, order: 2, region: null }, players: [] }
      ]);
      component.playerPool.set([{ name: 'Player', personID: 4, pick: null, round: null, region: null }]);

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
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Cap', personID: 1, order: 1, region: null }, players: [] }]);
      component.playerPool.set([]);

      expect(component.statusText()).toBe('Player pool is empty');
    });

    it('should return captain on deck message', () => {
      component.teams.set([{ teamID: 0, teamName: 'Test', captain: { name: 'Captain Name', personID: 1, order: 1, region: null }, players: [] }]);
      component.playerPool.set([{ name: 'Player', personID: 2, pick: null, round: null, region: null }]);

      expect(component.statusText()).toBe('Captain Name is on deck');
    });
  });

  describe('MapToPlayer', () => {
    it('should map available player correctly', () => {
      const playerDto: TeamsPlayerDto = { name: 'Test Player', personID: 1, pick: 5, round: 2, region: null };

      const result = component.MapToPlayer(playerDto, GrabbedThing.AvailablePlayer);

      expect(result.name).toBe('Test Player');
      expect(result.personID).toBe(1);
      expect(result.round).toBeNull();
      expect(result.pick).toBeNull();
    });



    it('should throw error for invalid type', () => {
      const warn = spyOn(console, 'warn');
      spyOn(console, 'debug');
      expect(() => component.MapToPlayer({}, GrabbedThing.Team)).toThrowError('Unable to map to player');
      // The thrown error is reported; a warning too would be a second line
      expect(warn).not.toHaveBeenCalled();
    });
  });

  describe('MapToTeam', () => {
    it('should map available player to team', () => {
      const player: TeamsPlayerDto = { name: 'Player', personID: 5, pick: null, round: null, region: null };

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
        captain: { name: 'Cap', personID: 2, order: 1, region: null },
        players: [{ name: 'P1', personID: 3, pick: 1, round: 1, region: null }]
      };

      const result = component.MapToTeam(team, GrabbedThing.Team);

      expect(result.teamName).toBe('Existing Team');
      expect(result.captain.personID).toBe(2);
      expect(result.players).toHaveSize(1);
    });

    it('should throw error for invalid type', () => {
      const warn = spyOn(console, 'warn');
      spyOn(console, 'debug');
      expect(() => component.MapToTeam({}, GrabbedThing.Player)).toThrowError('Unable to map to team');
      // The thrown error is reported; a warning too would be a second line
      expect(warn).not.toHaveBeenCalled();
    });
  });

  describe('onDragStart', () => {
    it('should set grabbed thing on drag start', () => {
      const player: TeamsPlayerDto = { name: 'Player', personID: 1, pick: null, round: null, region: null };
      const event = new DragEvent('dragstart');

      component.onDragStart(player, event, GrabbedThing.AvailablePlayer);

      // Can't access private members but test doesn't throw
      expect(component).toBeTruthy();
    });
  });

  describe('createDraggedPlayer', () => {
    it('should create grabbed player object', () => {
      const player: TeamsPlayerDto = { name: 'Player', personID: 1, pick: null, round: null, region: null };
      const team: TeamResponseDto = { teamID: 0, teamName: 'Team', captain: { name: 'Cap', personID: 2, order: 1, region: null }, players: [] };

      const result = component.createDraggedPlayer(player, team);

      expect(result.player).toBe(player);
      expect(result.team).toBe(team);
    });
  });

  describe('drag and drop', () => {
    // The ids that drop handlers announce come from the drag data, which ngx-drag-drop
    // hands over as a JSON copy, so the specs use copies too.
    const copy = <T>(x: T): T => JSON.parse(JSON.stringify(x));
    const cap = (id: number, order: number): CaptainDto => ({ name: 'Cap ' + id, personID: id, order, region: null });
    const player = (id: number, round: number | null = null): TeamsPlayerDto => ({ name: 'Player ' + id, personID: id, pick: round, round, region: null });
    const drop = (data: unknown, index?: number) => ({ data: copy(data), dropEffect: 'move', index } as DndDropEvent);
    const teamNames = () => component.teams().map(t => `${t.captain.order}:${t.captain.personID}`);
    const roster = (captainID: number) => component.teams().find(t => t.captain.personID === captainID)!.players.map(p => `${p.round}:${p.personID}`);
    const pool = () => component.playerPool().map(p => p.personID);
    const grab = (thing: unknown, kind: GrabbedThing) => component.onDragStart(thing as GrabbedThingTypes, new DragEvent('dragstart'), kind);

    beforeEach(() => {
      mockDraft.getTeams.and.returnValue(of([
        { teamID: 1, teamName: 'Alpha', captain: cap(101, 1), players: [player(1, 1), player(2, 2)] },
        { teamID: 2, teamName: 'Bravo', captain: cap(102, 2), players: [] },
      ]));
      mockDraft.getPlayerPool.and.returnValue(of([player(3), player(4), player(5)]));
      for (const m of ['addCaptain', 'resortCaptain', 'removeCaptain', 'addPlayerToTeam', 'movePlayerToTeam', 'removePlayerFromTeam'] as const) {
        (mockDraft[m] as jasmine.Spy).and.returnValue(of(m === 'addCaptain' ? { teamName: 'Named', teamID: 9 } : {}));
      }
      spyOn(console, 'debug');
      spyOn(console, 'warn');
      fixture.detectChanges();
    });

    it('makes a pool player a captain at the drop position and takes the name the server answers with', () => {
      const saved = new Subject<CaptainAddedResponseDto>();
      mockDraft.addCaptain.and.returnValue(saved);
      grab(component.playerPool()[1], GrabbedThing.AvailablePlayer);
      component.onDropInTeam(drop(component.playerPool()[1], 1));

      expect(teamNames()).toEqual(['1:101', '2:4', '3:102']);
      expect(pool()).toEqual([3, 5]);
      expect(component.teams()[1].teamName).toBe("Player 4's Team");
      expect(mockDraft.addCaptain).toHaveBeenCalledWith(jasmine.objectContaining({
        seasonID: 5, body: jasmine.objectContaining({ seasonID: 5, personID: 4, orderNumber: 2 }),
      }));

      // A later pick is still on its way; the answer must not reload over it.
      mockDraft.addPlayerToTeam.and.returnValue(new Subject<void>());
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[0]);
      const loads = mockDraft.getTeams.calls.count();

      saved.next({ teamName: 'The Fours', teamID: 44 });
      saved.complete();

      expect(mockDraft.getTeams.calls.count()).toBe(loads);
      expect(component.teams()[1].teamName).toBe('The Fours');
      expect(component.teams()[1].teamID).toBe(44);
      expect(roster(101)).toEqual(['1:1', '2:2', '3:3']);
    });

    // Every drop is applied before the server answers; a refusal must not leave it on screen.
    const refusals: [string, keyof TeamsService, () => void][] = [
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
        // As GlobalExceptionHandler sends a TeamServiceException.
        (mockDraft[request] as jasmine.Spy).and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { title: 'Request refused', status: 400, detail: 'It is not your turn to be making a draft pick' } })));
        const loads = mockDraft.getTeams.calls.count();

        act();

        expect(mockDraft[request]).toHaveBeenCalled();
        expect(mockSnackBar.open).toHaveBeenCalledWith('Draft change not saved: It is not your turn to be making a draft pick', 'Close');
        expect(mockDraft.getTeams.calls.count()).toBe(loads + 1);
        expect(teamNames()).toEqual(['1:101', '2:102']);
        expect(roster(101)).toEqual(['1:1', '2:2']);
        expect(pool()).toEqual([3, 4, 5]);
      });
    }


    /** Season 5's draft as the live hub pushes it: Bravo first, with Player 3 picked. */
    const pushed = (): DraftLiveDto => ({
      seasonID: 5,
      teams: [
        { teamID: 2, teamName: 'Bravo', captain: cap(102, 1), players: [player(3, 1)] },
        { teamID: 1, teamName: 'Alpha', captain: cap(101, 2), players: [player(1, 1), player(2, 2)] },
      ],
      pool: [player(4), player(5)],
      captainsLocked: false,
    });

    describe('when a live change to the draft comes in', () => {
      const loads = () => mockDraft.getTeams.calls.count();
      /** A notice that the draft changed (no draft with it), and the page's wait for more changes ran out. */
      const liveChange = () => {
        live.send({ topic: 'draft:5' });
        jasmine.clock().tick(LIVE_RELOAD_MS);
      };

      beforeEach(() => {
        jasmine.clock().install();
        // No spread: a reload runs LIVE_RELOAD_MS after a change
        spyOn(Math, 'random').and.returnValue(0);
      });
      afterEach(() => jasmine.clock().uninstall());

      it('loads the draft again, once a burst of changes settles', () => {
        const before = loads();
        live.send({ topic: 'draft:5' });
        jasmine.clock().tick(LIVE_RELOAD_MS - 1);
        live.send({ topic: 'draft:5' });
        jasmine.clock().tick(LIVE_RELOAD_MS - 1);
        expect(loads()).toBe(before);

        jasmine.clock().tick(1);
        expect(loads()).toBe(before + 1);
        // That load does not watch again: no loop
        fixture.detectChanges();
        expect(loads()).toBe(before + 1);
        expect(live.watching.length).toBe(1);
      });

      it('loads again on every later change', () => {
        const before = loads();
        liveChange();
        liveChange();
        expect(loads()).toBe(before + 2);
      });

      it('shows the teams, pool and lock state it loads', () => {
        mockDraft.getTeams.and.returnValue(of([
          { teamID: 2, teamName: 'Bravo', captain: cap(102, 1), players: [player(3, 1)] },
          { teamID: 1, teamName: 'Alpha', captain: cap(101, 2), players: [player(1, 1), player(2, 2)] },
        ]));
        mockDraft.getPlayerPool.and.returnValue(of([player(4), player(5)]));
        mockDraft.areCaptainsLocked.and.returnValue(of(false));
        liveChange();
        expect(teamNames()).toEqual(['1:102', '2:101']);
        expect(roster(102)).toEqual(['1:3']);
        expect(pool()).toEqual([4, 5]);
        expect(component.captainLocked()).toBeFalse();
      });

      it('waits for a drag under way to end', () => {
        const before = loads();
        grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
        liveChange();
        expect(loads()).toBe(before);
        component.onDragEnd();
        expect(loads()).toBe(before + 1);
        // A later drag ends with nothing waiting
        grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
        component.onDragEnd();
        expect(loads()).toBe(before + 1);
      });

      it('waits for a save on its way, as other reloads do', () => {
        const saved = new Subject<void>();
        mockDraft.addPlayerToTeam.and.returnValue(saved);
        grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
        component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]);
        component.onDragEnd();
        const before = loads();
        liveChange();
        expect(loads()).toBe(before);
        saved.next();
        saved.complete();
        expect(loads()).toBe(before + 1);
      });

      it('shows a draft pushed with the change at once, without loading it', () => {
        const before = loads();
        live.send({ topic: 'draft:5', data: pushed() });
        expect(teamNames()).toEqual(['1:102', '2:101']);
        expect(roster(102)).toEqual(['1:3']);
        expect(pool()).toEqual([4, 5]);
        expect(component.captainLocked()).toBeFalse();
        jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
        expect(loads()).toBe(before);
      });

      it('loads instead, once a drag under way ends, when a draft is pushed during it', () => {
        const before = loads();
        grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
        live.send({ topic: 'draft:5', data: pushed() });
        jasmine.clock().tick(LIVE_RELOAD_MS + LIVE_SPREAD_MS);
        expect(teamNames()).toEqual(['1:101', '2:102']);
        expect(loads()).toBe(before);

        component.onDragEnd();
        expect(loads()).toBe(before + 1);
      });

      it('loads again when the live connection comes back', () => {
        const before = loads();
        live.send({ topic: null });
        jasmine.clock().tick(LIVE_RELOAD_MS);
        expect(loads()).toBe(before + 1);
      });

      it('stops watching once the page is gone, and drops a reload still waiting', () => {
        const before = loads();
        live.send({ topic: 'draft:5' });
        fixture.destroy();
        jasmine.clock().tick(LIVE_RELOAD_MS);
        expect(live.active).toEqual([]);
        expect(loads()).toBe(before);
      });
    });

    it('renders a draft another user\'s change pushed without a manual detectChanges', async () => {
      fixture.autoDetectChanges();
      await fixture.whenStable();

      const loads = mockDraft.getTeams.calls.count();
      live.send({ topic: 'draft:5', data: {
        seasonID: 5,
        teams: [
          { teamID: 31, teamName: 'Charlie', captain: cap(4, 1), players: [] },
          { teamID: 1, teamName: 'Alpha', captain: cap(101, 2), players: [player(1, 1), player(2, 2)] },
          { teamID: 2, teamName: 'Bravo', captain: cap(102, 3), players: [] },
        ],
        pool: [player(3), player(5)],
        captainsLocked: true,
      } satisfies DraftLiveDto });
      await fixture.whenStable();
      expect(component.teams()[0].teamID).toBe(31);
      expect(mockDraft.getTeams.calls.count()).toBe(loads);

      const titles = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('mat-card.item:not(.dndPlaceholder) mat-card-title'))
        .map(t => t.textContent!.trim());
      expect(titles).toEqual(['1 - Charlie', '2 - Alpha', '3 - Bravo']);
    });

    it('lets a save finish after the page is gone, without touching it', () => {
      const saved = new Subject<void>();
      mockDraft.addPlayerToTeam.and.returnValue(saved);
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]);
      const loads = mockDraft.getTeams.calls.count();

      fixture.destroy();
      expect(saved.observed).toBeTrue(); // still being sent

      saved.error(new HttpErrorResponse({ status: 400, error: { title: 'Request refused', detail: 'No' } }));
      expect(mockSnackBar.open).not.toHaveBeenCalled();
      expect(mockDraft.getTeams.calls.count()).toBe(loads);
    });

    it('applies only the latest reload; an older one still loading is cancelled', () => {
      const older = new Subject<TeamResponseDto[]>();
      mockDraft.getTeams.and.returnValue(older);
      component.load();
      expect(older.observed).toBeTrue();

      mockDraft.getTeams.and.returnValue(of([{ teamID: 9, teamName: 'Newest', captain: cap(109, 1), players: [] }]));
      component.load();
      expect(older.observed).toBeFalse();
      older.next([{ teamID: 8, teamName: 'Older', captain: cap(108, 1), players: [] }]);

      expect(component.teams().map(t => t.teamName)).toEqual(['Newest']);
    });

    it('takes the lock state from the reload too', () => {
      mockDraft.areCaptainsLocked.and.returnValue(of(false));
      component.load();
      expect(component.captainLocked()).toBeFalse();
    });

    it('keeps teams and pool together when part of a reload fails, and still takes the lock state', () => {
      mockDraft.getTeams.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
      mockDraft.getPlayerPool.and.returnValue(of([player(9)]));
      mockDraft.areCaptainsLocked.and.returnValue(of(false));
      const teams = component.teams();
      const playerPool = component.playerPool();

      component.load();

      expect(component.teams()).toBe(teams);
      expect(component.playerPool()).toBe(playerPool); // not the pool without its teams
      expect(component.captainLocked()).toBeFalse();
      expect(mockSnackBar.open).toHaveBeenCalledWith('Failed to load the draft', 'Close');

      // the same when the pool is the part that fails
      mockDraft.getTeams.and.returnValue(of([]));
      mockDraft.getPlayerPool.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
      component.load();
      expect(component.teams()).toBe(teams);

      // and the next full reload still works
      mockDraft.getPlayerPool.and.returnValue(of([player(9)]));
      component.load();
      expect(component.teams()).toEqual([]);
      expect(pool()).toEqual([9]);
    });

    it('settles a save even when handling its answer throws', async () => {
      const unhandled = jasmine.createSpy('onUnhandledError');
      const previous = rxjsConfig.onUnhandledError;
      rxjsConfig.onUnhandledError = unhandled;
      try {
        // A null AddCaptain answer makes the name lookup throw inside the save's callback.
        mockDraft.addCaptain.and.returnValue(of(null as unknown as CaptainAddedResponseDto));
        mockDraft.resortCaptain.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { title: 'Request refused', detail: 'No' } })));
        const pending = new Subject<void>();
        mockDraft.addPlayerToTeam.and.returnValue(pending);
        grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
        component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]); // stays in flight
        grab(component.playerPool()[1], GrabbedThing.AvailablePlayer);
        component.onDropInTeam(drop(component.playerPool()[1], 1)); // its answer throws
        grab(component.teams()[0], GrabbedThing.Team);
        component.onDropInTeam(drop(component.teams()[0], 2)); // refused: wants a reload
        const loads = mockDraft.getTeams.calls.count();

        pending.next();
        pending.complete();
        await new Promise(resolve => setTimeout(resolve)); // RxJS reports the thrown error asynchronously

        expect(unhandled).toHaveBeenCalled();
        expect(mockDraft.getTeams.calls.count()).toBe(loads + 1); // the count got back to 0
      } finally {
        rxjsConfig.onUnhandledError = previous;
      }
    });

    it('does not load once the page is gone', () => {
      const loads = mockDraft.getTeams.calls.count();
      fixture.destroy();
      component.load();
      expect(mockDraft.getTeams.calls.count()).toBe(loads);
    });

    it('reloads after a refusal only once the other saves from this page are done, and once', () => {
      const refused = new Subject<void>();
      const pending = new Subject<void>();
      mockDraft.resortCaptain.and.returnValue(refused);
      mockDraft.addPlayerToTeam.and.returnValue(pending);
      grab(component.teams()[0], GrabbedThing.Team);
      component.onDropInTeam(drop(component.teams()[0], 2));
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]);
      const loads = mockDraft.getTeams.calls.count();

      refused.error(new HttpErrorResponse({ status: 400, error: { title: 'Request refused', detail: 'No' } }));
      expect(mockSnackBar.open).toHaveBeenCalledWith('Draft change not saved: No', 'Close');
      expect(mockDraft.getTeams.calls.count()).toBe(loads); // the pick is still on its way

      pending.next();
      pending.complete();
      expect(mockDraft.getTeams.calls.count()).toBe(loads + 1);
    });

    it('does not reload after a successful save', () => {
      const loads = mockDraft.getTeams.calls.count();
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]);
      expect(mockDraft.getTeams.calls.count()).toBe(loads);
    });

    it('does not apply a reload that was loading when a drop was saved; loads again after that save', () => {
      const loading = new Subject<TeamResponseDto[]>();
      mockDraft.getTeams.and.returnValue(loading);
      component.load();

      // A pick is dropped and its save starts while the reload is still loading.
      const pick = new Subject<void>();
      mockDraft.addPlayerToTeam.and.returnValue(pick);
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]);
      expect(roster(102)).toEqual(['1:3']);

      // The reload answers from before the pick: it must not remove it.
      loading.next([{ teamID: 2, teamName: 'Bravo', captain: cap(102, 1), players: [] }]);
      loading.complete();
      expect(roster(102)).toEqual(['1:3']);
      const loads = mockDraft.getTeams.calls.count();

      // Once the pick is saved, the page loads again (and that answer has it).
      mockDraft.getTeams.and.returnValue(of([
        { teamID: 1, teamName: 'Alpha', captain: cap(101, 1), players: [player(1, 1), player(2, 2)] },
        { teamID: 2, teamName: 'Bravo', captain: cap(102, 2), players: [player(3, 1)] },
      ]));
      pick.next();
      pick.complete();
      expect(mockDraft.getTeams.calls.count()).toBe(loads + 1);
      expect(roster(102)).toEqual(['1:3']);
    });

    it('uses the title, or a plain-text body, when there is no detail', () => {
      mockDraft.resortCaptain.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403, error: { title: 'Forbidden', status: 403 } })));
      grab(component.teams()[0], GrabbedThing.Team);
      component.onDropInTeam(drop(component.teams()[0], 2));
      expect(mockSnackBar.open).toHaveBeenCalledWith('Draft change not saved: Forbidden', 'Close');

      mockDraft.resortCaptain.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: 'You must be logged in' })));
      grab(component.teams()[0], GrabbedThing.Team);
      component.onDropInTeam(drop(component.teams()[0], 2));
      expect(mockSnackBar.open).toHaveBeenCalledWith('Draft change not saved: You must be logged in', 'Close');
    });

    it('gives a generic reason when the refusal has no message', () => {
      mockDraft.resortCaptain.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
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
      expect(mockDraft.resortCaptain).toHaveBeenCalledWith(jasmine.objectContaining({
        body: jasmine.objectContaining({ personID: 101, orderNumber: 2 }),
      }));
    });

    it('drafts a pool player onto a team', () => {
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropPickInTeam(drop(component.playerPool()[0]), component.teams()[1]);

      expect(roster(102)).toEqual(['1:3']);
      expect(pool()).toEqual([4, 5]);
      expect(mockDraft.addPlayerToTeam).toHaveBeenCalledWith({
        seasonID: 5, body: { seasonID: 5, captainID: 102, personID: 3 },
      });
    });

    it('moves a pick to another team and renumbers both', () => {
      const alpha = component.teams()[0];
      const grabbed = component.createDraggedPlayer(alpha.players[0], alpha);
      grab(grabbed, GrabbedThing.Player);
      component.onDropPickInTeam(drop(grabbed), component.teams()[1]);

      expect(roster(101)).toEqual(['1:2']);
      expect(roster(102)).toEqual(['1:1']);
      expect(mockDraft.movePlayerToTeam).toHaveBeenCalledWith({
        seasonID: 5, body: { seasonID: 5, previousCaptainID: 101, newCaptainID: 102, personID: 1, roundNumber: 1 },
      });
    });

    it('moves a pick to the end of its own team', () => {
      const alpha = component.teams()[0];
      const grabbed = component.createDraggedPlayer(alpha.players[0], alpha);
      grab(grabbed, GrabbedThing.Player);
      component.onDropPickInTeam(drop(grabbed), component.teams()[0]);

      expect(roster(101)).toEqual(['1:2', '2:1']);
      expect(mockDraft.movePlayerToTeam).toHaveBeenCalledWith(jasmine.objectContaining({
        body: jasmine.objectContaining({ previousCaptainID: 101, newCaptainID: 101, roundNumber: 2 }),
      }));
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
      expect(mockDraft.removePlayerFromTeam).toHaveBeenCalledWith({
        seasonID: 5, body: { seasonID: 5, captainID: 101, personID: 2 },
      });
    });

    it('reorders the pool without telling the backend', () => {
      grab(component.playerPool()[0], GrabbedThing.AvailablePlayer);
      component.onDropInPool(drop(component.playerPool()[0], 3)); // to the end

      expect(pool()).toEqual([4, 5, 3]);
      expect(mockDraft.removePlayerFromTeam).not.toHaveBeenCalled();
    });

    it('logs an unexpected drag type dropped in the pool', () => {
      grab({}, 'Mystery' as GrabbedThing);
      component.onDropInPool(drop({}, 0));
      expect(console.warn).toHaveBeenCalledWith('Draft: unexpected item dropped in the pool: Mystery');
    });

    it('ignores drops that are not a copy or move, and pick drops without a team', () => {
      const before = { teams: component.teams(), pool: component.playerPool() };
      component.onDropInTeam({ data: {}, dropEffect: 'none' } as DndDropEvent);
      component.onDropInPool({ data: {}, dropEffect: 'link' } as DndDropEvent);
      component.onDropPickInTeam({ data: {}, dropEffect: 'none' } as DndDropEvent, component.teams()[0]);
      component.onDropPickInTeam(drop({}));
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

    it('does not duplicate a player when a reload lands mid-drag', () => {
      const dragged = component.playerPool()[1]; // Player 4, the object the drag started from
      grab(dragged, GrabbedThing.AvailablePlayer);

      // Meanwhile someone drafts Player 3, and the page loads the draft again: every list is replaced with new objects.
      mockDraft.getTeams.and.returnValue(of([
        { teamID: 1, teamName: 'Alpha', captain: cap(101, 1), players: [player(1, 1), player(2, 2), player(3, 3)] },
        { teamID: 2, teamName: 'Bravo', captain: cap(102, 2), players: [] },
      ]));
      mockDraft.getPlayerPool.and.returnValue(of([player(4), player(5)]));
      component.load();
      expect(component.playerPool()[0]).not.toBe(dragged);

      component.onDropPickInTeam(drop(dragged), component.teams()[1]);

      expect(pool()).toEqual([5]);
      expect(roster(102)).toEqual(['1:4']);
      const everyone = [...component.teams().flatMap(t => t.players.map(p => p.personID)), ...pool()];
      expect(new Set(everyone).size).toBe(everyone.length);
    });

    it('appends when the drop has no index', () => {
      mockDraft.addCaptain.and.returnValue(new Subject<CaptainAddedResponseDto>());
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
        ['new captain', () => { grab(player(997), GrabbedThing.AvailablePlayer); component.onDropInTeam(drop(player(997), 0)); }, mockDraft.addCaptain],
        ['re-sorted team', () => { grab(ghostTeam, GrabbedThing.Team); component.onDropInTeam(drop(ghostTeam, 0)); }, mockDraft.resortCaptain],
        ['pick to pool', () => { grab(ghostPick, GrabbedThing.Player); component.onDropInPool(drop(ghostPick, 0)); }, mockDraft.removePlayerFromTeam],
        ['team to pool', () => { grab(ghostTeam, GrabbedThing.Team); component.onDropInPool(drop(ghostTeam, 0)); }, mockDraft.removeCaptain],
        ['pick to team', () => { grab(ghostPick, GrabbedThing.Player); component.onDropPickInTeam(drop(ghostPick), component.teams()[1]); }, mockDraft.movePlayerToTeam],
      ];
    };
    for (const name of ['new captain', 're-sorted team', 'pick to pool', 'team to pool', 'pick to team']) {
      it(`reloads instead of announcing a stale drop (${name})`, () => {
        const [, act, request] = stale().find(([n]) => n === name)!;
        const loads = mockDraft.getTeams.calls.count();
        act();
        expect(mockDraft.getTeams.calls.count()).toBe(loads + 1);
        expect(request).not.toHaveBeenCalled();
      });
    }

    it('reloads instead of guessing when a drop names someone the page no longer has', () => {
      const dragged = component.playerPool()[0];
      grab(dragged, GrabbedThing.AvailablePlayer);
      component.playerPool.set(component.playerPool().slice(1)); // already drafted elsewhere
      const loads = mockDraft.getTeams.calls.count();

      component.onDropPickInTeam(drop(dragged), component.teams()[1]);

      expect(mockDraft.getTeams.calls.count()).toBe(loads + 1);
      expect(mockDraft.addPlayerToTeam).not.toHaveBeenCalled();
    });
  });

  describe('template rendering (@for)', () => {
    const cap = (n: number): CaptainDto => ({ name: 'Cap ' + n, personID: 100 + n, order: n, region: null });
    const player = (id: number, round: number): TeamsPlayerDto => ({ name: 'Player ' + id, personID: id, pick: id, round: round, region: null });

    function teamCards(): HTMLElement[] {
      return Array.from(fixture.nativeElement.querySelectorAll('mat-card.item:not(.dndPlaceholder)'));
    }

    function poolItems(): HTMLElement[] {
      return Array.from(fixture.nativeElement.querySelectorAll('mat-list-item.player-pool-item:not([dndPlaceholderRef])'));
    }

    beforeEach(() => {
      mockDraft.getTeams.and.returnValue(of([
        { teamID: 1, teamName: 'Alpha', captain: cap(1), players: [player(1, 1), player(2, 2)] },
        { teamID: 2, teamName: 'Bravo', captain: cap(2), players: [] }
      ]));
      mockDraft.getPlayerPool.and.returnValue(of([player(3, 0), player(4, 0), player(5, 0)]));
      fixture.detectChanges();
    });

    it('renders one card per team, in order', () => {
      const cards = teamCards();
      expect(cards).toHaveSize(2);
      expect(cards[0].querySelector('mat-card-title')?.textContent).toContain('1 - Alpha');
      expect(cards[0].querySelector('mat-card-subtitle')?.textContent).toContain('Captain: Cap 1');
      expect(cards[1].querySelector('mat-card-title')?.textContent).toContain('2 - Bravo');
    });

    it('renders the players of each team with round and name', () => {
      const [alpha, bravo] = teamCards();
      const alphaPlayers = Array.from(alpha.querySelectorAll('.players-area > div')).map(d => d.textContent?.trim());
      expect(alphaPlayers).toEqual(['1 Player 1', '2 Player 2']);
      expect(bravo.querySelectorAll('.players-area > div')).toHaveSize(0);
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

  const captain = (personID: number, order: number): CaptainDto => ({ name: 'Cap ' + personID, personID, order, region: null });
  const pooled = (personID: number): TeamsPlayerDto => ({ name: 'Player ' + personID, personID, pick: null, round: null, region: null });

  beforeEach(async () => {
    const mockAccountService = jasmine.createSpyObj('AccountService', ['isEventOrganizer', 'isSysAdmin', 'personID']);
    mockAccountService.isEventOrganizer.and.returnValue(true);
    mockAccountService.personID.and.returnValue(0);

    await TestBed.configureTestingModule({
      imports: [TeamBuilderComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open', 'dismiss']) },
        { provide: AccountService, useValue: mockAccountService },
        { provide: LiveService, useValue: new FakeLive() }
      ]
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamBuilderComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('seasonID', '7');

    // The first change detection loads season 7 through the real TeamsService.
    fixture.detectChanges();
    httpTesting.expectOne('/api/seasons/7/draft/teams').flush([
      { teamID: 1, teamName: 'Alpha', captain: captain(11, 1), players: [pooled(21)] },
      { teamID: 2, teamName: 'Bravo', captain: captain(12, 2), players: [] },
      { teamID: 3, teamName: 'Charlie', captain: captain(13, 3), players: [] }
    ]);
    httpTesting.expectOne('/api/seasons/7/draft/player-pool').flush([pooled(30)]);
    httpTesting.expectOne('/api/seasons/7/draft/captains/locked').flush(false);
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

    const req = httpTesting.expectOne('/api/seasons/7/draft/captains/remove');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ seasonID: 7, personID: 12 });
    expect(Object.keys(req.request.body).sort((a, b) => a.localeCompare(b))).toEqual(['personID', 'seasonID']);
    expect('orderNumber' in req.request.body).toBe(false);
    req.flush({});
  });

  it('returns the captain and their players to the pool and re-sequences the remaining captains', () => {
    dropTeamInPool(component.teams()[0]);

    const req = httpTesting.expectOne('/api/seasons/7/draft/captains/remove');
    expect(req.request.body).toEqual({ seasonID: 7, personID: 11 });
    req.flush({});

    expect(component.teams().map(t => [t.captain.personID, t.captain.order])).toEqual([[12, 1], [13, 2]]);
    expect(component.playerPool().map(p => p.personID).sort((a, b) => a - b)).toEqual([11, 21, 30]);
  });
});
