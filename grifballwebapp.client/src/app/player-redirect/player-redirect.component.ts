import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-player-redirect',
  imports: [RouterLink],
  template: `
    <div class="hud-page">
      @if (loading()) {
        <div class="hud-panel hud-panel--chamfer">
          <span class="hud-kicker">&gt; Service Record Locator</span>
          <h1 class="hud-title">Locating Spartan Record...</h1>
          <p class="hud-muted">Querying database and Xbox Gamertag History for "{{gamertag()}}".</p>
        </div>
      } @else if (error()) {
        <div class="hud-panel hud-panel--chamfer">
          <span class="hud-kicker">&gt; Locator Error</span>
          <h1 class="hud-title">Spartan Record Not Found</h1>
          <p class="hud-muted">No service record or historical gamertag matching "<strong>{{gamertag()}}</strong>" was found in the archive.</p>
          <div class="hud-actions" style="margin-top: 1rem;">
            <a routerLink="/" class="hud-btn hud-btn--primary">Return Home</a>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
  `]
})
export class PlayerRedirectComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);

  readonly gamertag = signal<string>('');
  readonly loading = signal<boolean>(true);
  readonly error = signal<boolean>(false);

  ngOnInit(): void {
    const tag = this.route.snapshot.paramMap.get('gamertag') ?? '';
    this.gamertag.set(tag);

    if (!tag) {
      this.loading.set(false);
      this.error.set(true);
      return;
    }

    this.http.get<number>(`api/profile/resolvegamertag/${encodeURIComponent(tag)}`)
      .subscribe({
        next: (userId) => {
          this.router.navigate(['/profile', userId], { replaceUrl: true });
        },
        error: () => {
          this.loading.set(false);
          this.error.set(true);
        }
      });
  }
}
