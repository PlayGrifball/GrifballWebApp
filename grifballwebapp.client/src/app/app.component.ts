import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit, ViewChild, computed, effect, AfterViewInit, ElementRef, ViewChildren, QueryList } from '@angular/core';
import { ToolbarComponent } from './toolbar/toolbar.component';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MediaMatcher } from '@angular/cdk/layout';
import { SideBarDto } from './sidebarDto';
import { AccountService } from './account.service';
import { CommonModule } from '@angular/common';
import { ApiClientService } from './api/apiClient.service';
import { ThemingService } from './theming.service';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';

@Component({
    selector: 'app-root',
    imports: [
        CommonModule,
        RouterLink,
        MatListModule,
        MatSidenavModule,
        RouterOutlet,
        ToolbarComponent,
        MatButtonModule,
        MatMenuModule,
        MatIconModule,
    ],
    templateUrl: './app.component.html',
    styleUrl: './app.component.css',
    changeDetection: ChangeDetectionStrategy.Default
})
export class AppComponent implements OnDestroy, OnInit, AfterViewInit {
  @ViewChild('snav') snav!: MatSidenav;
  @ViewChildren('toolbarLink') toolbarLinks!: QueryList<ElementRef>;

  mobileQuery: MediaQueryList;
  private _mobileQueryListener: () => void;

  public navLinks: SideBarDto[] = [
    { title: "Home", path: "", show: computed(() => true) },
    { title: "Login", path: "/login", show: computed(() => true) },
    { title: "Season Manager", path: "/seasonManager", show: computed(() => this.accountService.isEventOrganizer()) },
  ];

  public visibleNavLinks: SideBarDto[] = [];
  public overflowNavLinks: SideBarDto[] = [];

  setTheme = effect(() => {
    this.themingService.setFont(this.themingService.font());
    this.themingService.setPrimaryShade(this.themingService.primary());
    this.themingService.setSecondaryShade(this.themingService.secondary());
    this.themingService.setTertiaryShade(this.themingService.tertiary());
    this.themingService.setNeutralShade(this.themingService.neutral());
    this.themingService.setNeutralVariantShade(this.themingService.neutralVariant());
    this.themingService.setErrorShade(this.themingService.error());
  });

  constructor(
    private changeDetectorRef: ChangeDetectorRef,
    media: MediaMatcher,
    public accountService: AccountService,
    public api: ApiClientService,
    private themingService: ThemingService
  ) {
    this.mobileQuery = media.matchMedia('(max-width: 600px)');
    this._mobileQueryListener = () => this.changeDetectorRef.detectChanges();
    this.mobileQuery.addEventListener("change", () => this._mobileQueryListener);
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
        this.navLinks.push(sideBarDto);
      },
    });
  }

  ngAfterViewInit(): void {
    this.adjustToolbarLinks();
    this.changeDetectorRef.detectChanges(); // Trigger change detection
    window.addEventListener('resize', () => this.adjustToolbarLinks());
  }

  ngOnDestroy(): void {
    this.mobileQuery.removeEventListener("change", this._mobileQueryListener);
    window.removeEventListener('resize', () => this.adjustToolbarLinks());
  }

  private adjustToolbarLinks(): void {
    if (this.mobileQuery.matches) {
      this.visibleNavLinks = [];
      this.overflowNavLinks = [...this.navLinks];
      this.changeDetectorRef.detectChanges();
      return;
    }

    const toolbarWidth = document.querySelector('.toolbar-links')?.clientWidth || 0;
    let usedWidth = 0;

    this.visibleNavLinks = [];
    this.overflowNavLinks = [];

    for (const nav of this.navLinks) {
      const linkWidth = this.getLinkWidth(nav.title);
      if (usedWidth + linkWidth < toolbarWidth) {
        this.visibleNavLinks.push(nav);
        usedWidth += linkWidth;
      } else {
        this.overflowNavLinks.push(nav);
      }
    }

    this.changeDetectorRef.detectChanges();
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
