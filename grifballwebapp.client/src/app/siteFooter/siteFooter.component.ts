import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { ApiClientService } from '../api/apiClient.service';
import { ToDatePipe } from '../shared/toDate.pipe';

/** The page footer: which build is running, linked to the source. */
@Component({
  selector: 'app-site-footer',
  imports: [DatePipe, ToDatePipe],
  template: `
    <footer class="site-footer">
      <a class="site-footer__build" href="https://github.com/PlayGrifball/GrifballWebApp">
        BUILD {{commitDate() | toDate | date:'short'}} {{commitHash()}}
      </a>
    </footer>
  `,
  styles: `
    .site-footer {
      box-sizing: border-box;
      max-width: var(--hud-max-width);
      margin: 0 auto;
      padding: 16px var(--hud-gutter) 24px;
      border-top: 1px solid var(--hud-hairline);
    }
    .site-footer__build {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
      font-family: var(--hud-font-mono);
      font-size: 13px;
      color: var(--hud-muted);
      overflow-wrap: anywhere;
    }
    .site-footer__build:hover { color: var(--hud-heading); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteFooterComponent {
  private api = inject(ApiClientService);

  // The build info is decoration: if it cannot be loaded the footer just leaves it out
  // (a toSignal over a failed observable would throw on every read).
  readonly commitHash = toSignal(this.api.commitHash().pipe(catchError(() => of(null))), { initialValue: null });
  readonly commitDate = toSignal(this.api.commitDate().pipe(catchError(() => of(null))), { initialValue: null });
}
