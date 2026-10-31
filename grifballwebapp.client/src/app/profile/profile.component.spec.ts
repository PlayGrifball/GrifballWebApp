import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ProfileComponent, ProfileStatsDto } from './profile.component';
import { AccountService } from '../account.service';
import { Component } from '@angular/core';
import { LinkedAccountsComponent } from '../shared/linkedAccounts/linkedAccounts.component';

// The linked-accounts section loads its own data; its behaviour is covered by its own spec.
@Component({ selector: 'app-linked-accounts', template: '<p>linked accounts</p>' })
class LinkedAccountsStubComponent {}

describe('ProfileComponent', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let component: ProfileComponent;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let personID: WritableSignal<number | null>;

  beforeEach(async () => {
    personID = signal<number | null>(null);
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    await TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AccountService, useValue: { personID } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '21' } } } },
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .overrideComponent(ProfileComponent, { remove: { imports: [LinkedAccountsComponent] }, add: { imports: [LinkedAccountsStubComponent] } })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfileComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    const pendingStats = http.match('api/profile/getprofilestats/21');
    for (const req of pendingStats) {
      if (!req.cancelled) {
        req.flush(null);
      }
    }
    http.verify();
  });

  function load(body: { gamertag: string | null } | null): void {
    fixture.detectChanges();
    http.expectOne('api/profile/getgamertag/21').flush(body);
    const stats = http.match('api/profile/getprofilestats/21');
    if (stats.length > 0) {
      stats[0].flush({
        userID: 21,
        gamertag: body?.gamertag ?? null,
        displayName: body?.gamertag ?? null,
        isDummyUser: false,
        pastGamertags: [],
        championships: [],
        careerByGame: [],
        overallCareer: null,
        seasons: []
      });
    }
    fixture.detectChanges();
  }

  const text = () => (fixture.nativeElement as HTMLElement).textContent!;
  const name = () => (fixture.nativeElement as HTMLElement).querySelector('h1')!.textContent!.trim();

  it('shows a placeholder service record for a user without a gamertag', () => {
    personID.set(3);
    load({ gamertag: null });
    expect(name()).toBe('Unknown Spartan');
    expect(text()).toContain('Not set');
    expect(text()).toContain('21');
    expect((fixture.nativeElement as HTMLElement).querySelector('.profile-emblem')!.textContent!.trim()).toBe('?');
  });

  it("shows another user's gamertag read-only", () => {
    personID.set(3);
    load({ gamertag: 'Sgt Grif' });

    expect(component.userID).toBe(21);
    expect(component.settingGamertag()).toBeFalse();
    expect(name()).toBe('Sgt Grif');
    expect((fixture.nativeElement as HTMLElement).querySelector('form')).toBeNull();
  });

  it('does not prompt the owner when the gamertag is already set', () => {
    personID.set(21);
    load({ gamertag: 'Sgt Grif' });
    expect(component.settingGamertag()).toBeFalse();
  });

  it('prompts the owner to set a missing gamertag', () => {
    personID.set(21);
    load({ gamertag: null });

    expect(component.settingGamertag()).toBeTrue();
    expect(text()).toContain('Please set your gamertag');
    expect((fixture.nativeElement as HTMLElement).querySelector('form')).not.toBeNull();
  });

  it('treats a null profile as having no gamertag', () => {
    personID.set(21);
    load(null);
    expect(component.gamertag()).toBeNull();
    expect(component.settingGamertag()).toBeTrue();
  });

  it('does not prompt other users when the gamertag is missing', () => {
    personID.set(4);
    load({ gamertag: null });
    expect(component.settingGamertag()).toBeFalse();
  });

  it('reports a failure to load the gamertag', () => {
    spyOn(console, 'log');
    fixture.detectChanges();
    http.expectOne('api/profile/getgamertag/21').flush('x', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed get gamertag', 'Close');
  });

  it('saves the gamertag and leaves edit mode', () => {
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('NewTag');
    component.setGamertag();

    http.expectOne({ method: 'POST', url: 'api/profile/setgamertag/21?gamertag=NewTag' }).flush({});
    fixture.detectChanges();
    expect(component.settingGamertag()).toBeFalse();
    expect(name()).toBe('NewTag');
  });

  it('reloads the stats after saving the gamertag (placeholder accounts may have been merged)', () => {
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('NewTag');
    component.setGamertag();

    http.expectOne({ method: 'POST', url: 'api/profile/setgamertag/21?gamertag=NewTag' }).flush({});
    expect(http.match('api/profile/getprofilestats/21').length).toBe(1);
  });

  it('URL-encodes a gamertag with a space, &, and +', () => {
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('Sgt Grif & Co+');
    component.setGamertag();

    http.expectOne({ method: 'POST', url: 'api/profile/setgamertag/21?gamertag=Sgt%20Grif%20%26%20Co%2B' }).flush({});
    fixture.detectChanges();
    expect(component.settingGamertag()).toBeFalse();
  });

  it('sends an empty gamertag rather than the literal string "null"', () => {
    personID.set(21);
    load({ gamertag: null });
    component.setGamertag();

    http.expectOne({ method: 'POST', url: 'api/profile/setgamertag/21?gamertag=' }).flush({});
    expect(component.settingGamertag()).toBeFalse();
  });

  it('stays in edit mode when saving fails', () => {
    spyOn(console, 'log');
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('NewTag');
    component.setGamertag();

    http.expectOne({ method: 'POST', url: 'api/profile/setgamertag/21?gamertag=NewTag' }).flush(null, { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed set gamertag', 'Close');
    expect(component.settingGamertag()).toBeTrue();
  });

  it("shows the server's error message when saving fails", () => {
    spyOn(console, 'log');
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('NewTag');
    component.setGamertag();

    http.expectOne({ method: 'POST', url: 'api/profile/setgamertag/21?gamertag=NewTag' })
      .flush('Gamertag is already set for this user. Contact sysadmin if it needs to be changed', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith(
      'Gamertag is already set for this user. Contact sysadmin if it needs to be changed', 'Close');
    expect(component.settingGamertag()).toBeTrue();
  });

  it('shows the gamertag once it arrives, without a manual detectChanges', async () => {
    await fixture.whenStable();
    http.expectOne('api/profile/getgamertag/21').flush({ gamertag: 'Sgt Grif' });
    await fixture.whenStable();

    expect(name()).toBe('Sgt Grif');
  });

  it('leaves edit mode once the gamertag is saved, without a manual detectChanges', async () => {
    personID.set(21);
    await fixture.whenStable();
    http.expectOne('api/profile/getgamertag/21').flush({ gamertag: null });
    await fixture.whenStable();
    expect(text()).toContain('Please set your gamertag');

    component.gamertag.set('NewTag');
    component.setGamertag();
    http.expectOne({ method: 'POST', url: 'api/profile/setgamertag/21?gamertag=NewTag' }).flush({});
    await fixture.whenStable();

    expect(name()).toBe('NewTag');
  });

  it('renders championship rings and multi-game career stats when available', () => {
    const stats: ProfileStatsDto = {
      userID: 21,
      gamertag: 'MasterChief',
      displayName: 'Master Chief',
      isDummyUser: false,
      pastGamertags: ['John-117'],
      championships: [
        {
          playerChampionshipID: 1,
          seasonID: 10,
          seasonName: 'Summer League 2021',
          leagueID: 1,
          leagueName: 'AGLA',
          leagueSlug: 'agla',
          gameVersionID: 1,
          gameVersionName: 'Halo 3',
          gameVersionSlug: 'halo3',
          tier: 'Pro',
          teamID: 40,
          teamName: 'Blue Team',
          isHistorical: true,
          isCaptain: true,
          gamertagUsed: 'MasterChief',
          year: 2021
        }
      ],
      overallCareer: {
        gameVersionName: 'All Games',
        gameVersionSlug: 'all',
        seasonsCount: 1,
        gamesPlayed: 14,
        wins: 12,
        losses: 2,
        winPercent: 85.7,
        goals: 28,
        goalsPerGame: 2.0,
        kills: 190,
        deaths: 95,
        assists: 40,
        kdRatio: 2.0,
        kdSpread: 95,
        carrierKills: 45,
        stopsPerGame: 3.21,
        sprees: 12,
        multiKills: 8,
        exterminations: 2,
        betrayals: 0
      },
      careerByGame: [
        {
          gameVersionID: 1,
          gameVersionName: 'Halo 3',
          gameVersionSlug: 'halo3',
          seasonsCount: 1,
          gamesPlayed: 14,
          wins: 12,
          losses: 2,
          winPercent: 85.7,
          goals: 28,
          goalsPerGame: 2.0,
          kills: 190,
          deaths: 95,
          assists: 40,
          kdRatio: 2.0,
          kdSpread: 95,
          carrierKills: 45,
          stopsPerGame: 3.21,
          sprees: 12,
          multiKills: 8,
          exterminations: 2,
          betrayals: 0
        }
      ],
      seasons: [
        {
          playerSeasonStatID: 1,
          seasonID: 10,
          seasonName: 'Summer League 2021',
          leagueID: 1,
          leagueName: 'AGLA',
          leagueSlug: 'agla',
          gameVersionID: 1,
          gameVersionName: 'Halo 3',
          gameVersionSlug: 'halo3',
          division: 'Pro',
          teamID: 40,
          teamName: 'Blue Team',
          isHistorical: true,
          stintIndex: 1,
          isPlayoffs: false,
          isCombined: false,
          source: 'Manual',
          gamesPlayed: 14,
          wins: 12,
          losses: 2,
          winPercent: 85.7,
          goals: 28,
          kills: 190,
          deaths: 95,
          assists: 40,
          kdRatio: 2.0,
          kdSpread: 95,
          carrierKills: 45,
          sprees: 12,
          multiKills: 8,
          exterminations: 2
        }
      ]
    };

    fixture.detectChanges();
    http.expectOne('api/profile/getgamertag/21').flush({ gamertag: 'MasterChief' });
    http.expectOne('api/profile/getprofilestats/21').flush(stats);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.trophy-card')).not.toBeNull();
    expect(el.textContent).toContain('Summer League 2021');
    expect(el.textContent).toContain('Pro Tier');
    expect(el.textContent).toContain('Blue Team');
    expect(el.textContent).toContain('Captain');
    expect(el.textContent).toContain('John-117');
    expect(el.textContent).toContain('12 - 2');
    expect(el.textContent).toContain('85.7% Win rate');

    // A historical season and its team are linked like any other: their own season and team pages
    const seasonLink = el.querySelector('.trophy-season a');
    expect(seasonLink).not.toBeNull();
    expect(seasonLink?.getAttribute('href')).toBe('/season/10');

    const teamLink = el.querySelector('.trophy-meta a.trophy-team');
    expect(teamLink).not.toBeNull();
    expect(teamLink?.getAttribute('href')).toBe('/season/10/team/40');

    const tabs = Array.from(el.querySelectorAll<HTMLButtonElement>('.hud-tab'));
    expect(tabs.map(t => t.getAttribute('aria-pressed'))).toEqual(['true', 'false']);
    tabs[1].click();
    fixture.detectChanges();
    expect(tabs.map(t => t.getAttribute('aria-pressed'))).toEqual(['false', 'true']);
  });
});

describe('ProfileComponent linked accounts', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let http: HttpTestingController;
  let personID: WritableSignal<number | null>;

  beforeEach(async () => {
    personID = signal<number | null>(null);
    await TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AccountService, useValue: { personID } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '21' } } } },
      ]
    })
      .overrideComponent(ProfileComponent, { remove: { imports: [LinkedAccountsComponent] }, add: { imports: [LinkedAccountsStubComponent] } })
      .compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfileComponent);
  });

  afterEach(() => {
    const pendingStats = http.match('api/profile/getprofilestats/21');
    for (const req of pendingStats) {
      if (!req.cancelled) {
        req.flush(null);
      }
    }
    http.verify();
  });

  function render(): HTMLElement {
    fixture.detectChanges();
    http.expectOne('api/profile/getgamertag/21').flush({ gamertag: 'Sgt Grif' });
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the linked accounts on your own profile', () => {
    personID.set(21);
    expect(render().querySelector('app-linked-accounts')).not.toBeNull();
  });

  it("hides them on someone else's profile", () => {
    personID.set(4);
    expect(render().querySelector('app-linked-accounts')).toBeNull();
  });
});
