import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CommendationDto, ProfileCommendationsComponent, ProfileCommendationsDto, tierProgress } from './profileCommendations.component';

function commendation(id: number, changes: Partial<CommendationDto> = {}): CommendationDto {
  return {
    medalID: id, medalName: `Medal ${id}`, description: null, difficultyName: 'Normal', typeName: 'Spree',
    count: 12, matches: 3, tier: 1, tierFloor: 10, nextTierAt: 50, ...changes,
  };
}

describe('ProfileCommendationsComponent', () => {
  let fixture: ComponentFixture<ProfileCommendationsComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ProfileCommendationsComponent);
    fixture.componentRef.setInput('userID', 21);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const text = () => el().textContent!.replace(/\s+/g, ' ');

  async function respond(body: ProfileCommendationsDto): Promise<void> {
    http.expectOne('api/users/21/commendations').flush(body);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('says it is loading until the commendations come', () => {
    expect(text()).toContain('Loading commendations');
    http.expectOne('api/users/21/commendations').flush({ userID: 21, totalMedals: 0, distinctMedals: 0, commendations: [] });
  });

  it('lists the most earned medals, with their counts, tiers and progress', async () => {
    await respond({
      userID: 21, totalMedals: 1234, distinctMedals: 7,
      commendations: [
        commendation(3, { medalName: 'Double Kill', description: 'Kill 2 enemies quickly.', count: 75, matches: 1, tier: 2, tierFloor: 50, nextTierAt: 100 }),
        commendation(5, { medalName: 'Overkill', difficultyName: 'Mythic', count: 1200, tier: 6, tierFloor: 1000, nextTierAt: null }),
      ],
    });

    const cards = el().querySelectorAll<HTMLElement>('.commendation-card');
    expect(cards.length).toBe(2);
    expect(text()).toContain('1,234 Halo Infinite medals earned, of 7 kinds.');

    expect(cards[0].querySelector('.commendation-name')!.textContent).toBe('Double Kill');
    expect(cards[0].querySelector('.commendation-count')!.textContent).toContain('75');
    expect(cards[0].getAttribute('title')).toBe('Kill 2 enemies quickly.');
    expect(cards[0].textContent).toContain('1 game');
    expect(cards[0].textContent).toContain('Tier 2');
    expect(cards[0].textContent).toContain('Next at 100');
    expect(cards[0].querySelector('[role="progressbar"]')!.getAttribute('aria-valuenow')).toBe('50');

    expect(cards[1].querySelector('.difficulty-pill')!.getAttribute('data-difficulty')).toBe('mythic');
    expect(cards[1].textContent).toContain('3 games');
    expect(cards[1].textContent).toContain('Max tier');
    expect(cards[1].getAttribute('title')).toBeNull();
    expect(cards[1].querySelector('[role="progressbar"]')!.getAttribute('aria-valuenow')).toBe('100');
  });

  it('leaves the difficulty out when the medal has none', async () => {
    await respond({ userID: 21, totalMedals: 12, distinctMedals: 1, commendations: [commendation(1, { difficultyName: null })] });

    expect(el().querySelector('.difficulty-pill')).toBeNull();
  });

  it('says one medal and one kind in the singular', async () => {
    await respond({ userID: 21, totalMedals: 1, distinctMedals: 1, commendations: [commendation(1, { count: 1, matches: 1, tier: 0, tierFloor: 0, nextTierAt: 10 })] });

    expect(text()).toContain('1 Halo Infinite medal earned, of 1 kind.');
  });

  it('says when no medals are recorded', async () => {
    await respond({ userID: 21, totalMedals: 0, distinctMedals: 0, commendations: [] });

    expect(el().querySelector('.commendation-card')).toBeNull();
    expect(text()).toContain('No Halo Infinite medals recorded');
  });

  it('says when the commendations fail to load', async () => {
    http.expectOne('api/users/21/commendations').flush('x', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text()).toContain("Couldn't load commendations");
  });

  it('loads the commendations again for another player', async () => {
    await respond({ userID: 21, totalMedals: 0, distinctMedals: 0, commendations: [] });
    fixture.componentRef.setInput('userID', 22);
    fixture.detectChanges();

    http.expectOne('api/users/22/commendations').flush({ userID: 22, totalMedals: 1, distinctMedals: 1, commendations: [commendation(1, { count: 1, matches: 1, tier: 0, tierFloor: 0, nextTierAt: 10 })] });
    await fixture.whenStable();
    fixture.detectChanges();

    // The other player's, in place of the first's (who had none)
    expect(text()).toContain('1 Halo Infinite medal earned, of 1 kind.');
    expect(text()).not.toContain('No Halo Infinite medals recorded');
  });
});

describe('tierProgress', () => {
  it('is the way from the tier reached to the next', () => {
    expect(tierProgress(commendation(1, { count: 30, tierFloor: 10, nextTierAt: 50 }))).toBe(50);
    expect(tierProgress(commendation(1, { count: 0, tierFloor: 0, nextTierAt: 10 }))).toBe(0);
  });

  it('is full at the last tier', () => {
    expect(tierProgress(commendation(1, { count: 5000, tierFloor: 1000, nextTierAt: null }))).toBe(100);
  });

  it('is full when the next tier is no further than the last', () => {
    expect(tierProgress(commendation(1, { count: 10, tierFloor: 10, nextTierAt: 10 }))).toBe(100);
  });

  it('stays within 0 to 100', () => {
    expect(tierProgress(commendation(1, { count: 5, tierFloor: 10, nextTierAt: 50 }))).toBe(0);
    expect(tierProgress(commendation(1, { count: 80, tierFloor: 10, nextTierAt: 50 }))).toBe(100);
  });
});
