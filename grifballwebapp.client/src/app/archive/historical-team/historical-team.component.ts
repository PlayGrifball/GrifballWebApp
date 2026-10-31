import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-historical-team',
  imports: [RouterLink],
  template: `
    <div class="hud-page">
      <div class="hud-page-head">
        <a class="hud-kicker" routerLink="/">&gt; Team Franchise Directory</a>
        <h1 class="hud-title">{{teamSlug()}}</h1>
      </div>

      <div class="hud-panel hud-panel--chamfer">
        <span class="hud-kicker">&gt; Franchise History</span>
        <h2>Team: {{teamSlug()}}</h2>
        <p class="hud-muted">Historical franchise roster and championship history across all leagues and eras.</p>
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
export class HistoricalTeamComponent implements OnInit {
  private route = inject(ActivatedRoute);

  readonly teamSlug = signal<string>('');

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('teamSlug') ?? '';
    this.teamSlug.set(slug);
  }
}
