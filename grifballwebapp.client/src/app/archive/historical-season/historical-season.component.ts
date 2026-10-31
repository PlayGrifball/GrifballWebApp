import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-historical-season',
  imports: [RouterLink],
  template: `
    <div class="hud-page">
      <div class="hud-page-head">
        <a class="hud-kicker" routerLink="/">&gt; Campaign Archive</a>
        <h1 class="hud-title">{{seasonName() || 'Historical Season ' + seasonID()}}</h1>
      </div>

      <div class="hud-panel hud-panel--chamfer">
        <span class="hud-kicker">&gt; Archive Record</span>
        <h2>Season #{{seasonID()}}</h2>
        <p class="hud-muted">Historical season archive records, team standings, and player leaderboards.</p>
        <div class="hud-actions" style="margin-top: 1rem;">
          <a routerLink="/" class="hud-btn">Back to Home</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
  `]
})
export class HistoricalSeasonComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);

  readonly seasonID = signal<number>(0);
  readonly seasonName = signal<string>('');

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('seasonID'));
    this.seasonID.set(id);

    this.http.get<string>(`api/season/getseasonname/${id}`)
      .subscribe({
        next: (name) => {
          if (name) this.seasonName.set(name);
        },
        error: () => {}
      });
  }
}
