import { Component, signal, ChangeDetectionStrategy, inject, DestroyRef, PLATFORM_ID, input, output, computed, effect, untracked } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Config, MatchWithMetadata, ViewerData } from 'brackets-viewer';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { CreateBracketDialogComponent } from './createBracketDialog/createBracketDialog.component';
import { SeedOrderingDialogComponent } from './seedOrderingDialog/seedOrderingDialog.component';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AccountService } from '@core/auth/account.service';
import { Locale } from 'brackets-viewer/dist/lang';
import { loadBracketsViewer } from '@shared/loadBracketsViewer';
import type { SeasonChampionDto } from '@api/models';
import { PlayoffBracketStore } from './playoffBracket.store';

@Component({
    selector: 'app-playoff-bracket',
    imports: [
        MatButtonModule,
        MatDialogModule,
        MatSnackBarModule,
        RouterLink
    ],
    templateUrl: './playoffBracket.component.html',
    styleUrl: './playoffBracket.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [PlayoffBracketStore],
})
export class PlayoffBracketComponent {
  private readonly store = inject(PlayoffBracketStore);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private destroyRef = inject(DestroyRef);
  private snackBar = inject(MatSnackBar);
  accountService = inject(AccountService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** The season page's season (0: none yet). A new one loads its bracket in place of the one shown. */
  readonly seasonID = input.required<number>();

  /** The season's champions (its titles, see SeasonSummaryDto), for the champion slot at the end of the bracket: each tier's
   * winning team, or to be decided while there is none. Not given: no slot (the page shows the champions elsewhere). */
  readonly champions = input<SeasonChampionDto[] | undefined>(undefined);
  /** The season is over: with no champion recorded it never gets one, so the slot is left out rather than "to be decided". */
  readonly hasEnded = input(false);
  /** An organizer created the bracket again or reseeded it: the server cleared its champion, so the page fetches the season's again. */
  readonly bracketChanged = output<void>();

  /** The champions for the slot, or undefined for no slot (none given, or an ended season that has none). */
  readonly championSlot = computed(() => {
    const champions = this.champions();
    return champions && (champions.length > 0 || !this.hasEnded()) ? champions : undefined;
  });

  hasBracket = signal(false);
  /** The bracket (or that there is none) is known: until then the page shows neither. */
  readonly loaded = this.store.loaded;
  /** The viewer has drawn the bracket: the legend goes below it then, not before. */
  drawn = signal(false);

  constructor() {
    this.store.seasonID.use(this.seasonID);
    // Each bracket that arrives is drawn; none (another season loading, or none at all) clears what was shown
    effect(() => {
      const viewerData = this.store.viewerData();
      untracked(() => {
        if (viewerData) {
          void this.render(viewerData);
          return;
        }
        this.hasBracket.set(false);
        this.drawn.set(false);
      });
    });
  }

  getViewerData(): void {
    this.store.reload();
  }

  async render(viewerData: ViewerData): Promise<void> {
    if (viewerData.participants.length === 0) {
      this.hasBracket.set(false);
      return;
    } else {
      this.hasBracket.set(true);
    }
    // The viewer draws into the page in the browser; on the server the box is left for it.
    if (!this.isBrowser)
      return;
    try {
      await loadBracketsViewer();
    } catch {
      this.snackBar.open('Failed to load the bracket viewer', 'Close');
      return;
    }
    window.bracketsViewer.addLocale('en', {
      common: {
        'group-name-winner-bracket': '{{stage.name}}',
        'group-name-loser-bracket': '{{stage.name}} - Losers',
      },
      'origin-hint': {
        'winner-bracket': 'WB {{round}}.{{position}}',
        'winner-bracket-semi-final': 'WB Semi {{position}}',
        'winner-bracket-final': 'WB Final',
        'consolation-final': 'Semi {{position}}',
      },
    } as Locale);

    let config: Config = {
      clear: true,
      onMatchClick: (match: MatchWithMetadata) => {
        this.router.navigate(['/seasonmatch/' + match.id]);
      },
    };

    await window.bracketsViewer.render(viewerData, config);
    this.drawn.set(true);
  }

  openDialog() {
    const dialogRef = this.dialog.open(CreateBracketDialogComponent, {
      data: this.seasonID()
    });

    dialogRef.componentInstance.bracketCreated
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(() => {
      this.getViewerData();
      this.bracketChanged.emit();
    });
  }

  setSeeds() {
    const dialogRef = this.dialog.open(SeedOrderingDialogComponent, {
      data: this.seasonID(),
      width: '600px',
      maxHeight: '80vh'
    });

    dialogRef.componentInstance.seedingOrder
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(() => {
      this.getViewerData();
      this.bracketChanged.emit();
    });
  }
}
