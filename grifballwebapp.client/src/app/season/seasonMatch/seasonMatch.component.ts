import { CommonModule, isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, effect, input, signal, Signal, WritableSignal, ChangeDetectionStrategy, PLATFORM_ID, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { parseSeasonMatchPage, SeasonMatchPageDto } from './seasonMatchPageDto';
import { ErrorMessageComponent } from '../../validation/errorMessage.component';
import { MtxDatetimepickerModule } from '@ng-matero/extensions/datetimepicker';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { FormsModule } from '@angular/forms';
import { MatInputModule } from '@angular/material/input';
import { RegexMatchValidValidatorDirective } from '../../validation/directives/regexMatchValidValidatorDirective.directive';
import { PossibleMatchDto, PossiblePlayerDto } from './possibleMatchDto';
import { MatTableModule } from '@angular/material/table';
import { AccountService } from '../../account.service';
import { map, Subscription, switchMap, takeWhile, timer } from 'rxjs';
import type { OnDemandJobStatusDto } from '../../api/dtos/onDemandJobDto';

import { ToDatePipe } from '../../shared/toDate.pipe';
import { errorText } from '../../shared/errorText';
import { PlayerDto, TeamDto } from '../team/team.component';
import { provideDatePickers } from '../../shared/datePickers';
import { VodListComponent } from '../../shared/vodList/vodList.component';
import { RecordedGamesComponent } from './recordedGames/recordedGames.component';

/** The commissioner actions on a match that are confirmed first. Each is a POST to the endpoint of the same name. */
export type MatchAction = 'HomeForfeit' | 'AwayForfeit' | 'DoubleForfeit' | 'UndoResult';

/** What confirming each action does. */
export const actionPrompts: Record<MatchAction, string> = {
  HomeForfeit: 'The home team forfeits and the away team wins.',
  AwayForfeit: 'The away team forfeits and the home team wins.',
  DoubleForfeit: 'Both teams forfeit and take a loss. In the playoffs nobody advances: the matches waiting on this one get a BYE instead.',
  UndoResult: 'This match goes back to undecided: its result, score and reported games are cleared, and any team or BYE it sent on to later matches is taken back out. Not possible once one of those later matches has a result of its own.',
};

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
        RecordedGamesComponent,
    ],
    templateUrl: './seasonMatch.component.html',
    styleUrl: './seasonMatch.component.scss',
    providers: [provideDatePickers()],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SeasonMatchComponent {
  private http = inject(HttpClient);
  accountService = inject(AccountService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly destroyRef = inject(DestroyRef);

  seasonMatchID: Signal<number> = input.required<number>()
  seasonMatch: WritableSignal<SeasonMatchPageDto | null> = signal(null);

  isSubmittingMatch: WritableSignal<boolean> = signal(false);

  /** The action waiting for the commissioner to confirm it. */
  pendingAction: WritableSignal<MatchAction | null> = signal(null);
  readonly actionPrompts = actionPrompts;
  /** Why the server turned the last action down. */
  actionError: WritableSignal<string | null> = signal(null);

  /** A result or reported games to clear. A match with a BYE is decided by the bracket. */
  canUndo: Signal<boolean> = computed(() => {
    const match = this.seasonMatch();
    if (match === null || match.homeTeamResult === 'Bye' || match.awayTeamResult === 'Bye')
      return false;
    return match.homeTeamResult !== 'TBD' || match.awayTeamResult !== 'TBD' || match.reportedGames.length > 0 || (match.recordedGames?.length ?? 0) > 0;
  });

  /** A result is in (or a BYE decided the match). */
  readonly isDecided = computed(() => {
    const match = this.seasonMatch();
    return match !== null && (match.homeTeamResult !== 'TBD' || match.awayTeamResult !== 'TBD');
  });

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
    this.http.get<TeamDto | null>('/api/Team/Team/' + teamID)
      .subscribe({
        next: team => roster.set({ teamID, players: team?.players ?? [] }),
        error: () => roster.set({ teamID, players: [] }),
      });
  }

  getPageDto(): void {
    this.http.get<unknown>('/api/SeasonMatch/GetSeasonMatchPage/' + this.seasonMatchID())
      .pipe(map(parseSeasonMatchPage))
      .subscribe({
        next: result => {
          this.seasonMatch.set(result);
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
    this.http.get<PossibleMatchDto[]>('/api/SeasonMatch/GetPossibleMatches/' + this.seasonMatchID())
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
    this.http.get<OnDemandJobStatusDto>('/api/SeasonMatch/MatchPullStatus/' + this.seasonMatchID())
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
    const query = backToSeasonStart ? '?backToSeasonStart=true' : '';
    this.http.post<OnDemandJobStatusDto>('/api/SeasonMatch/PullMatches/' + this.seasonMatchID() + query, null)
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
      switchMap(() => this.http.get<OnDemandJobStatusDto>('/api/SeasonMatch/MatchPullStatus/' + this.seasonMatchID())),
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

  onSubmit(matchID: string): void {
    this.isSubmittingMatch.set(true);

    this.http.post<string>('/api/SeasonMatch/ReportMatch/' + this.seasonMatchID() + '/' + matchID, null)
        .subscribe({
          next: result => console.log(result),
          error: result => console.log(result),
        }).add(() => this.finishedReportingMatch());
  }

  /** Asks the commissioner to confirm an action before it is sent. */
  requestAction(action: MatchAction): void {
    this.pendingAction.set(action);
  }

  cancelAction(): void {
    this.pendingAction.set(null);
  }

  confirmAction(): void {
    const action = this.pendingAction();
    if (action === null)
      return;

    this.pendingAction.set(null);
    this.actionError.set(null);
    this.isSubmittingMatch.set(true);

    this.http.post<string>('/api/SeasonMatch/' + action + '/' + this.seasonMatchID(), null)
      .subscribe({
        next: result => console.log(result),
        error: (e: HttpErrorResponse) => this.actionError.set(errorText(e, 'The server could not do that.')),
      }).add(() => this.finishedReportingMatch());
  }

  finishedReportingMatch(): void {
    this.isSubmittingMatch.set(false);
    this.getPageDto();
  }
}

/** A side's players and the team they were loaded for; players is null while loading. */
interface Roster {
  teamID: number | null,
  players: PlayerDto[] | null,
}

/** The roster of this team has arrived. */
function isLoaded(roster: Roster, teamID: number | null): boolean {
  return roster.teamID === teamID && roster.players !== null;
}
