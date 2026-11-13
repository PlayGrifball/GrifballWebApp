import { ComponentFixture, TestBed } from '@angular/core/testing';
import { input, output, signal, WritableSignal } from '@angular/core';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { describeProfile, ProfileComponent } from './profile.component';
import type { ProfileStatsDto } from '@api/models';
import { seasonInfo } from '@shared/testing/seasonInfo.stub';
import { AccountService } from '@core/auth/account.service';
import { Component } from '@angular/core';
import { LinkedAccountsComponent } from '@shared/linkedAccounts/linkedAccounts.component';
import { ProfileIdentityComponent } from './profileIdentity/profileIdentity.component';
import { VodListComponent } from '@shared/vodList/vodList.component';
import { StubVodListComponent } from '@shared/vodList/vodList.stub';
import { DeploymentHistoryComponent } from './deploymentHistory/deploymentHistory.component';
import { StubDeploymentHistoryComponent } from './deploymentHistory/deploymentHistory.stub';
import { ProfileCommendationsComponent } from './profileCommendations/profileCommendations.component';
import { StubProfileCommendationsComponent } from './profileCommendations/profileCommendations.stub';
import { By } from '@angular/platform-browser';

// The linked-accounts section loads its own data; its behaviour is covered by its own spec.
@Component({ selector: 'app-linked-accounts', template: '<p>linked accounts</p>' })
class LinkedAccountsStubComponent {}

// So does the region and past-gamertag-claims section (profileIdentity.component.spec.ts).
@Component({ selector: 'app-profile-identity', template: '<p>identity</p>' })
class ProfileIdentityStubComponent {
  readonly regionID = input<number | null>(null);
  readonly changed = output<void>();
}

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
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: snackBar })
      .overrideComponent(ProfileComponent, { remove: { imports: [DeploymentHistoryComponent] }, add: { imports: [StubDeploymentHistoryComponent] } })
      .overrideComponent(ProfileComponent, {
        remove: { imports: [LinkedAccountsComponent, ProfileIdentityComponent, VodListComponent, ProfileCommendationsComponent] },
        add: { imports: [LinkedAccountsStubComponent, ProfileIdentityStubComponent, StubVodListComponent, StubProfileCommendationsComponent] },
      })
      .compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfileComponent);
    fixture.componentRef.setInput('userID', 21);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    const pendingStats = http.match('/api/users/21/stats');
    for (const req of pendingStats) {
      if (!req.cancelled) {
        req.flush(null);
      }
    }
    http.verify();
  });

  function load(body: { gamertag: string | null } | null): void {
    fixture.detectChanges();
    http.expectOne('/api/users/21/gamertag').flush(body);
    const stats = http.match('/api/users/21/stats');
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

  it("lists the VODs of the player's teams", () => {
    personID.set(3);
    load({ gamertag: 'Sgt Grif' });
    const vods = fixture.debugElement.query(By.directive(StubVodListComponent)).componentInstance as StubVodListComponent;
    expect(vods.scope()).toBe('player');
    expect(vods.id()).toBe(21);
  });

  it("shows the player's commendations", () => {
    load({ gamertag: 'Sgt Grif' });
    const panel = fixture.debugElement.query(By.directive(StubProfileCommendationsComponent)).componentInstance as StubProfileCommendationsComponent;
    expect(panel.userID()).toBe(21);
  });

  it("hides the commendations, which are Halo Infinite medals, while another game's era is chosen", () => {
    load({ gamertag: 'Sgt Grif' });
    const panel = () => fixture.debugElement.query(By.directive(StubProfileCommendationsComponent));

    component.selectGame('halo3');
    fixture.detectChanges();
    expect(panel()).toBeNull();

    component.selectGame('infinite');
    fixture.detectChanges();
    expect(panel()).not.toBeNull();
  });

  it("shows another user's gamertag read-only", () => {
    personID.set(3);
    load({ gamertag: 'Sgt Grif' });

    expect(component.userID()).toBe(21);
    expect(component.settingGamertag()).toBeFalse();
    expect(name()).toBe('Sgt Grif');
    expect((fixture.nativeElement as HTMLElement).querySelector('form')).toBeNull();
  });

  describe('when the route moves to another user (Profile in the header)', () => {
    const stats = (userID: number, gamertag: string) => ({
      userID, gamertag, displayName: gamertag, isDummyUser: false, pastGamertags: [], championships: [], careerByGame: [], overallCareer: null, seasons: [],
    });

    it("loads the new user's profile in place of the one shown, keeping nothing of the previous one", () => {
      personID.set(21);
      load({ gamertag: null });
      component.selectGame('infinite');
      expect(component.settingGamertag()).toBeTrue();

      fixture.componentRef.setInput('userID', 42);
      fixture.detectChanges();
      expect(component.gamertag()).toBeNull();
      expect(component.profileStats()).toBeNull();
      expect(component.selectedGame()).toBe('all');
      expect(component.settingGamertag()).toBeFalse();

      http.expectOne('/api/users/42/gamertag').flush({ gamertag: 'Caboose' });
      http.expectOne('/api/users/42/stats').flush(stats(42, 'Caboose'));
      fixture.detectChanges();

      expect(name()).toBe('Caboose');
      expect(text()).toContain('42');
      // Someone else's profile: no gamertag prompt
      expect((fixture.nativeElement as HTMLElement).querySelector('form')).toBeNull();
    });

    it('drops a slow answer for the previous user', () => {
      fixture.detectChanges();
      const oldGamertag = http.expectOne('/api/users/21/gamertag');
      const oldStats = http.expectOne('/api/users/21/stats');

      fixture.componentRef.setInput('userID', 42);
      fixture.detectChanges();
      http.expectOne('/api/users/42/gamertag').flush({ gamertag: 'Caboose' });
      http.expectOne('/api/users/42/stats').flush(stats(42, 'Caboose'));
      oldGamertag.flush({ gamertag: 'Sgt Grif' });
      oldStats.flush(stats(21, 'Sgt Grif'));
      fixture.detectChanges();

      expect(name()).toBe('Caboose');
      expect(component.profileStats()!.userID).toBe(42);
    });

    it("drops a failure for the previous user's gamertag", () => {
      fixture.detectChanges();
      const oldGamertag = http.expectOne('/api/users/21/gamertag');
      http.expectOne('/api/users/21/stats').flush(stats(21, 'Sgt Grif'));

      fixture.componentRef.setInput('userID', 42);
      fixture.detectChanges();
      oldGamertag.flush(null, { status: 500, statusText: 'Server Error' });

      expect(snackBar.open).not.toHaveBeenCalled();
      http.expectOne('/api/users/42/gamertag').flush({ gamertag: 'Caboose' });
      http.expectOne('/api/users/42/stats').flush(stats(42, 'Caboose'));
    });
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
    http.expectOne('/api/users/21/gamertag').flush('x', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed get gamertag', 'Close');
  });

  it('saves the gamertag and leaves edit mode', () => {
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('NewTag');
    component.setGamertag();

    http.expectOne(r => r.method === 'POST' && r.url === '/api/users/21/gamertag' && r.body?.gamertag === 'NewTag').flush({});
    fixture.detectChanges();
    expect(component.settingGamertag()).toBeFalse();
    expect(name()).toBe('NewTag');
  });

  it('reloads the stats after saving the gamertag (placeholder accounts may have been merged)', () => {
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('NewTag');
    component.setGamertag();

    http.expectOne(r => r.method === 'POST' && r.url === '/api/users/21/gamertag' && r.body?.gamertag === 'NewTag').flush({});
    expect(http.match('/api/users/21/stats')).toHaveSize(1);
  });

  it('sends a gamertag with a space, &, and + as it is, in the body', () => {
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('Sgt Grif & Co+');
    component.setGamertag();

    const req = http.expectOne({ method: 'POST', url: '/api/users/21/gamertag' });
    expect(req.request.body).toEqual({ gamertag: 'Sgt Grif & Co+' });
    req.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    expect(component.settingGamertag()).toBeFalse();
  });

  it('sends an empty gamertag rather than the literal string "null"', () => {
    personID.set(21);
    load({ gamertag: null });
    component.setGamertag();

    const req = http.expectOne({ method: 'POST', url: '/api/users/21/gamertag' });
    expect(req.request.body).toEqual({ gamertag: '' });
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(component.settingGamertag()).toBeFalse();
  });

  it('stays in edit mode when saving fails', () => {
    spyOn(console, 'log');
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('NewTag');
    component.setGamertag();

    http.expectOne(r => r.method === 'POST' && r.url === '/api/users/21/gamertag' && r.body?.gamertag === 'NewTag').flush(null, { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed set gamertag', 'Close');
    expect(component.settingGamertag()).toBeTrue();
  });

  it("shows the server's error message when saving fails", () => {
    spyOn(console, 'log');
    personID.set(21);
    load({ gamertag: null });
    component.gamertag.set('NewTag');
    component.setGamertag();

    http.expectOne(r => r.method === 'POST' && r.url === '/api/users/21/gamertag' && r.body?.gamertag === 'NewTag')
      .flush('Gamertag is already set for this user. Contact sysadmin if it needs to be changed', { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith(
      'Gamertag is already set for this user. Contact sysadmin if it needs to be changed', 'Close');
    expect(component.settingGamertag()).toBeTrue();
  });

  it('shows the gamertag once it arrives, without a manual detectChanges', async () => {
    await fixture.whenStable();
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'Sgt Grif' });
    await fixture.whenStable();

    expect(name()).toBe('Sgt Grif');
  });

  it('leaves edit mode once the gamertag is saved, without a manual detectChanges', async () => {
    personID.set(21);
    await fixture.whenStable();
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: null });
    await fixture.whenStable();
    expect(text()).toContain('Please set your gamertag');

    component.gamertag.set('NewTag');
    component.setGamertag();
    http.expectOne(r => r.method === 'POST' && r.url === '/api/users/21/gamertag' && r.body?.gamertag === 'NewTag').flush({});
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
          season: seasonInfo(10, { seasonName: 'Summer League 2021', year: 2021, gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', division: 'Pro', isHistorical: true }),
          gameVersionID: 1,
          gameVersionName: 'Halo 3',
          gameVersionSlug: 'halo3',
          tier: 'Pro',
          teamID: 40,
          teamName: 'Blue Team',
          isCaptain: true,
          gamertagUsed: 'MasterChief',
        }
      ],
      overallCareer: {
        gameVersionName: 'All Games',
        gameVersionSlug: 'all',
        seasonsCount: 1,
        gamesPlayed: 14,
        gamesDecidedByVote: 2,
        wins: 12,
        losses: 2,
        winPercent: 85.7,
        goals: null,
        goalsPerGame: null,
        kills: 190,
        deaths: 95,
        assists: 40,
        kdRatio: 2.0,
        kdSpread: 95,
        carrierKills: null,
        stopsPerGame: null,
        sprees: null,
        multiKills: null,
        exterminations: null,
        betrayals: null, ballKills: null, damageDealt: null, damageTaken: null, gameVersionID: null, hammerKills: null, punches: null, score: null, swordKills: null
      },
      careerByGame: [
        {
          gameVersionID: 1,
          gameVersionName: 'Halo 3',
          gameVersionSlug: 'halo3',
          seasonsCount: 1,
          gamesPlayed: 14,
          gamesDecidedByVote: 0,
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
          betrayals: 0, ballKills: null, damageDealt: null, damageTaken: null, hammerKills: null, punches: null, score: null, swordKills: null
        }
      ],
      lateLeague: [],
      seasons: [
        {
          playerSeasonStatID: 1,
          userID: 21,
          gamertag: 'MasterChief',
          season: seasonInfo(10, { seasonName: 'Summer League 2021', year: 2021, gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', division: 'Pro', isHistorical: true }),
          gameVersionID: 1,
          gameVersionName: 'Halo 3',
          gameVersionSlug: 'halo3',
          teamID: 40,
          teamName: 'Blue Team',
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
          exterminations: 2, damageDealt: null, damageTaken: null, punches: null, roleNote: null, score: null, swordKills: null, tradeNote: null
        }
      ], regionID: null, regionName: null, serviceRecord: null
    };

    fixture.detectChanges();
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'MasterChief' });
    http.expectOne('/api/users/21/stats').flush(stats);
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
    // All games: only what every game counts alike; goals, stops and medals are per game
    const cards = () => Array.from(el.querySelectorAll('.stat-card .hud-label')).map(l => l.textContent!.trim());
    expect(cards()).not.toContain('Goals Scored');
    expect(cards()).not.toContain('Carrier Stops');
    expect(cards()).not.toContain('Medals');
    expect(el.querySelector('.career-note')).not.toBeNull();
    // Games Played says how many were decided by vote: the figures per game leave them out
    const gamesPlayed = () => Array.from(el.querySelectorAll('.stat-card')).find(c => c.querySelector('.hud-label')!.textContent!.trim() === 'Games Played')!
      .querySelector('.stat-val')!.textContent!.trim();
    expect(gamesPlayed()).toBe('14 (2 by vote)');

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
    expect(cards()).toContain('Goals Scored');
    expect(cards()).toContain('Carrier Stops');
    expect(cards()).toContain('Medals');
    expect(el.querySelector('.career-note')).toBeNull();
    expect(gamesPlayed()).toBe('14');
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
      ]
    })
      .overrideComponent(ProfileComponent, { remove: { imports: [DeploymentHistoryComponent] }, add: { imports: [StubDeploymentHistoryComponent] } })
      .overrideComponent(ProfileComponent, {
        remove: { imports: [LinkedAccountsComponent, ProfileIdentityComponent, VodListComponent, ProfileCommendationsComponent] },
        add: { imports: [LinkedAccountsStubComponent, ProfileIdentityStubComponent, StubVodListComponent, StubProfileCommendationsComponent] },
      })
      .compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfileComponent);
    fixture.componentRef.setInput('userID', 21);
  });

  afterEach(() => {
    const pendingStats = http.match('/api/users/21/stats');
    for (const req of pendingStats) {
      if (!req.cancelled) {
        req.flush(null);
      }
    }
    http.verify();
  });

  function render(): HTMLElement {
    fixture.detectChanges();
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'Sgt Grif' });
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('lists the player\'s Late League queue games and archive seasons, by the game chosen, and none when there are none', () => {
    const line = { userID: 21, gamertag: 'MasterChief', gamesPlayed: 10, wins: 7, losses: 3, winPercent: 70, goals: 9, kills: 40, deaths: 30, assists: 2, kdRatio: 1.33, kdSpread: 10, carrierKills: 4 };
    fixture.detectChanges();
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'MasterChief' });
    http.expectOne('/api/users/21/stats').flush({
      userID: 21, gamertag: 'MasterChief', displayName: 'MasterChief', isDummyUser: false,
      pastGamertags: [], championships: [], careerByGame: [], overallCareer: null, seasons: [],
      lateLeague: [
        { ...line, matchmakingSeasonID: 8, matchmakingSeasonName: 'Late League Season 8 (Halo 3)', gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', source: 'Recorded' },
        { ...line, matchmakingSeasonID: null, matchmakingSeasonName: null, gameVersionID: 6, gameVersionName: 'Halo Infinite', gameVersionSlug: 'infinite', source: 'Matches', kdRatio: null, kdSpread: null, winPercent: null },
      ],
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const rows = () => Array.from(el.querySelectorAll('[aria-labelledby="late-league-heading"] tbody tr'))
      .map(r => Array.from(r.querySelectorAll('td')).map(td => td.textContent!.trim()));
    expect(rows()).toEqual([
      ['Late League Season 8 (Halo 3)', 'Halo 3', '10', '7-3', '70%', '9', '40', '30', '+10', '1.33', '4', 'Recorded'],
      ['Current Late League', 'Halo Infinite', '10', '7-3', '-', '9', '40', '30', '+10', '-', '4', 'Matches'],
    ]);
    expect(el.querySelector('[aria-labelledby="late-league-heading"] tbody a')!.getAttribute('href')).toBe('/late-league/8');

    fixture.componentInstance.selectedGame.set('halo3');
    fixture.detectChanges();
    expect(rows().map(r => r[0])).toEqual(['Late League Season 8 (Halo 3)']);

    fixture.componentInstance.selectedGame.set('reach');
    fixture.detectChanges();
    expect(el.querySelector('#late-league-heading')).toBeNull();
  });

  it('shows a Late League line\'s games decided by vote, and that they have no box score', () => {
    const line = { userID: 21, gamertag: 'MasterChief', gamesPlayed: 10, gamesDecidedByVote: 0, wins: 7, losses: 3, winPercent: 70, goals: 9, kills: 40, deaths: 30, assists: 2, kdRatio: 1.33, kdSpread: 10, carrierKills: 4 };
    fixture.detectChanges();
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'MasterChief' });
    http.expectOne('/api/users/21/stats').flush({
      userID: 21, gamertag: 'MasterChief', displayName: 'MasterChief', isDummyUser: false,
      pastGamertags: [], championships: [], careerByGame: [], overallCareer: null, seasons: [],
      lateLeague: [
        { ...line, matchmakingSeasonID: 8, matchmakingSeasonName: 'Late League Season 8 (Halo 3)', gameVersionID: 1, gameVersionName: 'Halo 3', gameVersionSlug: 'halo3', source: 'Recorded' },
        { ...line, gamesDecidedByVote: 3, matchmakingSeasonID: null, matchmakingSeasonName: null, gameVersionID: 6, gameVersionName: 'Halo Infinite', gameVersionSlug: 'infinite', source: 'Matches' },
      ],
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const section = () => el.querySelector('[aria-labelledby="late-league-heading"]')!;
    expect(Array.from(section().querySelectorAll('tbody tr')).map(r => r.querySelectorAll('td')[2].textContent!.trim())).toEqual(['10', '10 (3 by vote)']);
    expect(section().querySelector('.vote-note')!.textContent).toContain('no box score');

    fixture.componentInstance.selectedGame.set('halo3');
    fixture.detectChanges();
    expect(section().querySelector('.vote-note')).toBeNull();
  });

  it('shows the service record strip at the top when the profile has one, and none when it has none', () => {
    const totals = { gamesPlayed: 3, wins: 2, losses: 1, winPercent: 66.7, kills: 6, deaths: 4, kdRatio: 1.5, bombScores: 2, uncountedBombScoreGames: 0 };
    fixture.detectChanges();
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'MasterChief' });
    http.expectOne('/api/users/21/stats').flush({
      userID: 21, gamertag: 'MasterChief', displayName: 'MasterChief', isDummyUser: false,
      pastGamertags: [], championships: [], careerByGame: [], overallCareer: null, seasons: [], lateLeague: [],
      serviceRecord: { mmr: 1100, all: totals, league: totals, matchmaking: totals },
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const main = el.querySelector('.profile-main')!;
    expect(main.firstElementChild!.querySelector('app-service-record .sr-strip')).not.toBeNull();
    expect(el.querySelector('app-service-record .sr-val')!.textContent!.trim()).toBe('1.50');

    fixture.componentInstance.profileStats.set({ ...fixture.componentInstance.profileStats()!, serviceRecord: null });
    fixture.detectChanges();
    expect(el.querySelector('app-service-record')).toBeNull();
  });

  it('shows the linked accounts on your own profile', () => {
    personID.set(21);
    expect(render().querySelector('app-linked-accounts')).not.toBeNull();
  });

  it("hides them on someone else's profile", () => {
    personID.set(4);
    expect(render().querySelector('app-linked-accounts')).toBeNull();
  });
});

describe('ProfileComponent: region and identity', () => {
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
      ]
    })
      .overrideProvider(MatSnackBar, { useValue: jasmine.createSpyObj('MatSnackBar', ['open']) })
      .overrideComponent(ProfileComponent, { remove: { imports: [DeploymentHistoryComponent] }, add: { imports: [StubDeploymentHistoryComponent] } })
      .overrideComponent(ProfileComponent, {
        remove: { imports: [LinkedAccountsComponent, ProfileIdentityComponent, VodListComponent, ProfileCommendationsComponent] },
        add: { imports: [LinkedAccountsStubComponent, ProfileIdentityStubComponent, StubVodListComponent, StubProfileCommendationsComponent] },
      })
      .compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfileComponent);
    fixture.componentRef.setInput('userID', 21);
  });

  afterEach(() => http.verify());

  function render(regionName: string | null): HTMLElement {
    fixture.detectChanges();
    http.expectOne('/api/users/21/gamertag').flush({ gamertag: 'Sgt Grif' });
    http.expectOne('/api/users/21/stats').flush({
      userID: 21, gamertag: 'Sgt Grif', displayName: null, isDummyUser: false, regionID: regionName ? 2 : null, regionName,
      pastGamertags: [], championships: [], careerByGame: [], overallCareer: null, seasons: [],
    });
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the region among the facts', () => {
    expect(render('Central North America').querySelector('.profile-region')!.textContent).toBe('Central North America');
  });

  it('shows no region when there is none', () => {
    expect(render(null).querySelector('.profile-region')).toBeNull();
  });

  it('shows the region and past gamertag claims on your own profile, and reloads when they change', () => {
    personID.set(21);
    const page = render('Central North America');
    const identity = fixture.debugElement.query(d => d.componentInstance instanceof ProfileIdentityStubComponent);
    expect(page.querySelector('app-profile-identity')).not.toBeNull();
    expect((identity.componentInstance as ProfileIdentityStubComponent).regionID()).toBe(2);
    (identity.componentInstance as ProfileIdentityStubComponent).changed.emit();
    http.expectOne('/api/users/21/stats').flush(null);
  });

  it("hides them on someone else's profile", () => {
    personID.set(4);
    expect(render(null).querySelector('app-profile-identity')).toBeNull();
  });
});

describe('describeProfile', () => {
  // Three stat lines (a season's regular and playoff lines, and another season's), two seasons
  const profile = (changes: Partial<ProfileStatsDto>) => ({ gamertag: 'Plasma Pete', displayName: 'Pete', seasons: [{}, {}, {}], overallCareer: { seasonsCount: 2 }, championships: [{}], ...changes } as ProfileStatsDto);

  it('gives the player\'s seasons and championships', () => {
    expect(describeProfile(profile({}))).toEqual({
      title: 'Plasma Pete',
      description: 'Plasma Pete\'s Grifball career: 2 seasons, 1 championship. Stats by season and game, and Late League.',
    });
  });

  it('falls back to the display name, and says nothing without either', () => {
    expect(describeProfile(profile({ gamertag: null, seasons: [], overallCareer: null, championships: [] }))).toEqual({
      title: 'Pete',
      description: 'Pete\'s Grifball career. Stats by season and game, and Late League.',
    });
    expect(describeProfile(profile({ gamertag: null, displayName: null }))).toBeNull();
  });
});
