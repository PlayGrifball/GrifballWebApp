import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, numberAttribute, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { TeamResponseDto } from '@api/models';
import type { TeamsPlayerDto } from '@api/models';
import { DndDropEvent, DndModule } from 'ngx-drag-drop';
import { GrabbedPlayer, GrabbedThing, GrabbedThingTypes } from './personStatus';
import { AccountService } from '@core/auth/account.service';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { SeasonTabsComponent } from '../../league';
import { TeamBuilderStore } from './teamBuilder.store';

@Component({
    selector: 'app-team-builder',
    imports: [
        SeasonTabsComponent,
        DndModule,
        MatListModule,
        MatIconModule,
        MatButtonModule,
        MatCardModule,
        RouterLink,
    ],
    templateUrl: './teamBuilder.component.html',
    styleUrl: './teamBuilder.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [TeamBuilderStore],
    // ngx-drag-drop takes its placeholders (dndPlaceholderRef) out of the DOM, so the browser's DOM
    // no longer matches what the server drew: the browser draws this page again instead of taking
    // the server's over (the rest of the page, the header with the user, still is).
    host: { ngSkipHydration: 'true' },
})
export class TeamBuilderComponent implements OnInit {
  /** The draft: loaded, kept up to date, and changed a move at a time. */
  readonly store = inject(TeamBuilderStore);
  accountService = inject(AccountService);

  /** The :seasonID of the route (0: the current season). Angular keeps this page when only it changes, so a new one
   * loads that season's draft in place of the one shown. */
  readonly seasonID = input.required({ transform: (value: unknown) => numberAttribute(value, 0) });
  /** The season shown: the route's, or the current season's ID once known. */
  protected readonly shownSeasonID = this.store.shownSeasonID;
  readonly captainLocked = this.store.captainLocked;
  readonly teams = this.store.teams;
  readonly playerPool = this.store.playerPool;
  readonly onDeck = this.store.onDeck;

  private grabbedThing: GrabbedThing = GrabbedThing.AvailablePlayer;

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

  constructor() {
    effect(() => {
      const seasonID = this.seasonID();
      untracked(() => this.store.show(seasonID));
    });
  }

  ngOnInit(): void {
    // Other users' changes, for as long as the page is open: from its first change detection, the pass in which the
    // effect above sets its season, so a reconnect never loads the draft of season 0
    this.store.listen();
  }

  /** Loads the draft from the server again. */
  load(): void {
    this.store.load();
  }

  lockCaptainsButtonClicked(): void {
    this.store.toggleCaptainLock();
  }

  onDragStart(item: GrabbedThingTypes, event: DragEvent, kind: GrabbedThing) {
    this.grabbedThing = kind;
    this.store.dragStarted();
  }

  /** A drag ended (dropped or not): a reload that waited for it goes ahead, after any save. */
  onDragEnd(): void {
    this.store.dragEnded();
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
      this.store.addCaptain(player, this.MapToTeam(player, GrabbedThing.AvailablePlayer).teamName, index);
    }
    // Team -> Team = Resort team
    else {
      const team = this.MapToTeam(event.data, this.grabbedThing);
      this.store.resortTeam(team.captain.personID, index);
    }
  }

  createDraggedPlayer(player: TeamsPlayerDto, team: TeamResponseDto): GrabbedPlayer {
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
      this.store.movePoolPlayer(player.personID, index);
    }
    // Player -> AvailablePlayer = Player is now available
    else if (this.grabbedThing === GrabbedThing.Player) {
      const grabbedPlayer = event.data as GrabbedPlayer;
      this.store.undraft(grabbedPlayer.team.captain.personID, grabbedPlayer.player.personID, index);
    }
    // Team -> AvailablePlayer = the captain and their picks are available again
    else if (this.grabbedThing === GrabbedThing.Team) {
      const team = event.data as TeamResponseDto;
      this.store.disband(team.captain.personID, index);
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
    if (this.grabbedThing === GrabbedThing.Player) {
      const grabbedPlayer = event.data as GrabbedPlayer;
      this.store.movePick(grabbedPlayer.team.captain.personID, captainID, grabbedPlayer.player.personID, event.index, team.captain.name);
    }
    else {
      // The server always adds a pick as the team's last round, so the drop position is ignored.
      const picked = this.MapToPlayer(event.data, this.grabbedThing);
      this.store.pick(captainID, picked.personID, team.captain.name);
    }
  }

  /** The pool player a drag carries (drag data is a copy made by ngx-drag-drop). */
  MapToPlayer(data: unknown, t: GrabbedThing): TeamsPlayerDto {
    if (t === GrabbedThing.AvailablePlayer) {
      return { ...(data as TeamsPlayerDto), round: null, pick: null };
    } else {
      console.log(data);
      throw new Error('Unable to map to player');
    }
  }

  /** The team a drag carries, or the new team a pool player would captain. */
  MapToTeam(data: unknown, t: GrabbedThing): TeamResponseDto {
    if (t === GrabbedThing.AvailablePlayer) {
      const player = data as TeamsPlayerDto;
      return { teamID: 0, teamName: player.name + "'s Team", captain: { name: player.name, personID: player.personID, order: null, region: null }, players: [] };
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
    return !this.accountService.isEventOrganizer();
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

  DisableDraggingPlayerFromPool(playerDto: TeamsPlayerDto): boolean {
    return false; // Should only be true if has EventManager, OR has Player role & is captain & is their turn to pick
  }


}
