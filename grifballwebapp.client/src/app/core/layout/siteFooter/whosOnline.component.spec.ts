import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { PresenceSnapshot } from '@api/models';
import { LiveService } from '../../live/live.service';
import { counted, presenceLabel } from './presencePages';
import { WhosOnlineComponent } from './whosOnline.component';

describe('WhosOnlineComponent', () => {
  const presence = signal<PresenceSnapshot | null>(null);

  function render(): HTMLElement {
    TestBed.configureTestingModule({
      imports: [WhosOnlineComponent],
      providers: [provideRouter([]), { provide: LiveService, useValue: { presence } }],
    });
    const fixture = TestBed.createComponent(WhosOnlineComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => presence.set(null));

  it('shows nothing until the hub has said who is on', () => {
    expect(render().textContent?.trim()).toBe('');
  });

  it('has no count and no pages until then', () => {
    render();
    const component = TestBed.createComponent(WhosOnlineComponent).componentInstance;

    expect([component.split(), component.pages()]).toEqual(['', []]);
  });

  it('shows how many are on, and each page (whose or which, when the server named it) with its members by name and its guests', () => {
    presence.set({
      online: 5, members: 3, guests: 2, pages: [
        { path: '/season/3/draft', subject: 'Season 5', guests: 1, members: [{ userID: 7, name: 'Hammer' }, { userID: 8, name: null }] },
        { path: '/profile/9', subject: 'Grunt Padre', guests: 0, members: [{ userID: 10, name: 'Doerstopper' }] },
        { path: '/staff', subject: null, guests: 1, members: [] },
      ],
    });

    const element = render();
    const text = (element.textContent ?? '').replace(/\s+/g, ' ');
    const links = [...element.querySelectorAll('a')].map(a => [a.textContent?.trim(), a.getAttribute('href')]);

    expect(text).toContain('5 online (3 members, 2 guests)');
    const texts = (selector: string) => [...element.querySelectorAll(selector)].map(e => (e.textContent ?? '').replace(/\s+/g, ' ').trim());
    expect(texts('.whos-online__where')).toEqual(['Season 5 draft', 'Grunt Padre’s profile', 'Staff area']);
    expect(texts('.whos-online__who')).toEqual(['Hammer, A member, 1 guest', 'Doerstopper', '1 guest']);
    expect(links).toEqual([
      ['Season 5 draft', '/season/3/draft'], ['Hammer', '/profile/7'], ['A member', '/profile/8'],
      ['Grunt Padre’s profile', '/profile/9'], ['Doerstopper', '/profile/10'],
    ]);
  });
});

describe('presenceLabel', () => {
  it('names the site\'s pages by their routes, and anything else "Elsewhere"', () => {
    expect(presenceLabel('/')).toBe('Home');
    expect(presenceLabel('/season')).toBe('A season');
    expect(presenceLabel('/season/4')).toBe('A season');
    expect(presenceLabel('/season/4/team/9')).toBe('A team');
    expect(presenceLabel('/seasonmatch/12')).toBe('A match');
    expect(presenceLabel('/match/0f8fad5b-d9cb-469f-a165-70867728950e')).toBe('A game');
    expect(presenceLabel('/late-league/2')).toBe('Late League');
    expect(presenceLabel('/staff')).toBe('Staff area');
    expect(presenceLabel('/somewhere/new')).toBe('Elsewhere');
  });

  it('says whose page or which one it is when the server named its subject, and the generic name without one', () => {
    expect(presenceLabel('/profile/3', 'Grunt Padre')).toBe('Grunt Padre’s profile');
    expect(presenceLabel('/xbox/2533274800000000', 'Doerstopper')).toBe('Doerstopper’s Xbox account');
    expect(presenceLabel('/season/4', 'Season 5')).toBe('Season 5');
    expect(presenceLabel('/season/4/draft', 'Season 5')).toBe('Season 5 draft');
    expect(presenceLabel('/season/4/teams', 'Season 5')).toBe('Season 5 teams');
    expect(presenceLabel('/season/4/signupForm', 'Season 5')).toBe('Signing up for Season 5');
    expect(presenceLabel('/seasonAvailability/4', 'Season 5')).toBe('Season 5 availability');
    expect(presenceLabel('/season/4/team/9', 'Reds (Season 5)')).toBe('Reds (Season 5)');
    expect(presenceLabel('/history/team/9', 'Reds (Season 5)')).toBe('History of Reds (Season 5)');
    expect(presenceLabel('/seasonmatch/12', 'Reds vs Blues (Season 5)')).toBe('Reds vs Blues (Season 5)');
    expect(presenceLabel('/reschedule-request/12', 'Reds vs Blues (Season 5)')).toBe('Rescheduling Reds vs Blues (Season 5)');
    expect(presenceLabel('/match/0f8fad5b-d9cb-469f-a165-70867728950e', 'Reds vs Blues (Season 5)')).toBe('A game of Reds vs Blues (Season 5)');
    expect(presenceLabel('/late-league/2', 'Winter 2026')).toBe('Late League: Winter 2026');
    expect(presenceLabel('/profile/3', null)).toBe('A profile');
    expect(presenceLabel('/leaderboards', 'ignored')).toBe('Leaderboards');
  });

  it('counts with the noun\'s plural', () => {
    expect(counted(1, 'guest')).toBe('1 guest');
    expect(counted(0, 'member')).toBe('0 members');
  });
});
