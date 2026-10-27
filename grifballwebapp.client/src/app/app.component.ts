import { AfterViewInit, ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, effect, inject, signal } from '@angular/core';
import { ToolbarComponent } from './toolbar/toolbar.component';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MediaMatcher } from '@angular/cdk/layout';
import { SideBarDto } from './sidebarDto';
import { AccountService } from './account.service';
import { ApiClientService } from './api/apiClient.service';
import { ThemingService } from './theming.service';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';

@Component({
    selector: 'app-root',
    imports: [
        RouterLink,
        MatListModule,
        MatSidenavModule,
        RouterOutlet,
        ToolbarComponent,
        MatButtonModule,
        MatMenuModule,
        MatIconModule
    ],
    templateUrl: './app.component.html',
    styleUrl: './app.component.css',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent implements OnDestroy, OnInit, AfterViewInit {
  accountService = inject(AccountService);
  api = inject(ApiClientService);
  private themingService = inject(ThemingService);

  readonly mobileQuery = inject(MediaMatcher).matchMedia('(max-width: 600px)');
  private readonly onResize = () => this.measureToolbar();

  // Layout inputs, measured after the first render and on every resize.
  private readonly isMobile = signal(false);
  private readonly toolbarWidth = signal(0);

  readonly navLinks = signal<SideBarDto[]>([
    { title: "Home", path: "", show: computed(() => true) },
    { title: "Late League", path: "/lateLeague", show: computed(() => true) },
    { title: "Login", path: "/login", show: computed(() => true) },
    { title: "Season Manager", path: "/seasonManager", show: computed(() => this.accountService.isEventOrganizer()) },
    { title: "Commissioner Dashboard", path: "/commissioner-dashboard", show: computed(() => this.accountService.isEventOrganizer()) },
    { title: "User Management", path: "/usermanagement", show: computed(() => this.accountService.isSysAdmin()) },
    { title: "Infinite Client", path: "/infiniteclient", show: computed(() => this.accountService.isSysAdmin()) },
    { title: "Excel Exporter", path: "/excel", show: computed(() => this.accountService.isEventOrganizer()) },
  ]);

  // Recomputed whenever the links change (e.g. "Current Season" arriving) or the toolbar is resized.
  private readonly toolbarLayout = computed(() => this.layoutToolbarLinks(this.navLinks(), this.isMobile(), this.toolbarWidth()));
  readonly visibleNavLinks = computed(() => this.toolbarLayout().visible);
  readonly overflowNavLinks = computed(() => this.toolbarLayout().overflow);

  setTheme = effect(() => {
    this.themingService.setFont(this.themingService.font());
    this.themingService.setPrimaryShade(this.themingService.primary());
    this.themingService.setSecondaryShade(this.themingService.secondary());
    this.themingService.setTertiaryShade(this.themingService.tertiary());
    this.themingService.setNeutralShade(this.themingService.neutral());
    this.themingService.setNeutralVariantShade(this.themingService.neutralVariant());
    this.themingService.setErrorShade(this.themingService.error());
  });

  ngOnInit(): void {
    this.api.getCurrentSeasonID().subscribe({
      next: (result) => {
        if (result == 0) return;
        const sideBarDto = {
          title: "Current Season",
          path: "/season/" + result,
          show: computed(() => true),
        };
        this.navLinks.update(links => [...links, sideBarDto]);
      },
    });
  }

  ngAfterViewInit(): void {
    this.measureToolbar();
    window.addEventListener('resize', this.onResize);
  }

  ngOnDestroy(): void {
    window.removeEventListener('resize', this.onResize);
  }

  private measureToolbar(): void {
    this.isMobile.set(this.mobileQuery.matches);
    this.toolbarWidth.set(document.querySelector('.toolbar-links')?.clientWidth || 0);
  }

  private layoutToolbarLinks(navLinks: SideBarDto[], isMobile: boolean, toolbarWidth: number): { visible: SideBarDto[], overflow: SideBarDto[] } {
    if (isMobile) {
      return { visible: [], overflow: [...navLinks] };
    }

    let usedWidth = 0;
    const visible: SideBarDto[] = [];
    const overflow: SideBarDto[] = [];

    for (const nav of navLinks) {
      const linkWidth = this.getLinkWidth(nav.title);
      if (usedWidth + linkWidth < toolbarWidth) {
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
