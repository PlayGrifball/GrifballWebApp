import { CommonModule, DOCUMENT, isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, DestroyRef, effect, ElementRef, Injector, input, signal, Signal, WritableSignal, ChangeDetectionStrategy, PLATFORM_ID, inject, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { parseSeasonMatchPage, SeasonMatchPageDto } from './seasonMatchPageDto';
import { BoxScoreComponent } from './boxScore/boxScore.component';
import { ErrorMessageComponent } from '../../shared/validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { RegexMatchValidValidatorDirective } from '../../shared/validation/directives/regexMatchValidValidatorDirective.directive';
import type { PossibleMatchDto, PossiblePlayerDto } from '@api/models';
import { MatTableModule } from '@angular/material/table';
import { AccountService } from '../../account.service';
import { map, Observable, Subscription, switchMap, takeWhile, timer } from 'rxjs';
import type { OnDemandJobStatusDto } from '@api/models';
import { nullWhenNotFound } from '../../api/nullWhenNotFound';
import { SeasonMatchService } from '@api/services/season-match.service';
import { TeamService } from '@api/services/team.service';

import { ToDatePipe } from '../../shared/toDate.pipe';
import { errorText } from '../../shared/errorText';
import type { TeamPagePlayerDto } from '@api/models';
import { provideDatePickers } from '../../shared/datePickers';
import { VodListComponent } from '../../shared/vodList/vodList.component';
import { GameSlot, ManualGamesComponent, slotChoices } from './manualGames/manualGames.component';

/** The commissioner actions on a match that are confirmed first. Each is a POST to the endpoint of the same name. */
export type MatchAction = 'HomeForfeit' | 'AwayForfeit' | 'DoubleForfeit' | 'UndoResult';

export const actionPrompts: Record<MatchAction, string> = {
  HomeForfeit: 'The home team forfeits and the away team wins.',
  AwayForfeit: 'The away team forfeits and the home team wins.',
  DoubleForfeit: 'Both teams forfeit and take a loss. In the playoffs nobody advances: the matches waiting on this one get a BYE instead.',
  UndoResult: 'This match goes back to undecided: its result is cleared, and any team or BYE it sent on to later matches is taken back out. After a forfeit, the games played before it stay. Otherwise its score and games are cleared too: Waypoint games can be reported again, but games entered by hand are deleted for good. Not possible once one of those later matches has a result or games of its own.',
};

/**
 * What the commissioner's tools on the page are doing, one thing at a time: nothing; waiting for an action's Confirm;
 * waiting to be told where a Waypoint game reported to a series with games goes (new, or replacing one); or sending one
 * of those to the server. Asking for one thing drops whatever else was waiting, manual entry's too (and starting something
 * there drops what waits here), and the page stays submitting until its reload is in.
 */
export type MatchPageMode =
  | { kind: 'idle' }
  | { kind: 'confirm', action: MatchAction }
  | { kind: 'placeReport', matchID: string }
  | { kind: 'submitting' };

const IDLE: MatchPageMode = { kind: 'idle' };

/** How often a queued or running pull of the players' games is looked at again. */
export const PULL_POLL_MS = 3000;

@Component({
    selector: 'app-season-match',
    imports: [
        CommonModule,
        RouterModule,
        FormsModule,
        MatInputModule,
        MatFormFieldModule,
        MatButtonModule,
        ErrorMessageComponent,
        RegexMatchValidValidatorDirective,
        MtxDatetimepickerModule,
        MatTableModule,
        ToDatePipe,
        VodListComponent,
        BoxScoreComponent,
        ManualGamesComponent,
    ],
    templateUrl: './seasonMatch.component.html',
    styleUrl: './seasonMatch.component.scss',
    providers: [provideDatePickers()],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonMatchComponent {
  private seasonMatchService = inject(SeasonMatchService);
  private teamService = inject(TeamService);
  accountService = inject(AccountService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly document = inject(DOCUMENT);

  /** Manual entry, below the box score: the page and it never both wait for the commissioner (see {@link start}). */
  private readonly manualGames = viewChild.required<Pick<ManualGamesComponent, 'cancel' | 'saving'>>('manualGames');
  /** The commissioner tools' heading: where focus goes when the control that had it is gone. */
  private readonly reportHeading = viewChild<ElementRef<HTMLElement>>('reportHeading');

  seasonMatchID: Signal<number> = input.required<number>()
  seasonMatch: WritableSignal<SeasonMatchPageDto | null> = signal(null);

  /** What the commissioner's tools are doing (see {@link MatchPageMode}). Changed only by the methods below. */
  private readonly _mode = signal<MatchPageMode>(IDLE);
  readonly mode = this._mode.asReadonly();
  /** A report or an action is being sent: the tools wait for it. */
  readonly submitting = computed(() => this.mode().kind === 'submitting');
  /** Something is being sent, here or by manual entry: nothing else starts or is sent meanwhile (the tools wait). */
  readonly busy = computed(() => this.submitting() || this.manualGames().saving());
  /** The action waiting for the commissioner to confirm it, if any. */
  readonly confirming = computed(() => { const mode = this.mode(); return mode.kind === 'confirm' ? mode.action : null; });
  /** The Waypoint game waiting for the commissioner to say whether it is a new game or replaces one, if any. */
  readonly placingReport = computed(() => { const mode = this.mode(); return mode.kind === 'placeReport' ? mode.matchID : null; });
  /** Where that game can go: a new game, or in place of each game reported. */
  readonly reportSlots = computed(() => slotChoices(this.seasonMatch()));

  readonly actionPrompts = actionPrompts;
  /** Why the server turned the last action down. */
  actionError: WritableSignal<string | null> = signal(null);

  /** A result or reported games to clear, as the server says (a match with a BYE is decided by the bracket). */
  readonly canUndo = computed(() => this.seasonMatch()?.canUndo ?? false);

  /** A result is in (or a BYE decided the match), as the server says. */
  readonly isDecided = computed(() => (this.seasonMatch()?.decidedBy ?? null) !== null);

  private readonly homeTeamID = computed(() => this.seasonMatch()?.homeTeamID ?? null);
  private readonly awayTeamID = computed(() => this.seasonMatch()?.awayTeamID ?? null);
  /** Each side's players, from its team page (null while loading). Reloading the match page does not reload them. */
  private readonly homeLoaded = signal<Roster>({ teamID: null, players: [] });
  private readonly awayLoaded = signal<Roster>({ teamID: null, players: [] });
  readonly homeRoster = computed(() => this.homeLoaded().players ?? []);
  readonly awayRoster = computed(() => this.awayLoaded().players ?? []);
  /** The page has answered (with a match or without). */
  readonly pageLoaded = signal(false);
  /** The match and both rosters are in: the page is shown then, in one go, so the rosters do not
   * push the score and the rest down as they arrive. */
  readonly ready = computed(() => this.pageLoaded()
    && isLoaded(this.homeLoaded(), this.homeTeamID()) && isLoaded(this.awayLoaded(), this.awayTeamID()));

  /** Selected game tab on the scoreboard: 'series' for series totals, or the matchNumber of a specific game. */
  readonly selectedGameTab = signal<'series' | number>('series');

  readonly availableTabs = computed(() => {
    const match = this.seasonMatch();
    if (!match || !match.reportedGames?.length)
      return [];
    const tabs: Array<{ id: 'series' | number; label: string }> = [];
    if (match.reportedGames.length > 1) {
      tabs.push({ id: 'series', label: 'Series Totals' });
    }
    for (const game of match.reportedGames) {
      tabs.push({ id: game.matchNumber, label: `Game ${game.matchNumber}` });
    }
    return tabs;
  });

  readonly activeScoreboard = computed(() => {
    const match = this.seasonMatch();
    if (!match || !match.reportedGames?.length)
      return null;
    const tab = this.selectedGameTab();
    if (tab === 'series' && match.seriesTotals) {
      return {
        title: 'Series Totals',
        matchID: null,
        recordedGameID: null,
        game: null,
        details: '',
        homeScore: match.homeTeamScore,
        awayScore: match.awayTeamScore,
        homePlayers: match.seriesTotals.homePlayers ?? [],
        awayPlayers: match.seriesTotals.awayPlayers ?? [],
        isSeries: true
      };
    }
    const game = match.reportedGames.find(g => g.matchNumber === tab) ?? match.reportedGames[0];
    return {
      title: `Game ${game.matchNumber}`,
      matchID: game.matchID,
      recordedGameID: game.recordedGameID ?? null,
      /** The game itself: a game entered by hand is edited and deleted from here. */
      game,
      /** A recorded game's game, map and gametype, as one line. */
      details: [game.gameVersionName, game.map, game.gametype].filter(x => !!x).join(' · '),
      homeScore: game.homeScore ?? null,
      awayScore: game.awayScore ?? null,
      homePlayers: game.homePlayers ?? [],
      awayPlayers: game.awayPlayers ?? [],
      isSeries: false
    };
  });

  possibleMatches: WritableSignal<PossibleMatchDto[]> = signal([]);
  /** Where the search for possible matches is: under way (also while the page is drawn on the
   * server, which never asks), done (the list, or none), or failed. */
  readonly possibleMatchesState = signal<'loading' | 'loaded' | 'error'>('loading');

  /** The pull of the players' recent games from Halo Infinite (a job on the worker): where it is, null until known. */
  readonly pull = signal<OnDemandJobStatusDto | null>(null);
  /** The pull is queued or running: its button waits, and the page looks again every few seconds. */
  readonly pulling = computed(() => this.pull()?.state === 'Queued' || this.pull()?.state === 'Running');
  readonly pullError = signal<string | null>(null);
  private pullWatch?: Subscription;

  public displayedColumns: string[] = ['gamertag', 'score', 'kills', 'deaths'];

  columns = [
    {
      columnDef: 'gamertag',
      header: 'Gamertag',
      cell: (element: PossiblePlayerDto) => `${element.gamertag}`,
    },
    {
      columnDef: 'score',
      header: 'Score',
      cell: (element: PossiblePlayerDto) => `${element.score}`,
    },
    {
      columnDef: 'kills',
      header: 'Kills',
      cell: (element: PossiblePlayerDto) => `${element.kills}`,
    },
    {
      columnDef: 'deaths',
      header: 'Deaths',
      cell: (element: PossiblePlayerDto) => `${element.deaths}`,
    },
  ];

  matchID: string = "";
  regex: string = String.raw`^[{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?$`;
  regexErrorMessage: string = "Must be a valid GUID";

  constructor() {
    effect(() => {
      this.seasonMatchID();
      this.getPageDto();
      this.getPossibleMatches();
      this.getPullStatus();
    })
    effect(() => this.loadRoster(this.homeTeamID(), this.homeLoaded));
    effect(() => this.loadRoster(this.awayTeamID(), this.awayLoaded));
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

  /** Loads the match; the subscription ends once it is in (or failed). */
  getPageDto(): Subscription {
    return this.seasonMatchService.getSeasonMatchPage({ seasonMatchID: this.seasonMatchID() })
      .pipe(map(parseSeasonMatchPage))
      .subscribe({
        next: result => {
          this.seasonMatch.set(result);
          if (result?.reportedGames?.length === 1) {
            this.selectedGameTab.set(result.reportedGames[0].matchNumber);
          } else {
            this.selectedGameTab.set('series');
          }
          this.pageLoaded.set(true);
        },
        error: () => this.pageLoaded.set(true),
      });
  }

  getPossibleMatches(): void {
    // The games already pulled from Halo Infinite (the pull is a job: getPullStatus, pullMatches). Not while the page
    // is rendered on the server: the browser asks once it has the page.
    this.possibleMatchesState.set('loading');
    if (!this.isBrowser)
      return;
    this.seasonMatchService.getPossibleMatches({ seasonMatchID: this.seasonMatchID() })
      .subscribe({
        next: result => {
          this.possibleMatches.set(result ?? []);
          this.possibleMatchesState.set('loaded');
        },
        error: () => {
          this.possibleMatches.set([]);
          this.possibleMatchesState.set('error');
        },
      });
  }

  /** Where the pull of the players' recent games is; one queued or running is watched until it's done. */
  getPullStatus(): void {
    this.pullWatch?.unsubscribe();
    this.pull.set(null);
    this.pullError.set(null);
    if (!this.isBrowser)
      return;
    this.seasonMatchService.getMatchPullStatus({ seasonMatchID: this.seasonMatchID() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => this.showPull(status),
        error: () => this.pull.set(null),
      });
  }

  /**
   * Queues a pull of the players' recent games from Halo Infinite (organizers): the worker runs it within a few seconds.
   * `backToSeasonStart`: back through their histories to the start of the match's season, not just their most recent games.
   */
  pullMatches(backToSeasonStart = false): void {
    this.pullError.set(null);
    this.seasonMatchService.pullMatches({ seasonMatchID: this.seasonMatchID(), backToSeasonStart: backToSeasonStart || undefined })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => this.showPull(status),
        error: (error: HttpErrorResponse) => this.pullError.set(errorText(error, "Couldn't start the pull.")),
      });
  }

  private showPull(status: OnDemandJobStatusDto): void {
    this.pull.set(status);
    if (status.state === 'Idle')
      return;
    // Look again every few seconds until it's done, then read the games it found
    this.pullWatch?.unsubscribe();
    this.pullWatch = timer(PULL_POLL_MS, PULL_POLL_MS).pipe(
      switchMap(() => this.seasonMatchService.getMatchPullStatus({ seasonMatchID: this.seasonMatchID() })),
      takeWhile(next => next.state !== 'Idle', true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: next => {
        this.pull.set(next);
        if (next.state === 'Idle')
          this.getPossibleMatches();
      },
      error: () => this.pullError.set("Couldn't tell whether the pull finished. Reload the page to check."),
    });
  }

  /** A Waypoint game to report: straight in as game 1 of an empty series, else once the commissioner says where it goes. */
  onSubmit(matchID: string): void {
    if (this.busy())
      return;
    if (this.seasonMatch()?.reportedGames.length) {
      // The slot choice opens beside what asked for it: Cancel goes back there
      const opener = this.focused();
      this.start({ kind: 'placeReport', matchID });
      this.returnFocus = () => opener;
      return;
    }
    this.send(this.seasonMatchService.reportMatch({ seasonMatchID: this.seasonMatchID(), matchID }));
  }

  /** Reports the Waypoint game waiting to be placed, where the commissioner said it goes. */
  placeReport(slot: GameSlot): void {
    const mode = this.mode();
    if (mode.kind !== 'placeReport')
      return;
    const replaces = typeof slot === 'number' ? slot : undefined;
    this.send(this.seasonMatchService.reportMatch({ seasonMatchID: this.seasonMatchID(), matchID: mode.matchID, replaces }));
  }

  /** Asks the commissioner to confirm an action before it is sent; a game waiting to be placed is dropped. */
  requestAction(action: MatchAction): void {
    if (this.busy())
      return;
    this.start({ kind: 'confirm', action });
    // The action's button is gone while its Confirm waits (a slot choice too): focus goes to the section's heading,
    // and back to the button (drawn again) on Cancel
    this.focusAfterRender();
    this.returnFocus = () => this.document.querySelector<HTMLElement>(`[data-action="${action}"]`);
  }

  /** Drops the action or the game waiting for the commissioner, focus back where it was asked for; nothing once it is being sent. */
  cancel(): void {
    if (this.submitting() || this.mode().kind === 'idle')
      return;
    this._mode.set(IDLE);
    this.focusAfterRender(this.returnFocus);
    this.returnFocus = null;
  }

  confirmAction(): void {
    const action = this.confirming();
    if (action === null)
      return;
    this.send(this.actionCall(action));
  }

  /** The generated service call for a commissioner action, each a POST to the endpoint of the same name. */
  private actionCall(action: MatchAction): Observable<void> {
    const seasonMatchID = this.seasonMatchID();
    switch (action) {
      case 'HomeForfeit': return this.seasonMatchService.homeForfeit({ seasonMatchID });
      case 'AwayForfeit': return this.seasonMatchService.awayForfeit({ seasonMatchID });
      case 'DoubleForfeit': return this.seasonMatchService.doubleForfeit({ seasonMatchID });
      case 'UndoResult': return this.seasonMatchService.undoResult({ seasonMatchID });
    }
  }

  /**
   * Manual entry started something (a game in its form, or an unreport waiting for its Confirm): what the page had
   * waiting for the commissioner is dropped, so the two never both wait. Focus stays with manual entry.
   */
  dropWaiting(): void {
    if (!this.submitting())
      this._mode.set(IDLE);
    this.returnFocus = null;
  }

  /** Manual entry added, changed or unreported a game: nothing the page had waiting still holds, and it reloads. */
  onManualChanged(): void {
    this.dropWaiting();
    this.getPageDto();
  }

  /** Waits for the commissioner (a Confirm, or a slot choice): manual entry drops whatever it had waiting. */
  private start(mode: MatchPageMode): void {
    this.manualGames().cancel();
    this._mode.set(mode);
  }

  /**
   * Sends a report or an action; whatever the answer, the page reloads and is idle again once the reload is in, so
   * no tool is offered against the match as it was. A refusal says why. Nothing while manual entry is saving. Focus
   * goes to the section's heading, as the panel or button that sent it is gone or waits.
   */
  private send(call: Observable<void>): void {
    if (this.busy())
      return;
    this.manualGames().cancel();
    this._mode.set({ kind: 'submitting' });
    this.returnFocus = null;
    this.focusAfterRender();
    this.actionError.set(null);
    call
      .subscribe({
        error: (e: HttpErrorResponse) => this.actionError.set(errorText(e, 'The server could not do that.')),
      }).add(() => this.getPageDto().add(() => this._mode.set(IDLE)));
  }

  /** Where Cancel puts focus back: the control that opened what is waiting, if it is still on the page. */
  private returnFocus: (() => HTMLElement | null) | null = null;

  /** The element with focus. */
  private focused(): HTMLElement | null {
    return this.document.activeElement as HTMLElement | null;
  }

  /** Once the page is drawn again (in the browser), focuses `target` if it is (still) on the page, else the Report Game heading. */
  private focusAfterRender(target?: (() => HTMLElement | null) | null): void {
    afterNextRender({
      write: () => {
        const element = target?.() ?? null;
        (element?.isConnected ? element : this.reportHeading()?.nativeElement)?.focus();
      },
    }, { injector: this.injector });
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
