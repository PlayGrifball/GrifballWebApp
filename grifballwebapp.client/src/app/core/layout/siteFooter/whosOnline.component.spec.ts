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

  it('shows how many are on, and each page with its members by name and its guests', () => {
    presence.set({
      online: 4, members: 2, guests: 2, pages: [
        { path: '/season/3/draft', guests: 1, members: [{ userID: 7, name: 'Hammer' }, { userID: 8, name: null }] },
        { path: '/staff', guests: 1, members: [] },
      ],
    });

    const element = render();
    const text = (element.textContent ?? '').replace(/\s+/g, ' ');
    const links = [...element.querySelectorAll('a')].map(a => [a.textContent?.trim(), a.getAttribute('href')]);

    expect(text).toContain('4 online (2 members, 2 guests)');
    const texts = (selector: string) => [...element.querySelectorAll(selector)].map(e => (e.textContent ?? '').replace(/\s+/g, ' ').trim());
    expect(texts('.whos-online__where')).toEqual(['A draft', 'Staff area']);
    expect(texts('.whos-online__who')).toEqual(['Hammer, A member, 1 guest', '1 guest']);
    expect(links).toEqual([['A draft', '/season/3/draft'], ['Hammer', '/profile/7'], ['A member', '/profile/8']]);
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

  it('counts with the noun\'s plural', () => {
    expect(counted(1, 'guest')).toBe('1 guest');
    expect(counted(0, 'member')).toBe('0 members');
  });
});
