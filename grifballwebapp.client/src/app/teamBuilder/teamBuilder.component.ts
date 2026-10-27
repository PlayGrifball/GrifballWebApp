import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { TeamResponseDto } from '../api/dtos/teamResponseDto';
import { PlayerDto } from '../api/dtos/playerDto';
import { DndDropEvent, DndModule } from 'ngx-drag-drop';
import { Observable } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GrabbedPlayer, GrabbedThing, GrabbedThingTypes } from './personStatus';
import { CaptainDto } from '../api/dtos/captainDto';
import { CaptainPlacementDto, RemoveCaptainDto } from '../api/dtos/captainPlacementDto';
import { AccountService } from '../account.service';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { SignalRService } from '../api/signalR.service';
import { addCaptain, addPlayerToTeam, Draft, dropPick, dropTeam, movePlayerToTeam, movePoolPlayer, removeCaptain, removePlayerFromTeam, resortCaptain } from './draft';

interface DropzoneLayout {
  container: string;
  list: string;
  dndHorizontal: boolean;
}

@Component({
    selector: 'app-team-builder',
    imports: [
        DndModule,
        MatListModule,
        MatIconModule,
        MatButtonModule,
        MatCardModule
    ],
    templateUrl: './teamBuilder.component.html',
    styleUrl: './teamBuilder.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class TeamBuilderComponent {
  private route = inject(ActivatedRoute);
  private api = inject(ApiClientService);
  private snackBarService = inject(MatSnackBar);
  accountService = inject(AccountService);
  private signalR = inject(SignalRService);
  private destroyRef = inject(DestroyRef);

  private seasonID: number = 0;
  readonly captainLocked = signal(true);
  readonly teams = signal<TeamResponseDto[]>([]);
  readonly playerPool = signal<PlayerDto[]>([]);

  private grabbedThing: GrabbedThing = GrabbedThing.AvailablePlayer;

  /** Whose pick it is: the first team (in pick order) with the fewest players, while players are left. */
  readonly onDeck = computed<CaptainDto | null>(() => {
    const teams = this.teams();
    if (teams.length === 0 || this.playerPool().length === 0)
      return null;
    return teams.reduce((fewest, team) => team.players.length < fewest.players.length ? team : fewest).captain;
  });

  readonly statusText = computed(() => {
    // If there are no teams then it is no ones turn yet
    if (this.teams().length === 0)
      return "No Teams Added";
    if (this.playerPool().length === 0)
      return "Player pool is empty";
    return this.onDeck()!.name + " is on deck";
  });

  /** The drag data for each pick (team and player), built once per change instead of per render. */
  readonly draggedPicks = computed(() => new Map(this.teams().flatMap(team =>
    team.players.map(player => [player.personID, this.createDraggedPlayer(player, team)] as const))));

  public GrabbedThing: typeof GrabbedThing = GrabbedThing;
  private readonly verticalLayout: DropzoneLayout = {
    container: 'row',
    list: 'column',
    dndHorizontal: false,
  };
  layout: DropzoneLayout = this.verticalLayout;
  private readonly horizontalLayout: DropzoneLayout = {
    container: 'row',
    list: 'row wrap',
    dndHorizontal: true,
  };

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    if (this.seasonID === 0) {
      this.api.getCurrentSeasonID()
        .subscribe({
          next: (result) => {
            this.seasonID = result;
            this.load();
          },
        });
    }
    else {
      this.load();
    }

    // Other users' changes. The hub connection outlives this page, so the handlers are
    // removed again when the page is destroyed.
    const offs = [
      this.signalR.addCaptain(dto => this.applyRemote(dto.seasonID,
        draft => addCaptain(draft, dto.personID, dto.teamName, dto.captainName, dto.orderNumber))),
      this.signalR.resortCaptain(dto => this.applyRemote(dto.seasonID,
        draft => resortCaptain(draft, dto.personID, dto.orderNumber))),
      this.signalR.removeCaptain(dto => this.applyRemote(dto.seasonID,
        draft => removeCaptain(draft, dto.personID))),
      this.signalR.addPlayerToTeam(dto => this.applyRemote(dto.seasonID,
        draft => addPlayerToTeam(draft, dto.captainID, dto.personID))),
      // The server puts the pick at round roundNumber (TeamService.MovePlayerToTeam).
      this.signalR.movePlayerToTeam(dto => this.applyRemote(dto.seasonID,
        draft => movePlayerToTeam(draft, dto.previousCaptainID, dto.newCaptainID, dto.personID, dto.roundNumber - 1))),
      this.signalR.removePlayerFromTeam(dto => this.applyRemote(dto.seasonID,
        draft => removePlayerFromTeam(draft, dto.captainID, dto.personID))),
      this.signalR.lockCaptains(seasonID => {
        if (seasonID === this.seasonID)
          this.captainLocked.set(true);
      }),
      this.signalR.unlockCaptains(seasonID => {
        if (seasonID === this.seasonID)
          this.captainLocked.set(false);
      }),
    ];
    this.destroyRef.onDestroy(() => offs.forEach(off => off()));
  }

  /** Applies another user's change, if it is for this season. */
  private applyRemote(seasonID: number, change: (draft: Draft) => Draft | null): void {
    if (seasonID !== this.seasonID)
      return;
    this.applyDraft(change);
  }

  /**
   * The one way the draft changes (drag and drop and SignalR alike): a pure operation from
   * ./draft returns the next draft, and only the lists it replaced are set. If the people it
   * names are not in the draft, the page is out of sync and reloads.
   */
  private applyDraft(change: (draft: Draft) => Draft | null): Draft | null {
    const current: Draft = { teams: this.teams(), pool: this.playerPool() };
    const next = change(current);
    if (next === null) {
      console.log('Detected out of sync');
      this.load();
      return null;
    }
    if (next.teams !== current.teams)
      this.teams.set(next.teams);
    if (next.pool !== current.pool)
      this.playerPool.set(next.pool);
    return next;
  }

  load(): void {
    this.api.getTeams(this.seasonID)
        .subscribe({
          next: (result) => this.teams.set(result),
        });
    this.api.getPlayerPool(this.seasonID)
        .subscribe({
          next: (result) => {
            this.playerPool.set(result);
          },
        });
    this.api.areCaptainsLocked(this.seasonID)
      .subscribe({
        next: (result) => {
          this.captainLocked.set(result);
        },
      });
  }

  lockCaptainsButtonClicked(): void {
    if (this.captainLocked()) {
      this.api.unlockCaptains(this.seasonID, this.signalR.hubConnection.connectionId)
        .subscribe({
          next: r => this.captainLocked.set(false)
        })
    }
    else {
      this.api.lockCaptains(this.seasonID, this.signalR.hubConnection.connectionId)
        .subscribe(
          {
            next: r => this.captainLocked.set(true)
          })
    }
  }

  setHorizontalLayout(horizontalLayoutActive: boolean) {
    this.layout = horizontalLayoutActive
      ? this.horizontalLayout
      : this.verticalLayout;
  }

  onDragStart(item: GrabbedThingTypes, event: DragEvent, kind: GrabbedThing) {
    this.grabbedThing = kind;
  }

  /**
   * Sends a drag-and-drop change. The page has already applied it; if the server refuses
   * (not your turn, not signed up, not allowed...), say why and reload the real draft.
   * `reload` also reloads after a success, for changes the server finishes itself.
   */
  private save(request: Observable<unknown>, reload = false): void {
    request.subscribe({
      next: r => {
        console.log(r);
        if (reload)
          this.load();
      },
      error: e => {
        const reason = typeof e?.error === 'string' && e.error ? e.error : 'the server refused it';
        this.snackBarService.open(`Draft change not saved: ${reason}`, 'Close');
        this.load();
      },
    });
  }

  private static isDrop(event: DndDropEvent): boolean {
    return event.dropEffect === 'copy' || event.dropEffect === 'move';
  }

  /** A pool player dropped on the teams list becomes a captain; a team dropped there is re-sorted. */
  onDropInTeam(event: DndDropEvent) {
    if (!TeamBuilderComponent.isDrop(event))
      return;

    const index = event.index ?? this.teams().length;

    // AvailablePlayer -> Team = New team created
    if (this.grabbedThing === GrabbedThing.AvailablePlayer) {
      const player = this.MapToPlayer(event.data, this.grabbedThing);
      const team = this.MapToTeam(player, GrabbedThing.AvailablePlayer);
      const next = this.applyDraft(draft => addCaptain(draft, player.personID, team.teamName, team.captain.name, index + 1));
      if (!next)
        return;
      const added = next.teams.find(t => t.captain.personID === player.personID)!;
      const dto = new CaptainPlacementDto();
      dto.seasonID = this.seasonID;
      dto.personID = added.captain.personID;
      dto.orderNumber = added.captain.order!; // Order must not be null
      // The server names the team (the signup's team name, or "<display name>'s Team"),
      // and does not echo the change back to this connection, so reload to show its name.
      this.save(this.api.addCaptain(dto, this.signalR.hubConnection.connectionId), true);
      console.log('Added new captain ' + added.captain.name + ' with pick order ' + added.captain.order);
    }
    // Team -> Team = Resort team
    else {
      const team = this.MapToTeam(event.data, this.grabbedThing);
      const next = this.applyDraft(draft => dropTeam(draft, team.captain.personID, index));
      if (!next)
        return;
      const moved = next.teams.find(t => t.captain.personID === team.captain.personID)!;
      const dto = new CaptainPlacementDto();
      dto.seasonID = this.seasonID;
      dto.personID = moved.captain.personID;
      dto.orderNumber = moved.captain.order!; // Order must not be null
      // Announce to backend that team has been resorted
      this.save(this.api.resortCaptain(dto, this.signalR.hubConnection.connectionId));
      console.log('Resorted captains ' + moved.captain.name + ' now has pick order ' + moved.captain.order);
    }
  }

  createDraggedPlayer(player: PlayerDto, team: TeamResponseDto): GrabbedPlayer {
    return {
      team: team,
      player: player,
    };
  }

  /** Pool players can be reordered; a pick dropped here is undrafted; a team dropped here is disbanded. */
  onDropInPool(event: DndDropEvent) {
    if (!TeamBuilderComponent.isDrop(event))
      return;

    const index = event.index ?? this.playerPool().length;

    // AvailablePlayer -> AvailablePlayer = only the order changes, the backend does not care
    if (this.grabbedThing === GrabbedThing.AvailablePlayer) {
      const player = this.MapToPlayer(event.data, this.grabbedThing);
      this.applyDraft(draft => movePoolPlayer(draft, player.personID, index));
    }
    // Player -> AvailablePlayer = Player is now available
    else if (this.grabbedThing === GrabbedThing.Player) {
      const grabbedPlayer = event.data as GrabbedPlayer;
      const captainID = grabbedPlayer.team.captain.personID;
      const personID = grabbedPlayer.player.personID;
      if (!this.applyDraft(draft => removePlayerFromTeam(draft, captainID, personID, index)))
        return;
      // Announce player is now available
      this.save(this.api.removePlayerFromTeam({
        seasonID: this.seasonID,
        captainID: captainID,
        personID: personID,
      }, this.signalR.hubConnection.connectionId));
    }
    // Team -> AvailablePlayer = the captain and their picks are available again
    else if (this.grabbedThing === GrabbedThing.Team) {
      const team = event.data as TeamResponseDto;
      const personID = team.captain.personID;
      if (!this.applyDraft(draft => removeCaptain(draft, personID, index)))
        return;
      // Announce to backend that player is now available. RemoveCaptainDto carries no order
      // number: the server re-sequences the remaining captains 1..n itself (TeamService.RemoveCaptain).
      const dto: RemoveCaptainDto = { seasonID: this.seasonID, personID };
      this.save(this.api.removeCaptain(dto, this.signalR.hubConnection.connectionId));
    }
    else {
      console.log('Unexpected type placed in draft pool: ' + this.grabbedThing);
    }
  }

  /** A pool player or another pick dropped on a team's picks. */
  onDropPickInTeam(event: DndDropEvent, team?: TeamResponseDto) {
    if (!team)
      return;
    if (!TeamBuilderComponent.isDrop(event))
      return;

    const captainID = team.captain.personID;

    let personID: number;
    if (this.grabbedThing === GrabbedThing.Player) {
      const grabbedPlayer = event.data as GrabbedPlayer;
      const previousCaptainID = grabbedPlayer.team.captain.personID;
      personID = grabbedPlayer.player.personID;
      const next = this.applyDraft(draft => dropPick(draft, previousCaptainID, captainID, personID, event.index));
      if (!next)
        return;
      const player = next.teams.find(t => t.captain.personID === captainID)!.players.find(p => p.personID === personID)!;
      // Announce player moved
      this.save(this.api.movePlayerToTeam({
        seasonID: this.seasonID,
        previousCaptainID: previousCaptainID,
        newCaptainID: captainID,
        personID: personID,
        roundNumber: player.round!, // MUST NOT BE NULL :(
      }, this.signalR.hubConnection.connectionId));
      console.log(team.captain.name + '\'s ' + player.round + ' pick is ' + player.name);
    }
    else {
      // The server always adds a pick as the team's last round, so the drop position is ignored.
      const picked = this.MapToPlayer(event.data, this.grabbedThing);
      personID = picked.personID;
      const next = this.applyDraft(draft => addPlayerToTeam(draft, captainID, personID));
      if (!next)
        return;
      const player = next.teams.find(t => t.captain.personID === captainID)!.players.find(p => p.personID === personID)!;
      // Announce player added to team
      this.save(this.api.addPlayerToTeam({
        seasonID: this.seasonID,
        captainID: captainID,
        personID: personID,
      }, this.signalR.hubConnection.connectionId));
      console.log(team.captain.name + '\'s ' + player.round + ' pick is ' + player.name);
    }
  }

  /** The pool player a drag carries (drag data is a copy made by ngx-drag-drop). */
  MapToPlayer(data: any, t: GrabbedThing): PlayerDto {
    if (t === GrabbedThing.AvailablePlayer) {
      return { ...(data as PlayerDto), round: null, pick: null };
    } else {
      console.log(data);
      throw new Error('Unable to map to player');
    }
  }

  /** The team a drag carries, or the new team a pool player would captain. */
  MapToTeam(data: any, t: GrabbedThing): TeamResponseDto {
    if (t === GrabbedThing.AvailablePlayer) {
      const player = data as PlayerDto;
      return { teamID: 0, teamName: player.name + "'s Team", captain: { name: player.name, personID: player.personID, order: null }, players: [] };
    } else if (t === GrabbedThing.Team) {
      return data as TeamResponseDto;
    } else {
      console.log(data);
      throw new Error('Unable to map to team');
    }
  }

  DisableDraggingTeams(): boolean {
    // return true if CaptainsLocked or missing EventManager role

    if (this.captainLocked())
      return true;
    if (!this.accountService.isEventOrganizer())
      return true;
    
    return false;
  }

  DisableDraggingPlayerFromTeam(): boolean {
    // Only event organizer can drag players from team 
    if (this.accountService.isEventOrganizer())
      return false;
    return true;
  }

  DisableDraggingPlayerToTeam(teamDto: TeamResponseDto): boolean {
    // Eent organizer can drag players to team whenever
    if (this.accountService.isEventOrganizer()) {
      return false;
    }

    const currentCaptain = this.onDeck();
    if (currentCaptain === null) {
      return true;
    }
    else {
      const isMyTurn = currentCaptain.personID === this.accountService.personID();
      if (!isMyTurn) {
        return true; // Disable when it is not your turn
      }

      return teamDto.captain.personID !== currentCaptain.personID;
    }
  }

  DisableDraggingPlayerFromPool(playerDto: PlayerDto): boolean {
    return false; // Should only be true if has EventManager, OR has Player role & is captain & is their turn to pick
  }


}
