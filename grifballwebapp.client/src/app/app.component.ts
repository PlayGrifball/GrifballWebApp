import { ChangeDetectionStrategy, ChangeDetectorRef, Component, HostBinding, OnDestroy, OnInit, ViewChild, computed } from '@angular/core';
import { ToolbarComponent } from './toolbar/toolbar.component';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MediaMatcher } from '@angular/cdk/layout';
import { SideBarDto } from './sidebarDto';
import { AccountService } from './account.service';
import { CommonModule } from '@angular/common';
import { ApiClientService } from './api/apiClient.service';
import { themes, ThemeService } from './theme.service';
import { toObservable } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-root',
  standalone: true,
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
      //show: false,
      show: computed(() => true),
    },
    {
      title: "Login",
      path: "/login",
      //show: true,
      show: computed(() => true),
    },
    // {
    //   title: "Current Season",
    //   path: "/season/0",
    //   //show: true,
    //   show: computed(() => true),
    // },
    {
      title: "Season Manager",
      path: "/seasonManager",
      //show: this.accountService.isEventOrganizer(),
      show: computed(() => this.accountService.isEventOrganizer()),
    }
  ];

  constructor(changeDetectorRef: ChangeDetectorRef,
    media: MediaMatcher,
    public accountService: AccountService,
    public api: ApiClientService,
    private themeService: ThemeService) {
    
    this.mobileQuery = media.matchMedia('(max-width: 600px)');
    this._mobileQueryListener = () => changeDetectorRef.detectChanges();
    this.mobileQuery.addEventListener("change", () => this._mobileQueryListener);
  }

  ngOnInit(): void {
    this.api.getCurrentSeasonID().subscribe({
      next: (result) => {
        const sideBarDto = {
          title: "Current Season",
          path: "/season/" + result,
          show: computed(() => true),
        };
        this.navLinks.push(sideBarDto);
      },
    })
    //this.accountService.isEventOrganizer.bind().
    //this.navLinks = [
    //  {
    //    title: "Home",
    //    path: "",
    //    show: true,
    //  },
    //  {
    //    title: "Login",
    //    path: "/login",
    //    show: true,
    //  },
    //  {
    //    title: "Seasons",
    //    path: "/seasons",
    //    show: this.accountService.isLoggedIn(),
    //  }
    //];
  }

  ngOnDestroy(): void {
    this.mobileQuery.removeEventListener("change", this._mobileQueryListener);
  }

}
