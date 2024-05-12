import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { Config, MatchWithMetadata, ViewerData } from '../../bracketTypes/types';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
//import { BracketsViewer } from 'brackets-viewer';
//import { BracketsManager } from 'brackets-manager/dist'

@Component({
  selector: 'app-playoff-bracket',
  standalone: true,
  imports: [
    CommonModule,
  ],
  templateUrl: './playoffBracket.component.html',
  styleUrl: './playoffBracket.component.scss',
})
export class PlayoffBracketComponent implements OnInit {
  private seasonID: number = 0;

  constructor(private route: ActivatedRoute, private http: HttpClient) { }

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

    config.onMatchClick = this.matchClicked;

    window.bracketsViewer.onMatchClicked = this.matchClicked; // Not sure what this even does. Does not seem to work
    window.bracketsViewer.render(viewerData, config);
  }

  matchClicked(match: MatchWithMetadata): void {
    console.log(match.id + ' clicked');
  }
}
