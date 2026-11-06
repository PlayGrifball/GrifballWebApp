import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, RouterLink } from '@angular/router';
import { By } from '@angular/platform-browser';
import { TeamHistoryComponent, TeamHistoryDto } from './teamHistory.component';
import { seasonInfo } from '../api/dtos/seasonInfoDto.stub';

describe('TeamHistoryComponent', () => {
  let fixture: ComponentFixture<TeamHistoryComponent>;
  let component: TeamHistoryComponent;
  let http: HttpTestingController;

  const history: TeamHistoryDto = {
    teamName: 'Trophy Hunters',
    leagueName: 'AGLA',
    appearances: 2,
    wins: 12,
    losses: 3,
    championships: 1,
    players: 3,
    seasons: [
      {
        teamID: 30, teamName: 'Trophy Hunters', season: seasonInfo(3, { seasonName: 'Season 3', year: 2024, division: 'Pro' }),
        wins: 5, losses: 2, titles: [],
        players: [
          { teamPlayerID: 1, userID: 10, gamertag: 'Captain Grif', draftCaptainOrder: 1, draftRound: null, isCaptain: true },
          { teamPlayerID: 2, userID: 11, gamertag: 'Simmons', draftCaptainOrder: null, draftRound: 1 },
        ],
      },
      {
        teamID: 20, teamName: 'trophy hunters', season: seasonInfo(2, { seasonName: 'Archive 2015', year: 2015, isHistorical: true, gameVersionName: null }),
        wins: 7, losses: 1, recordFromSource: true, titles: ['Pro'],
        players: [
          { teamPlayerID: 3, userID: 12, gamertag: null, draftCaptainOrder: null, draftRound: null, isCaptain: true },
        ],
      },
    ],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TeamHistoryComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamHistoryComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('teamID', 20);
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const request = (teamID = 20) => http.expectOne(`/api/Team/History/${teamID}`);

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  async function load(h: TeamHistoryDto | null): Promise<void> {
    fixture.detectChanges();
    request().flush(h);
    await settle();
  }

  const seasons = () => Array.from(el().querySelectorAll('.history-season'));
  const links = (root: Element) => fixture.debugElement.queryAll(By.directive(RouterLink))
    .filter(d => root.contains(d.nativeElement))
    .map(d => d.injector.get(RouterLink).urlTree!.toString());

  it('asks for the franchise by one of its teams and waits until it has it', async () => {
    fixture.detectChanges();
    expect(el().querySelector('.hud-page')!.classList).toContain('hud-waiting');
    expect(el().querySelector('h1')!.textContent).toBe('Team history');
    expect(el().querySelector('.hud-kicker')!.textContent).toBe('> Franchise history');

    request().flush(history);
    await settle();
    expect(component.loaded()).toBeTrue();
    expect(el().querySelector('.hud-page')!.classList).not.toContain('hud-waiting');
  });

  it("shows the franchise's latest name and all-time totals", async () => {
    await load(history);
    const totals = Array.from(el().querySelectorAll('.history-total')).map(t => Array.from(t.children).map(c => c.textContent!.trim()).join(' '));

    expect(el().querySelector('h1')!.textContent).toBe('Trophy Hunters');
    expect(el().querySelector('.hud-kicker')!.textContent).toBe('> Franchise history · AGLA');
    expect(totals).toEqual(['Seasons 2', 'All-time record 12 - 3', 'Championships 1', 'Players 3']);
    expect(el().querySelectorAll('.history-total .hud-gold').length).toBe(1);
  });

  it("doesn't gild a franchise without a title", async () => {
    await load({ ...history, championships: 0 });
    expect(el().querySelector('.history-total .hud-gold')).toBeNull();
  });

  it('lists every season newest first, with its record, titles, captain and roster', async () => {
    await load(history);
    const [latest, oldest] = seasons();

    expect(seasons().length).toBe(2);
    expect(latest.querySelector('.history-season__name')!.textContent).toBe('Season 3');
    expect(latest.textContent).toContain('2024');
    expect(latest.textContent).toContain('AGLA · Halo Infinite · Pro');
    expect(latest.querySelector('.history-season__record')!.textContent).toBe('5 - 2');
    expect(latest.textContent).not.toContain('champions');
    expect(latest.classList).not.toContain('history-season--champion');
    expect(latest.querySelector('.history-season__captain')!.textContent).toContain('Captain Grif');
    expect(Array.from(latest.querySelectorAll('.history-season__roster li')).map(li => li.textContent!.trim())).toEqual(['Captain Grif', 'Simmons']);
    expect(latest.textContent).not.toContain('From the source spreadsheet');

    expect(oldest.querySelector('.history-season__team')!.textContent).toBe('trophy hunters');
    expect(oldest.textContent).toContain('Pro champions');
    expect(oldest.classList).toContain('history-season--champion');
    expect(oldest.textContent).toContain('From the source spreadsheet');
    expect(oldest.textContent).not.toContain('Halo Infinite');
    expect(oldest.querySelector('.history-season__captain')!.textContent).toContain('Unknown Spartan');
  });

  it("links each season, that season's team and its players", async () => {
    await load(history);
    const [latest] = seasons();

    expect(links(latest)).toEqual(['/season/3', '/season/3/team/30', '/profile/10', '/profile/10', '/profile/11']);
  });

  it('says when a season recorded no roster', async () => {
    await load({ ...history, seasons: [{ ...history.seasons[0], players: [] }] });
    expect(seasons()[0].querySelector('.history-season__captain')).toBeNull();
    expect(seasons()[0].textContent).toContain('No roster recorded.');
  });

  it('says when there is no such team', async () => {
    await load(null);
    expect(component.history()).toBeNull();
    expect(el().querySelector('.hud-empty')!.textContent).toBe('No such team has played a season.');
    expect(seasons().length).toBe(0);
  });

  it('says when the history could not be loaded', async () => {
    fixture.detectChanges();
    request().flush('boom', { status: 500, statusText: 'Server Error' });
    await settle();

    expect(component.failed()).toBeTrue();
    expect(component.history()).toBeNull();
    expect(el().querySelector('.hud-empty--error')!.getAttribute('role')).toBe('alert');
    expect(el().querySelector('.history-totals')).toBeNull();
  });

  it('reloads when the team changes, clearing the old franchise meanwhile', async () => {
    await load(history);
    fixture.componentRef.setInput('teamID', 41);
    fixture.detectChanges();
    const next = request(41);

    expect(component.history()).toBeNull();
    expect(seasons().length).toBe(0);
    expect(el().querySelector('h1')!.textContent).toBe('Team history');

    next.flush({ ...history, teamName: 'Texas Shock' });
    await settle();
    expect(el().querySelector('h1')!.textContent).toBe('Texas Shock');
    expect(seasons().length).toBe(2);
  });

  it('cancels a request a newer team replaced, so its late answer never shows', async () => {
    fixture.detectChanges();
    const first = request();
    fixture.componentRef.setInput('teamID', 99);
    fixture.detectChanges();
    const second = request(99);

    expect(first.cancelled).toBeTrue();
    second.flush({ ...history, teamName: 'Other Hunters' });
    await settle();
    expect(el().querySelector('h1')!.textContent).toBe('Other Hunters');
  });
});
