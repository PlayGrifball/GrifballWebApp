import { AfterViewInit, ChangeDetectionStrategy, Component, OnDestroy, OnInit, PLATFORM_ID, afterRenderEffect, computed, effect, inject, signal, untracked } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ToolbarComponent } from './toolbar/toolbar.component';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MediaMatcher } from '@angular/cdk/layout';
import { SideBarDto } from './sidebarDto';
import { AccountService } from './account.service';
import { ApiClientService } from './api/apiClient.service';
import { ThemingService } from './theming.service';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { SiteFooterComponent } from './siteFooter/siteFooter.component';

/** Room taken in the toolbar by the menu button (with its gap) when it is shown. */
export const MENU_BUTTON_WIDTH = 48;

@Component({
    selector: 'app-root',
    imports: [
        RouterLink,
        RouterLinkActive,
        MatSidenavModule,
        RouterOutlet,
        ToolbarComponent,
        MatButtonModule,
        MatIconModule,
        SiteFooterComponent,
    ],
    templateUrl: './app.component.html',
    styleUrl: './app.component.css',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent implements OnDestroy, OnInit, AfterViewInit {
  accountService = inject(AccountService);
  api = inject(ApiClientService);
  private themingService = inject(ThemingService);
  /** In a browser, not rendering on the server: the window, its size and the theme are only there. */
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly mobileQuery = inject(MediaMatcher).matchMedia('(max-width: 720px)');
  // Resize events come many times a frame: measure at most once per animation frame.
  private resizeFrame: number | null = null;
  private readonly onResize = () => {
    if (this.resizeFrame !== null)
      return;
    this.resizeFrame = requestAnimationFrame(() => {
      this.resizeFrame = null;
      this.measureToolbar();
    });
  };
  // Crossing the mobile breakpoint changes the layout even without a window resize (e.g. zoom).
  private readonly onMobileChange = () => this.measureToolbar();
  private destroyed = false;

  // Layout inputs, measured from the DOM after rendering (when the links change) and on every resize.
  private readonly isMobile = signal(false);
  private readonly toolbarWidth = signal(0);
  private readonly linkWidths = signal(new Map<string, number>());

  readonly navLinks = signal<SideBarDto[]>([
    { title: "Home", path: "", show: computed(() => true) },
    { title: "Archive", path: "/archive", show: computed(() => true) },
    { title: "Season Manager", path: "/seasonManager", show: computed(() => this.accountService.isEventOrganizer()) },
    { title: "Commissioner Dashboard", path: "/commissioner-dashboard", show: computed(() => this.accountService.isEventOrganizer()) },
    { title: "User Management", path: "/usermanagement", show: computed(() => this.accountService.isSysAdmin()) },
    { title: "Infinite Client", path: "/infiniteclient", show: computed(() => this.accountService.isSysAdmin()) },
    { title: "Excel Exporter", path: "/excel", show: computed(() => this.accountService.isEventOrganizer()) },
  ]);

  // Recomputed whenever the links change (e.g. "Current Season" arriving) or the toolbar is resized.
  private readonly toolbarLayout = computed(() => this.layoutToolbarLinks(this.navLinks(), this.isMobile(), this.toolbarWidth(), this.linkWidths()));
  readonly visibleNavLinks = computed(() => this.toolbarLayout().visible);
  readonly overflowNavLinks = computed(() => this.toolbarLayout().overflow);
  /** The sidenav (and its menu button) is only needed for links that do not fit in the toolbar. */
  readonly showMenu = computed(() => this.overflowNavLinks().length > 0);

  // The theme is set on the document in the browser; the server renders the stylesheet's colours.
  setTheme = this.isBrowser && effect(() => {
    this.themingService.setFont(this.themingService.font());
    this.themingService.setPrimaryShade(this.themingService.primary());
    this.themingService.setSecondaryShade(this.themingService.secondary());
    this.themingService.setTertiaryShade(this.themingService.tertiary());
    this.themingService.setNeutralShade(this.themingService.neutral());
    this.themingService.setNeutralVariantShade(this.themingService.neutralVariant());
    this.themingService.setErrorShade(this.themingService.error());
  });

  constructor() {
    // Measuring touches the DOM, so it happens after rendering, never inside the computed layout.
    afterRenderEffect(() => {
      this.navLinks();
      untracked(() => this.measureToolbar());
    });
  }

  ngOnInit(): void {
    this.api.getCurrentSeasonID().subscribe({
      next: (result) => {
        if (result == 0) return;
        const sideBarDto = {
          title: "Current Season",
          path: "/season/" + result,
          show: computed(() => true),
        };
        // Right after Home: it is the page most people come for.
        this.navLinks.update(links => [links[0], sideBarDto, ...links.slice(1)]);
      },
    });
  }

  ngAfterViewInit(): void {
    if (!this.isBrowser)
      return;
    window.addEventListener('resize', this.onResize);
    this.mobileQuery.addEventListener('change', this.onMobileChange);
    // The links are first measured in the fallback font; measure again once the web fonts are in.
    document.fonts?.ready.then(() => {
      if (!this.destroyed)
        this.measureToolbar();
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    if (!this.isBrowser)
      return;
    window.removeEventListener('resize', this.onResize);
    this.mobileQuery.removeEventListener('change', this.onMobileChange);
    if (this.resizeFrame !== null)
      cancelAnimationFrame(this.resizeFrame);
  }

  private measureToolbar(): void {
    this.isMobile.set(this.mobileQuery.matches);
    // The room the links would have without the menu button, so the layout does not depend on itself.
    const width = document.querySelector('.toolbar-links')?.clientWidth || 0;
    this.toolbarWidth.set(width + (untracked(this.showMenu) ? MENU_BUTTON_WIDTH : 0));
    this.linkWidths.set(new Map(this.navLinks().map(nav => [nav.title, this.getLinkWidth(nav.title)])));
  }

  private layoutToolbarLinks(navLinks: SideBarDto[], isMobile: boolean, toolbarWidth: number, linkWidths: Map<string, number>): { visible: SideBarDto[], overflow: SideBarDto[] } {
    if (isMobile) {
      return { visible: [], overflow: navLinks.filter(nav => nav.show()) };
    }

    // Links this user may not see take no room.
    const shown = navLinks.filter(nav => nav.show());
    const width = (nav: SideBarDto) => linkWidths.get(nav.title) ?? 0;
    // When some links overflow, the menu button that opens them takes room from the others.
    const total = shown.reduce((sum, nav) => sum + width(nav), 0);
    const room = total < toolbarWidth ? toolbarWidth : toolbarWidth - MENU_BUTTON_WIDTH;

    let usedWidth = 0;
    const visible: SideBarDto[] = [];
    const overflow: SideBarDto[] = [];

    for (const nav of shown) {
      const linkWidth = width(nav);
      if (usedWidth + linkWidth < room) {
        visible.push(nav);
        usedWidth += linkWidth;
      } else {
        overflow.push(nav);
      }
    }

    return { visible, overflow };
  }

  private getLinkWidth(title: string): number {
    const hiddenLink = document.getElementById('hidden');
    if (!hiddenLink) {
      console.error("Hidden link element not found!");
      return 0;
    }
    hiddenLink.innerText = title;
    return hiddenLink.offsetWidth;
  }
}
