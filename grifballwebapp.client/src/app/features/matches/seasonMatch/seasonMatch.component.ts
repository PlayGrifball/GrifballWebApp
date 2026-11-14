import { DOCUMENT, NgTemplateOutlet, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, effect, ElementRef, Injector, input, linkedSignal, signal, Signal, WritableSignal, ChangeDetectionStrategy, inject, viewChild } from '@angular/core';
import { RouterModule } from '@angular/router';
import { SeasonMatchPageDto } from './seasonMatchPageDto';
import { MatchAction, SeasonMatchStore } from './seasonMatch.store';
import { BoxScoreComponent } from './boxScore/boxScore.component';
import { ErrorMessageComponent } from '@shared/validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { RegexMatchValidValidatorDirective } from '@shared/validation/directives/regexMatchValidValidatorDirective.directive';
import type { PossiblePlayerDto } from '@api/models';
import { MatTableModule } from '@angular/material/table';
import { AccountService } from '@core/auth/account.service';
import { Observable } from 'rxjs';

import { ToDatePipe } from '@shared/toDate.pipe';
import { errorText } from '@shared/errorText';
import { provideDatePickers } from '@shared/datePickers';
import { VodListComponent } from '@shared/vodList/vodList.component';
import { ManualGamesComponent } from './manualGames/manualGames.component';
import { GameSlot, slotChoices } from './manualGames/manualGameForm';
import { describePage, pageDate, PageDescription } from '@shared/pageMeta';

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

/**
 * The season match as search results and link previews show it: the two teams (and the series score once decided),
 * then the season and the result or when it is scheduled.
 */
export function describeSeasonMatch(match: SeasonMatchPageDto): PageDescription {
  const home = match.homeTeamName ?? 'TBD';
  const away = match.awayTeamName ?? 'TBD';
  let title = `${home} vs ${away}`;
  if (match.decidedBy === 'Games' && match.homeTeamScore != null && match.awayTeamScore != null)
    title = `${home} ${match.homeTeamScore}–${match.awayTeamScore} ${away}`;
  else if (match.decidedBy === 'Bye')
    title = `${byeTeam(match, home, away)} (bye)`;
  let season = match.seasonName;
  if (match.isPlayoff)
    season = `${season} playoffs`;
  return {
    title,
    description: `${season}, best of ${match.bestOf}: ${seasonMatchResult(match, home, away)}. Games, scores and player stats.`,
  };
}

/** The team a BYE advanced: the one that won it, or the only one there is. */
function byeTeam(match: SeasonMatchPageDto, home: string, away: string): string {
  if (match.homeTeamResult === 'Won')
    return home;
  if (match.awayTeamResult === 'Won')
    return away;
  return match.homeTeamName ? home : away;
}

/**
 * What decided the match, as the server says (decidedBy), never worked out from the two results: who won its games
 * (with the series score), who forfeited to whom, both forfeiting, a BYE; or, undecided, when it is scheduled.
 */
function seasonMatchResult(match: SeasonMatchPageDto, home: string, away: string): string {
  const homeWon = match.homeTeamResult === 'Won';
  const winner = homeWon ? home : away;
  const loser = homeWon ? away : home;
  switch (match.decidedBy) {
    case 'Games': {
      if (match.homeTeamScore == null || match.awayTeamScore == null)
        return `${winner} beat ${loser}`;
      const winnerScore = homeWon ? match.homeTeamScore : match.awayTeamScore;
      const loserScore = homeWon ? match.awayTeamScore : match.homeTeamScore;
      return `${winner} beat ${loser} ${winnerScore}–${loserScore}`;
    }
    case 'Forfeit':
      if (!homeWon && match.awayTeamResult !== 'Won')
        return `both ${home} and ${away} forfeited`;
      return `${loser} forfeited to ${winner}`;
    case 'Bye':
      return `${byeTeam(match, home, away)} advanced with a bye`;
    default: {
      const date = pageDate(match.scheduledTime);
      return date ? `${home} vs ${away}, scheduled for ${date}` : `${home} vs ${away}`;
    }
  }
}

@Component({
    selector: 'app-season-match',
    imports: [
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
        DatePipe,
        NgTemplateOutlet,
    ],
    templateUrl: './seasonMatch.component.html',
    styleUrl: './seasonMatch.component.scss',
    providers: [provideDatePickers(), SeasonMatchStore],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonMatchComponent {
  /** The match, its rosters, possible games and pull, and the calls that change it. */
  readonly store = inject(SeasonMatchStore);
  accountService = inject(AccountService);
  private readonly injector = inject(Injector);
  private readonly document = inject(DOCUMENT);

  /** Manual entry, below the box score: the page and it never both wait for the commissioner (see {@link start}). */
  private readonly manualGames = viewChild.required<Pick<ManualGamesComponent, 'cancel' | 'saving'>>('manualGames');
  /** The commissioner tools' heading: where focus goes when the control that had it is gone. */
  private readonly reportHeading = viewChild<ElementRef<HTMLElement>>('reportHeading');

  seasonMatchID: Signal<number> = input.required<number>()
  readonly seasonMatch = this.store.seasonMatch;

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

  /** Whether the user may pull the players' games from Halo: any player, as staff may (the server allows the same). */
  readonly canPull = computed(() => this.accountService.isPlayer() || this.accountService.isEventOrganizer() || this.accountService.isSysAdmin());

  /** A result or reported games to clear, as the server says (a match with a BYE is decided by the bracket). */
  readonly canUndo = computed(() => this.seasonMatch()?.canUndo ?? false);

  /** A result is in (or a BYE decided the match), as the server says. */
  readonly isDecided = computed(() => (this.seasonMatch()?.decidedBy ?? null) !== null);

  /**
   * Selected game tab on the scoreboard: 'series' for series totals, or the matchNumber of a specific game. Each time
   * the match (re)loads: its one game, or the series.
   */
  readonly selectedGameTab = linkedSignal<'series' | number>(() => {
    const games = this.seasonMatch()?.reportedGames;
    return games?.length === 1 ? games[0].matchNumber : 'series';
  });

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
    describePage(() => {
      const match = this.seasonMatch();
      return match ? describeSeasonMatch(match) : null;
    });
    // Another match: nothing of the previous one stays (its teams would title this page), and its request is cancelled
    // so a late answer can't land on this one
    effect(onCleanup => {
      const page = this.store.open(this.seasonMatchID());
      onCleanup(() => page.unsubscribe());
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
    this.send(this.store.report(matchID));
  }

  /** Reports the Waypoint game waiting to be placed, where the commissioner said it goes. */
  placeReport(slot: GameSlot): void {
    const mode = this.mode();
    if (mode.kind !== 'placeReport')
      return;
    const replaces = typeof slot === 'number' ? slot : undefined;
    this.send(this.store.report(mode.matchID, replaces));
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
    this.send(this.store.act(action));
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
    this.store.reload();
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
      }).add(() => this.store.reload().add(() => this._mode.set(IDLE)));
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
