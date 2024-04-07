import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { TeamResponseDto } from '../api/dtos/teamResponseDto';
import { PlayerDto } from '../api/dtos/playerDto';
import { DndModule } from 'ngx-drag-drop';
import {
  DndDraggableDirective,
  DndDropEvent,
  DndDropzoneDirective,
  DndHandleDirective,
  DndPlaceholderRefDirective,
  DropEffect,
  EffectAllowed,
} from 'ngx-drag-drop';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GrabbedPlayer, GrabbedThing, GrabbedThingTypes } from './personStatus';
import { CaptainDto } from '../api/dtos/captainDto';
import { CaptainPlacementDto } from '../api/dtos/captainPlacementDto';
import { AccountService } from '../account.service';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

interface DropzoneLayout {
  container: string;
  list: string;
  dndHorizontal: boolean;
}

@Component({
  selector: 'app-team-builder',
  standalone: true,
  imports: [
    CommonModule,
    DndModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatCardModule,
  ],
  templateUrl: './teamBuilder.component.html',
  styleUrl: './teamBuilder.component.scss',
})
export class TeamBuilderComponent {
  private seasonID: number = 0;
  captainLocked: boolean = true;
  teams : TeamResponseDto[] = [];
  playerPool: PlayerDto[] = [];

  allowTypes: string[] = [
    'PlayerDto'
  ];

  playerChosen : PlayerDto[] = [];

  horizontalLayoutActive: boolean = false;
  private currentDraggableEvent?: DragEvent;
  private currentDragEffectMsg?: string;
  private grabbedThing: GrabbedThing = GrabbedThing.AvailablePlayer;
  private grabbedThingObj!: GrabbedThingTypes;

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


  constructor(private route: ActivatedRoute, private api: ApiClientService, private snackBarService: MatSnackBar, public accountService: AccountService) {}

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
  }

  load(): void {
    this.api.getTeams(this.seasonID)
        .subscribe({
          next: (result) => this.teams = result,
        });
    this.api.getPlayerPool(this.seasonID)
        .subscribe({
          next: (result) => {
            this.playerPool = result;
          },
        });
    this.api.areCaptainsLocked(this.seasonID)
      .subscribe({
        next: (result) => {
          this.captainLocked = result;
        },
      });
  }

  lockCaptainsButtonClicked(): void {
    if (this.captainLocked) {
      this.api.unlockCaptains(this.seasonID)
        .subscribe({
          next: r => this.captainLocked = false
        })
    }
    else {
      this.api.lockCaptains(this.seasonID)
        .subscribe(
          {
            next: r => this.captainLocked = true
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
    this.currentDragEffectMsg = '';
    this.currentDraggableEvent = event;

    //this.snackBarService.dismiss();
    //this.snackBarService.open('Drag started!', undefined, { duration: 2000 });
  }

  onDragged(item: GrabbedThingTypes, list: GrabbedThingTypes[], effect: DropEffect) {
    //this.currentDragEffectMsg = `Drag ended with effect "${effect}"!`;

    if (effect === 'move') {
      const index = list.indexOf(item);
      list.splice(index, 1);
      // TODO: If we remove we need to resort. No need to tell backend but frontend needs to stay up to date
    }
  }

  onDragEnd(event: DragEvent) {
    //this.currentDraggableEvent = event;
    //this.snackBarService.dismiss();
    //this.snackBarService.open(
    //  this.currentDragEffectMsg || `Drag ended!`,
    //  undefined,
    //  { duration: 2000 }
    //);
  }

  onDropInTeam(event: DndDropEvent, list?: TeamResponseDto[]) {
    if (!list)
      return;
    if (event.dropEffect !== 'copy' && event.dropEffect !== 'move')
      return;

    let index = event.index;

    if (typeof index === 'undefined') {
      index = list.length;
    }

    let team = this.MapToTeam(event.data, this.grabbedThing);

    list.splice(index, 0, team);

    // AvailablePlayer -> Team = New team created
    if (this.grabbedThing === GrabbedThing.AvailablePlayer) {
      // There are no duplicates so we just use index
      list.forEach((team, index) => {
        team.captain.order = index + 1;
      });
      const dto = new CaptainPlacementDto();
      dto.seasonID = this.seasonID;
      dto.personID = team.captain.personID;
      dto.orderNumber = team.captain.order!; // Order must not be null
      this.api.addCaptain(dto)
        .subscribe({next: r => console.log(r)});
      console.log('Added new captain ' + team.captain.name + ' with pick order ' + team.captain.order);
    }
    // Team -> Team = Resort team
    else {
      // When resorting there will be a ghost team we have to ignore. Resort to using our own index
      let trueIndex = 1;
      list.forEach((t, i) => {
        const matchingCaptainID = t.captain.personID == team.captain.personID;
        if (matchingCaptainID && i !== index) {
          // Ignore ghost team.
        }
        else {
          t.captain.order = trueIndex++;
        }
      });
      const dto = new CaptainPlacementDto();
      dto.seasonID = this.seasonID;
      dto.personID = team.captain.personID;
      dto.orderNumber = team.captain.order!; // Order must not be null
      this.api.resortCaptain(dto).subscribe({ next: r => console.log(r) });;
      console.log('Resorted captains ' + team.captain.name + ' now has pick order ' + team.captain.order);
      // Announce to backend that team has been resorted
    }
  }

  createDraggedPlayer(player: PlayerDto, team: TeamResponseDto): GrabbedPlayer {
    let foo = {
      team: team,
      player: player,
    };
    return foo;
    //let draggedPlayer = new GrabbedPlayer();
    //draggedPlayer.player = player;
    //draggedPlayer.team = team;
    //return draggedPlayer;
  }

  resortPreviousTeam(grabbedPlayer: GrabbedPlayer) {
    const previousTeam = this.teams.find((t => {
      return t.captain.personID === grabbedPlayer.team.captain.personID;
    }));

    if (previousTeam === undefined) {
      return;
    }

    let trueIndex = 1;
    previousTeam.players.forEach((p, i) => {
      const matchingPlayerID = p.personID == grabbedPlayer.player.personID;
      if (matchingPlayerID) {
        // Ignore removed player.
      }
      else {
        p.round = trueIndex++;
      }
    });

  }

  onDropInPool(event: DndDropEvent, list?: PlayerDto[]) {
    if (!list)
      return;
    if (event.dropEffect !== 'copy' && event.dropEffect !== 'move')
      return;

    let index = event.index;

    if (typeof index === 'undefined') {
      index = list.length;
    }

    let player = this.MapToPlayer(event.data, this.grabbedThing);

    list.splice(index, 0, player);

    // AvailablePlayer -> AvailablePlayer = Do not care
    if (this.grabbedThing === GrabbedThing.AvailablePlayer)
      return;
    else if (this.grabbedThing === GrabbedThing.Player) {
      const grabbedPlayer = event.data as GrabbedPlayer;
      this.resortPreviousTeam(grabbedPlayer);
      // Announce player is now available
      this.api.removePlayerFromTeam({
        seasonID: this.seasonID,
        captainID: grabbedPlayer.team.captain.personID,
        personID: player.personID,
      }).subscribe({ next: r => console.log(r) });
    }
    // Team -> AvailablePlayer = Player is now available
    else if (this.grabbedThing === GrabbedThing.Team) {
      let trueIndex = 1;
      this.teams.forEach((t, i) => {
        const matchingCaptainID = t.captain.personID == player.personID;
        if (matchingCaptainID) {
          // Ignore removed team.
        }
        else {
          t.captain.order = trueIndex++;
        }
      });

      // TBD but if we allow putting team back in pool then the players also must go back in pool, not just captain
      const team = event.data as TeamResponseDto;
      team.players.forEach((p, i) => {
        list.splice(index!, 0, p);
      });

      // Announce to backend that player is now available
      const dto = new CaptainPlacementDto();
      dto.seasonID = this.seasonID;
      dto.personID = player.personID;
      this.api.removeCaptain(dto).subscribe({ next: r => console.log(r) });;

    }
    else {
      console.log('Unexpected type placed in draft pool: ' + this.grabbedThing);
    }
  }

  onDropPickInTeam(event: DndDropEvent, team?: TeamResponseDto) {
    if (!team)
      return;
    if (event.dropEffect !== 'copy' && event.dropEffect !== 'move')
      return;

    let index = event.index;

    if (typeof index === 'undefined') {
      index = team.players.length;
    }

    let player = this.MapToPlayer(event.data, this.grabbedThing);
    //console.log(event.data);
    //team.players.push(player);
    team.players.splice(index, 0, player);

    let trueIndex = 1;
    team.players.forEach((p, i) => {
      const matchingCaptainID = p.personID == player.personID;
      if (matchingCaptainID && i !== index) {
        // Ignore ghost player.
      }
      else {
        p.round = trueIndex++;
        //console.log(player.name + ' is now round ' + player.round);
      }
    });

    if (this.grabbedThing === GrabbedThing.Player) {
      const grabbedPlayer = event.data as GrabbedPlayer;
      if (team.captain.personID !== grabbedPlayer.team.captain.personID) {
        // Need to update previous team
        this.resortPreviousTeam(grabbedPlayer);
      }
      // Announce player moved
      this.api.movePlayerToTeam({
        seasonID: this.seasonID,
        previousCaptainID: grabbedPlayer.team.captain.personID,
        newCaptainID: team.captain.personID,
        personID: player.personID,
        roundNumber: player.round!, // MUST NOT BE NULL :(
      }).subscribe({next: r => console.log(r)});
    }
    else {
      // Announce player added to team
      this.api.addPlayerToTeam({
        seasonID: this.seasonID,
        captainID: team.captain.personID,
        personID: player.personID,
      }).subscribe({ next: r => console.log(r) });
    }

    console.log(team.captain.name + '\'s ' + player.round + ' pick is ' + player.name);
    //console.log(team.players.length)
  }

  MapToPlayer(data: any, t: GrabbedThing): PlayerDto {
    let player = new PlayerDto();

    if (t === GrabbedThing.AvailablePlayer) {
      player = data as PlayerDto;
      player.round = null;
      player.pick = null;
    } else if (t === GrabbedThing.Player) {
      const grabbedPlayer = data as GrabbedPlayer;
      player = grabbedPlayer.player; // Not sure we should be nulling these out...
      player.round = null;
      player.pick = null;
    } else if (t === GrabbedThing.Team) {
      const captain = data.captain as CaptainDto;
      player.personID = captain.personID;
      player.name = captain.name;
      player.round = null;
      player.pick = null;
    } else {
      console.log(data);
      throw new Error('Unable to map to player');
    }

    return player;
  }

  MapToTeam(data: any, t: GrabbedThing): TeamResponseDto {
    let team = new TeamResponseDto();
    team.captain = new CaptainDto();
    team.players = [];

    if (t === GrabbedThing.AvailablePlayer) {
      const player = data as PlayerDto;
      team.teamName = player.name + "'s Team";
      team.captain.name = player.name;
      team.captain.personID = player.personID;
    } else if (t === GrabbedThing.Team) {
      team = data as TeamResponseDto;
    } else {
      console.log(data);
      throw new Error('Unable to map to team');
    }

    return team;
  }

  DisableDraggingTeams(): boolean {
    // return true if CaptainsLocked or missing EventManager role

    if (this.captainLocked)
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

    const currentCaptain = this.CurrentCaptainOnDeck();
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

  CurrentCaptainOnDeck(): CaptainDto | null {
    // If there are no teams then it is no ones turn yet
    if (this.teams.length === 0)
      return null;

    if (this.playerPool.length === 0)
      return null;

    const sorted = [...this.teams].sort((a, b) => {
      const aPlayerCount = a.players.length;
      const BPlayerCount = b.players.length;
      return aPlayerCount - BPlayerCount;
    })

    const first = sorted[0];
    return first.captain;
  }

  StatusText(): string {
    // If there are no teams then it is no ones turn yet
    if (this.teams.length === 0)
      return "No Teams Added";

    if (this.playerPool.length === 0)
      return "Player pool is empty";

    const sorted = [...this.teams].sort((a, b) => {
      const aPlayerCount = a.players.length;
      const BPlayerCount = b.players.length;
      return aPlayerCount - BPlayerCount;
    })

    const first = sorted[0];
    return first.captain.name + " is on deck";
  }

}
