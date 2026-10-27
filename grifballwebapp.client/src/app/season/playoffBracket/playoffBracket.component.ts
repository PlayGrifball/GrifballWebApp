import { Component, OnInit, signal, ChangeDetectionStrategy, inject } from '@angular/core';
import { Config, MatchWithMetadata, ViewerData } from 'brackets-viewer';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { CreateBracketDialogComponent } from './createBracketDialog/createBracketDialog.component';
import { SeedOrderingDialogComponent } from './seedOrderingDialog/seedOrderingDialog.component';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AccountService } from '../../account.service';
import { Locale } from 'brackets-viewer/dist/lang';

@Component({
    selector: 'app-playoff-bracket',
    imports: [
        MatButtonModule,
        MatDialogModule,
        MatSnackBarModule
    ],
    templateUrl: './playoffBracket.component.html',
    styleUrl: './playoffBracket.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class PlayoffBracketComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  accountService = inject(AccountService);

  private seasonID: number = 0;

  hasBracket = signal(false);

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));
    
    if (this.seasonID === 0)
      return;

    this.getViewerData();
  }

  getViewerData(): void {
    this.http.get<ViewerData>("api/Brackets/GetViewerData?seasonID=" + this.seasonID)
      .subscribe(
        {
          next: r => this.render(r),
          error: e => {
            console.log(e);
            this.snackBar.open("Failed to get viewer data", "Close");
          }
        });
  }

  render(viewerData: ViewerData) {
    if (viewerData.participants.length === 0) {
      this.hasBracket.set(false);
      return;
    } else {
      this.hasBracket.set(true);
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
      clear: true
    };

    window.bracketsViewer.onMatchClicked = (match: MatchWithMetadata) => {
      this.router.navigate(['/seasonmatch/' + match.id]);
    };

    window.bracketsViewer.render(viewerData, config);
  }

  openDialog() {
    const dialogRef = this.dialog.open(CreateBracketDialogComponent, {
      data: this.seasonID
    });

    dialogRef.componentInstance.bracketCreated.subscribe(() => {
      this.getViewerData();
    });
  }

  setSeeds() {
    const dialogRef = this.dialog.open(SeedOrderingDialogComponent, {
      data: this.seasonID,
      width: '600px',
      maxHeight: '80vh'
    });

    dialogRef.componentInstance.seedingOrder.subscribe(() => {
      this.getViewerData();
    });
  }
}
