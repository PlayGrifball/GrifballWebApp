import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SeasonChampionsComponent } from './seasonChampions.component';

describe('SeasonChampionsComponent', () => {
  let fixture: ComponentFixture<SeasonChampionsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    fixture = TestBed.createComponent(SeasonChampionsComponent);
    fixture.componentRef.setInput('seasonID', 12);
    fixture.componentRef.setInput('champions', [
      { tier: 'Pro', teamID: 40, teamName: 'Relentless', roster: [
        { userID: 7, gamertag: 'Captain R', isCaptain: true },
        { userID: 8, gamertag: 'Runner', isCaptain: false },
      ] },
      { tier: 'Amateur', teamID: 41, teamName: 'Underdogs', roster: [] },
    ]);
    fixture.detectChanges();
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('links each tier\'s team to its team page and its players to their profiles', () => {
    const hrefs = Array.from(el().querySelectorAll('a')).map(a => a.getAttribute('href'));
    expect(hrefs).toEqual(['/season/12/team/40', '/profile/7', '/profile/8', '/season/12/team/41']);
    expect(Array.from(el().querySelectorAll('.champion__tier')).map(t => t.textContent!.trim())).toEqual(['Pro champions', 'Amateur champions']);
  });

  it('marks the captain', () => {
    const captains = el().querySelectorAll('.champion__captain');
    expect(captains.length).toBe(1);
    expect(captains[0].parentElement!.textContent).toContain('Captain R');
  });
});
