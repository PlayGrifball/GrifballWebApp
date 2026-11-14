import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { SiteFooterStore } from './siteFooter.store';

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
  providers: [SiteFooterStore],
})
export class SiteFooterComponent {
  private readonly store = inject(SiteFooterStore);

  readonly commitHash = this.store.commitHash;
  readonly commitDate = this.store.commitDate;
}
