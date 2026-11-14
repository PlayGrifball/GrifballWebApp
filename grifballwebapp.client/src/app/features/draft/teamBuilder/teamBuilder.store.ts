import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatSnackBar } from '@angular/material/snack-bar';
import type { CaptainDto, CaptainPlacementDto, RemoveCaptainDto, TeamResponseDto, TeamsPlayerDto } from '@api/models';
import { SeasonService } from '@api/services/season.service';
import { TeamsService } from '@api/services/teams.service';
import { SignalRService } from '@core/signalr/signalR.service';
import { errorText } from '@shared/errorText';
import { whileCurrent } from '@shared/whileCurrent';
import { catchError, finalize, forkJoin, map, Observable, of, Subject, switchMap } from 'rxjs';
import { addCaptain, addPlayerToTeam, Draft, dropPick, dropTeam, movePlayerToTeam, movePoolPlayer, removeCaptain, removePlayerFromTeam, renameTeam, resortCaptain } from './draft';

/**
 * A season's draft as the team builder shows and changes it: the teams, the player pool and whether captains are locked,
 * loaded from the server, kept up to date with other users' changes (SignalR), and changed here a move at a time, each
 * applied at once and saved. One per page (the component provides it).
 */
@Injectable()
export class TeamBuilderStore {
  private readonly draft = inject(TeamsService);
  private readonly seasons = inject(SeasonService);
  private readonly snackBarService = inject(MatSnackBar);
  private readonly signalR = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);

  /** This page's hub connection, for the server to leave out of the broadcast of its own change; none (no header) until it connects. */
  private get connectionID(): string | undefined {
    return this.signalR.hubConnection.connectionId || undefined;
  }

  /** The season asked for (0: the current season), set by {@link show}. */
  private readonly routeSeasonID = signal(0);
  /** The season shown: the one asked for, or the current season's ID once known. Set only by {@link show}, which resets the draft with it. */
  private readonly _shownSeasonID = signal(0);
  readonly shownSeasonID = this._shownSeasonID.asReadonly();
  readonly captainLocked = signal(true);
  readonly teams = signal<TeamResponseDto[]>([]);
  readonly playerPool = signal<TeamsPlayerDto[]>([]);

  // Every reload goes through here; a newer one cancels the one still loading (see load).
  private readonly reloads = new Subject<void>();
  private destroyed = false;
  // Saves still on their way, and whether a refusal asked for a reload once they are done.
  private savesInFlight = 0;
  private reloadAfterSaves = false;
  // Saves ever started here; a load that saw a save start while it ran does not apply its result.
  private savesStarted = 0;
  // A drag under way, and whether a reload waits for it to end (a reload would redraw the board
  // under the pointer).
  private dragging = false;
  private reloadAfterDrag = false;

  /** Whose pick it is: the first team (in pick order) with the fewest players, while players are left. */
  readonly onDeck = computed<CaptainDto | null>(() => {
    const teams = this.teams();
    if (teams.length === 0 || this.playerPool().length === 0)
      return null;
    return teams.reduce((fewest, team) => team.players.length < fewest.players.length ? team : fewest, teams[0]).captain;
  });

  constructor() {
    // The server's draft is the truth: the latest load replaces what the page shows, and later
    // SignalR updates apply on top of it. Teams and pool go together (one without the other could
    // show a player twice or nowhere), so if either fails neither is applied; the lock state is
    // taken on its own.
    const part = <T>(request: Observable<T>) => request.pipe(catchError(() => of(undefined)));
    this.reloads.pipe(
      switchMap(() => {
        const savesStarted = this.savesStarted;
        const seasonID = this.shownSeasonID();
        return forkJoin({
          teams: part(this.draft.getTeams({ seasonID })),
          pool: part(this.draft.getPlayerPool({ seasonID })),
          locked: part(this.draft.areCaptainsLocked({ seasonID })),
        }).pipe(whileCurrent(seasonID, this.shownSeasonID), map(snapshot => ({ ...snapshot, savesStarted })));
      }),
      takeUntilDestroyed(),
    ).subscribe(({ teams, pool, locked, savesStarted }) => {
      // A drop saved while this was loading may be missing from it: load again once that
      // save is done instead (SignalR updates do not count, so there is no loop).
      if (savesStarted !== this.savesStarted) {
        this.reloadAfterSaves = true;
        this.reloadIfSettled();
        return;
      }
      if (teams !== undefined && pool !== undefined) {
        this.teams.set(teams);
        this.playerPool.set(pool);
      }
      if (locked !== undefined)
        this.captainLocked.set(locked);
      if (teams === undefined || pool === undefined || locked === undefined)
        this.snackBarService.open('Failed to load the draft', 'Close');
    });
    this.destroyRef.onDestroy(() => this.destroyed = true);
  }

  /** Shows a season's draft (0: the current season's). Nothing of the previous one stays: a load of it still on its way is dropped. */
  show(seasonID: number): void {
    this.routeSeasonID.set(seasonID);
    this._shownSeasonID.set(seasonID);
    this.teams.set([]);
    this.playerPool.set([]);
    this.captainLocked.set(true);
    if (seasonID === 0) {
      this.seasons.getCurrentSeasonId()
        .pipe(whileCurrent(seasonID, this.routeSeasonID))
        .subscribe({
          next: (result) => {
            this._shownSeasonID.set(result);
            this.load();
          },
        });
    }
    else {
      this.load();
    }
  }

  /** Follows other users' changes until the page is gone (the hub connection outlives it, so the handlers are removed then). */
  listen(): void {
    const offs = [
      this.signalR.addCaptain(dto => this.applyRemote(dto.seasonID,
        draft => addCaptain(draft, dto.personID, dto.teamName, dto.captainName, dto.orderNumber, dto.teamID))),
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
        if (seasonID === this.shownSeasonID())
          this.captainLocked.set(true);
      }),
      this.signalR.unlockCaptains(seasonID => {
        if (seasonID === this.shownSeasonID())
          this.captainLocked.set(false);
      }),
      // The hub came up after this page loaded the draft (it was still connecting, or it dropped
      // and reconnected): a change sent before then never reached this page, so load the draft
      // again. Only on that change of state, so a load never leads to another.
      this.signalR.connected(() => this.reloadWhenIdle()),
    ];
    this.destroyRef.onDestroy(() => offs.forEach(off => off()));
  }

  /** Applies another user's change, if it is for this season. */
  private applyRemote(seasonID: number, change: (draft: Draft) => Draft | null): void {
    if (seasonID !== this.shownSeasonID())
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
      this.reloadAfterSaves = true;
      this.reloadIfSettled();
      return null;
    }
    if (next.teams !== current.teams)
      this.teams.set(next.teams);
    if (next.pool !== current.pool)
      this.playerPool.set(next.pool);
    return next;
  }

  /** Loads the draft from the server (see the constructor); nothing once the page is gone. */
  load(): void {
    if (!this.destroyed)
      this.reloads.next();
  }

  /** Locks the captains if they're unlocked, else unlocks them. */
  toggleCaptainLock(): void {
    const seasonID = this.shownSeasonID();
    if (this.captainLocked()) {
      this.draft.unlockCaptains({ seasonID, signalRConnectionID: this.connectionID })
        .subscribe({ next: () => this.captainLocked.set(false) });
    }
    else {
      this.draft.lockCaptains({ seasonID, signalRConnectionID: this.connectionID })
        .subscribe({ next: () => this.captainLocked.set(true) });
    }
  }

  /** A drag started: a reload waits for it to end. */
  dragStarted(): void {
    this.dragging = true;
  }

  /** A drag ended (dropped or not): a reload that waited for it goes ahead, after any save. */
  dragEnded(): void {
    this.dragging = false;
    if (this.reloadAfterDrag) {
      this.reloadAfterDrag = false;
      this.reloadWhenIdle();
    }
  }

  /** Loads the draft again once no drag is under way and no save is on its way. */
  private reloadWhenIdle(): void {
    if (this.dragging) {
      this.reloadAfterDrag = true;
      return;
    }
    this.reloadAfterSaves = true;
    this.reloadIfSettled();
  }

  /**
   * Sends a change; the page has already applied it. The request is not tied to the page: it
   * completes even if the user navigates away. If the server refuses it (not your turn, not
   * signed up, not allowed...), say why and reload the real draft once no other save is on its
   * way. `saved` gets the server's answer, for changes the server finishes itself. A success
   * never reloads, since a reload could replace a later change that is still on its way. Once
   * the page is gone, nothing is shown or loaded.
   */
  private save<T>(request: Observable<T>, saved?: (response: T) => void): void {
    this.savesInFlight++;
    this.savesStarted++;
    // finalize settles the count however the save ends, even if `saved` throws.
    request.pipe(finalize(() => {
      this.savesInFlight--;
      this.reloadIfSettled();
    })).subscribe({
      next: r => {
        console.log(r);
        if (saved && !this.destroyed)
          saved(r);
      },
      error: (e: HttpErrorResponse) => {
        if (this.destroyed)
          return;
        // TeamService's refusals come back from GlobalExceptionHandler as a 400 ProblemDetails
        // ({ title: "Request refused", detail: "<message>" }); an unexpected error is a 500 whose detail is a generic message.
        this.snackBarService.open(`Draft change not saved: ${errorText(e, 'the server refused it')}`, 'Close');
        // Reload once no other save from this page is on its way: an earlier snapshot could miss
        // one of them, and the server does not echo our own changes back.
        this.reloadAfterSaves = true;
      },
    });
  }

  private reloadIfSettled(): void {
    if (this.savesInFlight === 0 && this.reloadAfterSaves) {
      this.reloadAfterSaves = false;
      this.load();
    }
  }

  /**
   * A pool player becomes the captain of a new team at `index` in pick order. The server names the team (the signup's
   * team name, or "<display name>'s Team") and answers with that name; the broadcast to others skips this connection.
   */
  addCaptain(player: TeamsPlayerDto, teamName: string, index: number): void {
    const next = this.applyDraft(draft => addCaptain(draft, player.personID, teamName, player.name, index + 1));
    if (!next)
      return;
    const added = next.teams.find(t => t.captain.personID === player.personID)!;
    // Order must not be null
    const dto: CaptainPlacementDto = { seasonID: this.shownSeasonID(), personID: added.captain.personID, orderNumber: added.captain.order! };
    this.save(this.draft.addCaptain({ seasonID: dto.seasonID, signalRConnectionID: this.connectionID, body: dto }),
      saved => this.applyDraft(draft => renameTeam(draft, player.personID, saved.teamName, saved.teamID)));
    console.log('Added new captain ' + added.captain.name + ' with pick order ' + added.captain.order);
  }

  /** A team moves to `index` in pick order. */
  resortTeam(captainID: number, index: number): void {
    const next = this.applyDraft(draft => dropTeam(draft, captainID, index));
    if (!next)
      return;
    const moved = next.teams.find(t => t.captain.personID === captainID)!;
    // Order must not be null
    const dto: CaptainPlacementDto = { seasonID: this.shownSeasonID(), personID: moved.captain.personID, orderNumber: moved.captain.order! };
    this.save(this.draft.resortCaptain({ seasonID: dto.seasonID, signalRConnectionID: this.connectionID, body: dto }));
    console.log('Resorted captains ' + moved.captain.name + ' now has pick order ' + moved.captain.order);
  }

  /** A pool player moves to `index` in the pool: only the order changes, which the server does not keep. */
  movePoolPlayer(personID: number, index: number): void {
    this.applyDraft(draft => movePoolPlayer(draft, personID, index));
  }

  /** A pick goes back to the pool, at `index`. */
  undraft(captainID: number, personID: number, index: number): void {
    if (!this.applyDraft(draft => removePlayerFromTeam(draft, captainID, personID, index)))
      return;
    const seasonID = this.shownSeasonID();
    this.save(this.draft.removePlayerFromTeam({
      seasonID,
      signalRConnectionID: this.connectionID,
      body: { seasonID, captainID, personID },
    }));
  }

  /** A team is disbanded: the captain and their picks go back to the pool, at `index`. */
  disband(captainID: number, index: number): void {
    if (!this.applyDraft(draft => removeCaptain(draft, captainID, index)))
      return;
    // RemoveCaptainDto carries no order number: the server re-sequences the remaining captains 1..n itself
    // (TeamService.RemoveCaptain).
    const dto: RemoveCaptainDto = { seasonID: this.shownSeasonID(), personID: captainID };
    this.save(this.draft.removeCaptain({ seasonID: dto.seasonID, signalRConnectionID: this.connectionID, body: dto }));
  }

  /** A pick moves from one team to another (or within one), at `index` among its picks. */
  movePick(previousCaptainID: number, captainID: number, personID: number, index: number | undefined, teamName: string): void {
    const next = this.applyDraft(draft => dropPick(draft, previousCaptainID, captainID, personID, index));
    if (!next)
      return;
    const player = next.teams.find(t => t.captain.personID === captainID)!.players.find(p => p.personID === personID)!;
    const seasonID = this.shownSeasonID();
    this.save(this.draft.movePlayerToTeam({
      seasonID,
      signalRConnectionID: this.connectionID,
      body: { seasonID, previousCaptainID, newCaptainID: captainID, personID, roundNumber: player.round! }, // MUST NOT BE NULL :(
    }));
    console.log(teamName + '\'s ' + player.round + ' pick is ' + player.name);
  }

  /** A pool player is picked by a team. The server always adds a pick as the team's last round. */
  pick(captainID: number, personID: number, teamName: string): void {
    const next = this.applyDraft(draft => addPlayerToTeam(draft, captainID, personID));
    if (!next)
      return;
    const player = next.teams.find(t => t.captain.personID === captainID)!.players.find(p => p.personID === personID)!;
    const seasonID = this.shownSeasonID();
    this.save(this.draft.addPlayerToTeam({
      seasonID,
      signalRConnectionID: this.connectionID,
      body: { seasonID, captainID, personID },
    }));
    console.log(teamName + '\'s ' + player.round + ' pick is ' + player.name);
  }
}
