import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';
import { BuildService } from '@api/services/build.service';

/** The page footer: which build is running, linked to the source. */
@Component({
  selector: 'app-site-footer',
  imports: [DatePipe],
  template: `
    <footer class="site-footer">
      <a class="site-footer__build" href="https://github.com/PlayGrifball/GrifballWebApp">
        BUILD {{commitDate() | date:'short'}} {{commitHash()}}
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
  private build = inject(BuildService);

  // The build info is decoration: if it cannot be loaded the footer just leaves it out
  // (a toSignal over a failed observable would throw on every read).
  readonly commitHash = toSignal(this.build.getCommitHash().pipe(catchError(() => of(null))), { initialValue: null });
  readonly commitDate = toSignal(this.build.getCommitDate().pipe(map(x => x ? new Date(x) : null), catchError(() => of(null))), { initialValue: null });
}
