import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit, ViewChild, computed, effect } from '@angular/core';
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

@Component({
    selector: 'app-root',
    imports: [
        CommonModule,
        RouterLink,
        MatListModule,
        MatSidenavModule,
        RouterOutlet,
        ToolbarComponent,
    ],
    templateUrl: './app.component.html',
    styleUrl: './app.component.css',
    changeDetection: ChangeDetectionStrategy.Default
})
export class AppComponent implements OnDestroy, OnInit {
  @ViewChild('snav') snav!: MatSidenav;

  mobileQuery: MediaQueryList;
  private _mobileQueryListener: () => void;

  public navLinks: SideBarDto[] = [
    {
      title: "Home",
      path: "",
      show: computed(() => true),
    },
    {
      title: "Login",
      path: "/login",
      show: computed(() => true),
    },
    {
      title: "Season Manager",
      path: "/seasonManager",
      //show: this.accountService.isEventOrganizer(),
      show: computed(() => this.accountService.isEventOrganizer()),
    }
  ];

  setTheme = effect(() => {
    this.themingService.setFont(this.themingService.font());
    this.themingService.setPrimaryShade(this.themingService.primary());
    this.themingService.setSecondaryShade(this.themingService.secondary());
    this.themingService.setTertiaryShade(this.themingService.tertiary());
    this.themingService.setNeutralShade(this.themingService.neutral());
    this.themingService.setNeutralVariantShade(this.themingService.neutralVariant());
    this.themingService.setErrorShade(this.themingService.error());
  });

  constructor(changeDetectorRef: ChangeDetectorRef,
    media: MediaMatcher,
    public accountService: AccountService,
    public api: ApiClientService,
    private themingService: ThemingService,
    ) {
    
    this.mobileQuery = media.matchMedia('(max-width: 600px)');
    this._mobileQueryListener = () => changeDetectorRef.detectChanges();
    this.mobileQuery.addEventListener("change", () => this._mobileQueryListener);
  }

  ngOnInit(): void {
    this.api.getCurrentSeasonID().subscribe({
      next: (result) => {
        if (result == 0)
          return;
        const sideBarDto = {
          title: "Current Season",
          path: "/season/" + result,
          show: computed(() => true),
        };
        this.navLinks.push(sideBarDto);
      },
    })

  }

  ngOnDestroy(): void {
    this.mobileQuery.removeEventListener("change", this._mobileQueryListener);
  }

}
