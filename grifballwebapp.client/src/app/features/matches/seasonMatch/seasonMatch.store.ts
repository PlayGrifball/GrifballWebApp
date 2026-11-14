import { isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, effect, inject, Injectable, PLATFORM_ID, signal, WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { map, Observable, Subscription, switchMap, takeWhile, timer } from 'rxjs';
import type { OnDemandJobStatusDto, PossibleMatchDto, TeamPagePlayerDto } from '@api/models';
import { SeasonMatchService } from '@api/services/season-match.service';
import { TeamService } from '@api/services/team.service';
import { nullWhenNotFound } from '@core/http/nullWhenNotFound';
import { errorText } from '@shared/errorText';
import { parseSeasonMatchPage, SeasonMatchPageDto } from './seasonMatchPageDto';

/** The commissioner actions on a match that are confirmed first. Each is a POST to the endpoint of the same name. */
export type MatchAction = 'HomeForfeit' | 'AwayForfeit' | 'DoubleForfeit' | 'UndoResult';

/** How often a queued or running pull of the players' games is looked at again. */
export const PULL_POLL_MS = 3000;

/**
 * A season match page's data and its server calls: the match, each side's roster, the Waypoint games pulled for it and
 * the pull that finds them, and the reports and commissioner actions sent. One per page (the component provides it).
 */
@Injectable()
export class SeasonMatchStore {
  private readonly seasonMatchService = inject(SeasonMatchService);
  private readonly teamService = inject(TeamService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly destroyRef = inject(DestroyRef);

  /** The match shown, set by {@link open}. */
  private seasonMatchID = 0;

  private readonly _seasonMatch = signal<SeasonMatchPageDto | null>(null);
  readonly seasonMatch = this._seasonMatch.asReadonly();
  /** The page has answered (with a match or without). */
  private readonly _pageLoaded = signal(false);
  readonly pageLoaded = this._pageLoaded.asReadonly();

  private readonly homeTeamID = computed(() => this.seasonMatch()?.homeTeamID ?? null);
  private readonly awayTeamID = computed(() => this.seasonMatch()?.awayTeamID ?? null);
  /** Each side's players, from its team page (null while loading). Reloading the match page does not reload them. */
  private readonly homeLoaded = signal<Roster>({ teamID: null, players: [] });
  private readonly awayLoaded = signal<Roster>({ teamID: null, players: [] });
  readonly homeRoster = computed(() => this.homeLoaded().players ?? []);
  readonly awayRoster = computed(() => this.awayLoaded().players ?? []);
  /** The match and both rosters are in: the page is shown then, in one go, so the rosters do not
   * push the score and the rest down as they arrive. */
  readonly ready = computed(() => this.pageLoaded()
    && isLoaded(this.homeLoaded(), this.homeTeamID()) && isLoaded(this.awayLoaded(), this.awayTeamID()));

  private readonly _possibleMatches = signal<PossibleMatchDto[]>([]);
  readonly possibleMatches = this._possibleMatches.asReadonly();
  /** Where the search for possible matches is: under way (also while the page is drawn on the
   * server, which never asks), done (the list, or none), or failed. */
  private readonly _possibleMatchesState = signal<'loading' | 'loaded' | 'error'>('loading');
  readonly possibleMatchesState = this._possibleMatchesState.asReadonly();

  /** The pull of the players' recent games from Halo Infinite (a job on the worker): where it is, null until known. */
  private readonly _pull = signal<OnDemandJobStatusDto | null>(null);
  readonly pull = this._pull.asReadonly();
  /** The pull is queued or running: its button waits, and the page looks again every few seconds. */
  readonly pulling = computed(() => this.pull()?.state === 'Queued' || this.pull()?.state === 'Running');
  private readonly _pullError = signal<string | null>(null);
  readonly pullError = this._pullError.asReadonly();
  private pullWatch?: Subscription;

  constructor() {
    effect(() => this.loadRoster(this.homeTeamID(), this.homeLoaded));
    effect(() => this.loadRoster(this.awayTeamID(), this.awayLoaded));
  }

  /**
   * Shows another match: nothing of the previous one stays (its teams would title the page). Loads it, its possible
   * matches and its pull; unsubscribing the result cancels the match's request, so a late answer can't land on the next.
   */
  open(seasonMatchID: number): Subscription {
    this.seasonMatchID = seasonMatchID;
    this._seasonMatch.set(null);
    const page = this.reload();
    this.getPossibleMatches();
    this.getPullStatus();
    return page;
  }

  /** Loads the match again; the subscription ends once it is in (or failed). */
  reload(): Subscription {
    return this.seasonMatchService.getSeasonMatchPage({ seasonMatchID: this.seasonMatchID })
      .pipe(map(parseSeasonMatchPage))
      .subscribe({
        next: result => {
          this._seasonMatch.set(result);
          this._pageLoaded.set(true);
        },
        error: () => this._pageLoaded.set(true),
      });
  }

  private loadRoster(teamID: number | null, roster: WritableSignal<Roster>): void {
    roster.set({ teamID, players: teamID === null ? [] : null });
    if (teamID === null)
      return;
    this.teamService.team({ teamID }).pipe(nullWhenNotFound())
      .subscribe({
        next: team => roster.set({ teamID, players: team?.players ?? [] }),
        error: () => roster.set({ teamID, players: [] }),
      });
  }

  private getPossibleMatches(): void {
    // The games already pulled from Halo Infinite (the pull is a job: getPullStatus, pullMatches). Not while the page
    // is rendered on the server: the browser asks once it has the page.
    this._possibleMatchesState.set('loading');
    if (!this.isBrowser)
      return;
    this.seasonMatchService.getPossibleMatches({ seasonMatchID: this.seasonMatchID })
      .subscribe({
        next: result => {
          this._possibleMatches.set(result ?? []);
          this._possibleMatchesState.set('loaded');
        },
        error: () => {
          this._possibleMatches.set([]);
          this._possibleMatchesState.set('error');
        },
      });
  }

  /** Where the pull of the players' recent games is; one queued or running is watched until it's done. */
  private getPullStatus(): void {
    this.pullWatch?.unsubscribe();
    this._pull.set(null);
    this._pullError.set(null);
    if (!this.isBrowser)
      return;
    this.seasonMatchService.getMatchPullStatus({ seasonMatchID: this.seasonMatchID })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => this.showPull(status),
        error: () => this._pull.set(null),
      });
  }

  /**
   * Queues a pull of the players' recent games from Halo Infinite (organizers): the worker runs it within a few seconds.
   * `backToSeasonStart`: back through their histories to the start of the match's season, not just their most recent games.
   */
  pullMatches(backToSeasonStart = false): void {
    this._pullError.set(null);
    this.seasonMatchService.pullMatches({ seasonMatchID: this.seasonMatchID, backToSeasonStart: backToSeasonStart || undefined })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => this.showPull(status),
        error: (error: HttpErrorResponse) => this._pullError.set(errorText(error, "Couldn't start the pull.")),
      });
  }

  private showPull(status: OnDemandJobStatusDto): void {
    this._pull.set(status);
    if (status.state === 'Idle')
      return;
    // Look again every few seconds until it's done, then read the games it found
    this.pullWatch?.unsubscribe();
    this.pullWatch = timer(PULL_POLL_MS, PULL_POLL_MS).pipe(
      switchMap(() => this.seasonMatchService.getMatchPullStatus({ seasonMatchID: this.seasonMatchID })),
      takeWhile(next => next.state !== 'Idle', true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: next => {
        this._pull.set(next);
        if (next.state === 'Idle')
          this.getPossibleMatches();
      },
      error: () => this._pullError.set("Couldn't tell whether the pull finished. Reload the page to check."),
    });
  }

  /** Reports a Waypoint game: a new game of the series, or in place of game `replaces`. */
  report(matchID: string, replaces?: number): Observable<void> {
    return this.seasonMatchService.reportMatch({ seasonMatchID: this.seasonMatchID, matchID, replaces });
  }

  /** A commissioner action, each a POST to the endpoint of the same name. */
  act(action: MatchAction): Observable<void> {
    const seasonMatchID = this.seasonMatchID;
    switch (action) {
      case 'HomeForfeit': return this.seasonMatchService.homeForfeit({ seasonMatchID });
      case 'AwayForfeit': return this.seasonMatchService.awayForfeit({ seasonMatchID });
      case 'DoubleForfeit': return this.seasonMatchService.doubleForfeit({ seasonMatchID });
      case 'UndoResult': return this.seasonMatchService.undoResult({ seasonMatchID });
    }
  }
}

/** A side's players and the team they were loaded for; players is null while loading. */
interface Roster {
  teamID: number | null,
  players: TeamPagePlayerDto[] | null,
}

/** The roster of this team has arrived. */
function isLoaded(roster: Roster, teamID: number | null): boolean {
  return roster.teamID === teamID && roster.players !== null;
}
