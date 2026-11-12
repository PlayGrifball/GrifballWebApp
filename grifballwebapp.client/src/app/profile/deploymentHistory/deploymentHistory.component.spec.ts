import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { deploymentFinishLabel, type DeploymentHistoryDto } from '../../api/dtos/deploymentHistoryDto';
import type { SeasonInfoDto } from '../../api/dtos/seasonInfoDto';
import { DeploymentHistoryComponent } from './deploymentHistory.component';

const season = (overrides: Partial<SeasonInfoDto> = {}): SeasonInfoDto => ({
  seasonID: 5,
  seasonName: 'Summer League 2018',
  start: '2018-06-01T00:00:00Z',
  end: '2018-08-01T00:00:00Z',
  year: 2018,
  hasEnded: true,
  leagueID: 1,
  leagueName: 'AGLA',
  leagueSlug: 'agla',
  format: 'LeagueSeason',
  isHistorical: false,
  ...overrides,
} as SeasonInfoDto);

const line = (overrides: Partial<DeploymentHistoryDto> = {}): DeploymentHistoryDto => ({
  season: season(),
  teamID: 31,
  teamName: 'Dynasty',
  isCaptain: false,
  wins: 8,
  losses: 2,
  recordFromSource: false,
  finish: 'RegularSeason',
  championshipTiers: [],
  ...overrides,
});

describe('DeploymentHistoryComponent', () => {
  let fixture: ComponentFixture<DeploymentHistoryComponent>;
  let http: HttpTestingController;
  const el = () => fixture.nativeElement as HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DeploymentHistoryComponent],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DeploymentHistoryComponent);
    fixture.componentRef.setInput('userID', 21);
  });

  afterEach(() => http.verify());

  async function load(body: DeploymentHistoryDto[] | null): Promise<void> {
    fixture.detectChanges();
    http.expectOne('/api/users/21/deployment-history').flush(body);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows nothing for a player on no team', async () => {
    await load([]);
    expect(el().querySelector('section')).toBeNull();
  });

  it('says when the history could not be loaded, and retries', async () => {
    fixture.detectChanges();
    http.expectOne('/api/users/21/deployment-history').flush('down', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    const alert = el().querySelector('section [role="alert"]')!;
    expect(alert.textContent).toContain('could not be loaded');
    expect(el().querySelector('li.deployment')).toBeNull();

    alert.querySelector<HTMLButtonElement>('button')!.click();
    await load([line()]);
    expect(el().querySelector('[role="alert"]')).toBeNull();
    expect(el().querySelectorAll('li.deployment')).toHaveSize(1);
  });

  it('lists only the chosen game\'s deployments, keeping a season of more than one game', async () => {
    await load([
      line({ season: season({ seasonID: 1, gameVersionSlug: 'halo-3' }), teamID: 1, teamName: 'Three' }),
      line({ season: season({ seasonID: 2, gameVersionSlug: 'halo-reach' }), teamID: 2, teamName: 'Reach' }),
      line({ season: season({ seasonID: 3, gameVersionSlug: null }), teamID: 3, teamName: 'Mixed' }),
    ]);
    const teams = () => Array.from(el().querySelectorAll('.deployment__team a')).map(a => a.textContent!.trim());
    expect(teams()).toEqual(['Three', 'Reach', 'Mixed']);

    fixture.componentRef.setInput('game', 'halo-3');
    fixture.detectChanges();
    expect(teams()).toEqual(['Three', 'Mixed']);

    fixture.componentRef.setInput('game', 'all');
    fixture.detectChanges();
    expect(teams()).toEqual(['Three', 'Reach', 'Mixed']);
  });

  it('lists each deployment with its season, team, captaincy, record and finish', async () => {
    await load([
      line({ season: season({ seasonID: 9, seasonName: 'Season 9', year: 2026, hasEnded: false }), teamID: 90, teamName: 'Bravo', isCaptain: true, wins: 1, losses: 0, finish: 'InProgress' }),
      line({ finish: 'Champion', championshipTiers: ['Pro'], recordFromSource: true }),
    ]);

    const section = el().querySelector('section')!;
    expect(section.getAttribute('aria-labelledby')).toBe('deployment-heading');
    expect(section.querySelector('h2')!.textContent).toContain('Deployment History');
    const rows = Array.from(section.querySelectorAll<HTMLElement>('li.deployment'));
    expect(rows).toHaveSize(2);

    const [current, title] = rows;
    const links = (row: HTMLElement) => Array.from(row.querySelectorAll('a')).map(a => [a.textContent!.trim(), a.getAttribute('href')]);
    expect(links(current)).toEqual([['Season 9', '/season/9'], ['Bravo', '/season/9/team/90']]);
    expect(current.querySelector('.deployment__captain')?.textContent).toBe('Captain');
    expect(current.querySelector('.deployment__record')!.textContent!.replace(/\s+/g, ' ').trim()).toBe('Team 1-0');
    expect(current.querySelector('.deployment__record')!.getAttribute('title')).toBe('Team record');
    expect(current.querySelector('.deployment__finish')!.textContent!.trim()).toBe('In progress');
    expect(current.classList).not.toContain('deployment--champion');
    expect(current.querySelector('.deployment__meta')!.textContent).toContain('2026');

    expect(links(title)).toEqual([['Summer League 2018', '/season/5'], ['Dynasty', '/season/5/team/31']]);
    expect(title.querySelector('.deployment__captain')).toBeNull();
    expect(title.querySelector('.deployment__record')!.getAttribute('title')).toBe('Team record, from the source spreadsheet');
    expect(title.querySelector('.deployment__finish')!.textContent).toContain('Pro Champion');
    expect(title.querySelector('.deployment__finish')!.getAttribute('data-finish')).toBe('Champion');
    expect(title.classList).toContain('deployment--champion');
  });

  it('treats a null body as no history', async () => {
    await load(null);
    expect(el().querySelector('section')).toBeNull();
  });
});

describe('deploymentFinishLabel', () => {
  it('names each finish, a champion with every tier it won', () => {
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: ['Pro'] })).toBe('Pro Champion');
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: ['Open'] })).toBe('Open Champion');
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: ['Amateur', 'Pro'] })).toBe('Amateur & Pro Champion');
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: [] })).toBe('Champion');
    expect(deploymentFinishLabel({ finish: 'Champion' } as Parameters<typeof deploymentFinishLabel>[0])).toBe('Champion');
    expect(deploymentFinishLabel({ finish: 'Playoffs', championshipTiers: [] })).toBe('Playoffs');
    expect(deploymentFinishLabel({ finish: 'Eliminated', championshipTiers: [] })).toBe('Eliminated');
    expect(deploymentFinishLabel({ finish: 'RegularSeason', championshipTiers: [] })).toBe('Regular season');
    expect(deploymentFinishLabel({ finish: 'InProgress', championshipTiers: [] })).toBe('In progress');
  });
});
