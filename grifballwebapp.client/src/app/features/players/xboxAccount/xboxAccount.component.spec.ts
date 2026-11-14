import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal, WritableSignal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { AccountService } from '@core/auth/account.service';
import { PULL_POLL_MS, XboxAccountComponent } from './xboxAccount.component';
import type { OnDemandJobStatusDto, XboxAccountDto, XboxAccountGameDto } from '@api/models';

const XUID = '2535400000000042';

function game(matchID: string, changes: Partial<XboxAccountGameDto> = {}): XboxAccountGameDto {
  return { matchID, startTime: '2026-10-02T20:00:00Z', score: 5, otherScore: 3, outcome: 'Won', kills: 7, deaths: 3, assists: 2, ...changes };
}

function account(changes: Partial<XboxAccountDto> = {}): XboxAccountDto {
  return { xboxUserID: Number(XUID), gamertag: 'Pickup Pete', userID: null, gameCount: 1, page: 1, pageSize: 25, games: [game('m-1')], ...changes };
}

const idle: OnDemandJobStatusDto = { state: 'Idle', progress: null, progressMessage: null, startedAt: null, lastRun: null };

describe('XboxAccountComponent', () => {
  let fixture: ComponentFixture<XboxAccountComponent>;
  let http: HttpTestingController;
  let isPlayer: WritableSignal<boolean>;

  beforeEach(() => {
    isPlayer = signal(false);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]),
        { provide: AccountService, useValue: { isPlayer, isEventOrganizer: signal(false), isSysAdmin: signal(false) } }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(XboxAccountComponent);
    fixture.componentRef.setInput('xuid', XUID);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const text = () => el().textContent ?? '';
  const button = (label: string) => Array.from(el().querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent!.trim() === label);
  const request = (page = 1) => http.expectOne(r => r.url === `/api/xbox-accounts/${XUID}` && r.params.get('page') === String(page));
  const pullStatus = () => http.expectOne(r => r.method === 'GET' && r.url === `/api/xbox-accounts/${XUID}/pull`);

  it('shows the gamertag and its games, each linking to its game page, and says no account was created', () => {
    pullStatus().flush(idle);
    request().flush(account({ games: [game('m-1'), game('m-2', { outcome: 'Lost', score: 2, otherScore: 5, startTime: null })], gameCount: 2 }));
    fixture.detectChanges();

    expect(el().querySelector('h1')!.textContent).toBe('Pickup Pete');
    expect(text()).toContain('No account created');
    expect(el().querySelector('a[href^="/profile"]')).toBeNull();
    const rows = Array.from(el().querySelectorAll('tbody tr'));
    expect(rows).toHaveSize(2);
    expect(rows[0].querySelector('a')!.getAttribute('href')).toBe('/match/m-1');
    expect(rows[0].textContent).toContain('Won');
    expect(rows[0].textContent).toContain('5–3');
    expect(rows[1].textContent).toContain('Unknown time');
    expect(rows[1].textContent).toContain('2–5');
  });

  it('links to the profile of the user who holds the account', () => {
    pullStatus().flush(idle);
    request().flush(account({ userID: 21 }));
    fixture.detectChanges();

    expect(el().querySelector('a[href="/profile/21"]')!.textContent).toBe('Profile');
    expect(text()).not.toContain('No account created');
  });

  it('pages through the games, older then newer', () => {
    pullStatus().flush(idle);
    request().flush(account({ gameCount: 30 }));
    fixture.detectChanges();
    expect(button('Newer')!.disabled).toBeTrue();

    button('Older')!.click();
    fixture.detectChanges();
    request(2).flush(account({ gameCount: 30, page: 2, games: [game('m-26')] }));
    fixture.detectChanges();
    expect(button('Older')!.disabled).toBeTrue();
    expect(text()).toContain('Page 2');

    button('Newer')!.click();
    fixture.detectChanges();
    request(1).flush(account({ gameCount: 30 }));
  });

  it('says when there is no such account, and when it could not be loaded', () => {
    pullStatus().flush(idle);
    request().flush({ title: 'Not found' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect(text()).toContain('The site has no such Xbox account.');

    fixture.componentRef.setInput('xuid', '7');
    fixture.detectChanges();
    http.expectOne('/api/xbox-accounts/7/pull').flush(idle);
    http.expectOne(r => r.url === '/api/xbox-accounts/7').flush('down', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(el().querySelector('[role="alert"]')!.textContent).toContain('Could not load the account');
  });

  it('says when no games are saved', () => {
    pullStatus().flush(idle);
    request().flush(account({ games: [], gameCount: 0 }));
    fixture.detectChanges();

    expect(text()).toContain('The site has saved no games for this account.');
    expect(button('Older')).toBeUndefined();
  });
  it('offers the pull to a player, not to a visitor', () => {
    pullStatus().flush(idle);
    request().flush(account());
    fixture.detectChanges();
    expect(button('Pull recent games')).toBeUndefined();

    isPlayer.set(true);
    fixture.detectChanges();
    expect(button('Pull recent games')).toBeDefined();
  });

  it('pulls the account\'s games as a job, watches it, then reads the newest games again', () => {
    jasmine.clock().install();
    try {
      isPlayer.set(true);
      pullStatus().flush(idle);
      request().flush(account());
      fixture.detectChanges();

      button('Pull recent games')!.click();
      http.expectOne(r => r.method === 'POST' && r.url === `/api/xbox-accounts/${XUID}/pull`).flush({ ...idle, state: 'Queued' });
      fixture.detectChanges();
      expect(text()).toContain('waiting for the worker');
      expect(button('Pull recent games')!.disabled).toBeTrue();

      jasmine.clock().tick(PULL_POLL_MS);
      pullStatus().flush({ ...idle, state: 'Running' });
      fixture.detectChanges();
      expect(text()).toContain('Pulling the account\'s recent games from Halo Infinite…');

      jasmine.clock().tick(PULL_POLL_MS);
      pullStatus().flush({ ...idle, lastRun: { firedAt: '2026-11-14T03:00:00Z', durationMs: 900, result: 'Succeeded', error: null } });
      request().flush(account({ gameCount: 2, games: [game('m-new'), game('m-1')] }));
      fixture.detectChanges();
      expect(text()).toContain('Recent games last pulled');
      expect(el().querySelectorAll('tbody tr')).toHaveSize(2);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('says why a pull could not start', () => {
    isPlayer.set(true);
    pullStatus().flush(idle);
    request().flush(account());
    fixture.detectChanges();

    button('Pull recent games')!.click();
    http.expectOne(r => r.method === 'POST').flush({ title: 'Forbidden', detail: 'Not allowed' }, { status: 403, statusText: 'Forbidden', headers: { 'Content-Type': 'application/problem+json' } });
    fixture.detectChanges();

    expect(el().querySelector('.xa-pull [role="alert"]')!.textContent).toContain('Not allowed');
  });
});
