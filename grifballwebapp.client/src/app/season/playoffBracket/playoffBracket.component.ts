import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { Config, MatchWithMetadata, ViewerData } from '../../bracketTypes/types';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { CreateBracketDialogComponent } from './createBracketDialog/createBracketDialog.component';
//import { BracketsViewer } from 'brackets-viewer';
//import { BracketsManager } from 'brackets-manager/dist'

@Component({
  selector: 'app-playoff-bracket',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatDialogModule,
  ],
  templateUrl: './playoffBracket.component.html',
  styleUrl: './playoffBracket.component.scss',
})
export class PlayoffBracketComponent implements OnInit {
  private seasonID: number = 0;
  
  constructor(private route: ActivatedRoute, private http: HttpClient, private router: Router, private dialog: MatDialog) { }

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));
    
    if (this.seasonID === 0)
      return;

    this.http.get<ViewerData>("api/Brackets/GetViewerData?seasonID=" + this.seasonID)
      .subscribe(
        {
          next: r => this.render(r),
          error: e => console.log(e)
        });
  }

  render(viewerData: ViewerData) {
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
    });

    let config: Partial<Config> = {};

    window.bracketsViewer.onMatchClicked = (match: MatchWithMetadata) => {
      this.router.navigate(['/seasonmatch/' + match.id]);
    };

    window.bracketsViewer.render(viewerData, config);
  }

  openDialog() {
    const dialogRef = this.dialog.open(CreateBracketDialogComponent, {
      data: this.seasonID
    });

    dialogRef.afterClosed().subscribe(result => {
      console.log(`Dialog result: ${result}`);
    });
  }
}
