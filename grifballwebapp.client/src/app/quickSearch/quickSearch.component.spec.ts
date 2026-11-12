import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { provideRouter, Router } from '@angular/router';
import { QuickSearchDto, QuickSearchPlayerDto } from '@api/models';
import { playerDetail } from './playerDetail';
import { QUICK_SEARCH_DEBOUNCE_MS, QuickSearchComponent, toGroups } from './quickSearch.component';

const hits: QuickSearchDto = {
  players: [
    { userID: 3, name: 'Sarge', pastGamertag: null, matchedName: null, kind: null, years: null },
    { userID: 4, name: 'Simmons', pastGamertag: 'KissAss', matchedName: null, kind: null, years: null },
  ],
  teams: [{ teamID: 7, teamName: 'Red Team', seasonID: 2, seasonName: 'Season 2' }],
  seasons: [{ seasonID: 2, seasonName: 'Season 2', start: '2018-12-01T00:00:00Z', end: '2019-02-01T00:00:00Z', years: '2018–2019' }],
};

describe('QuickSearch helpers', () => {
  it('groups the hits as players, teams and seasons, each linking to its page, leaving out empty groups', () => {
    const groups = toGroups(hits);
    expect(groups.map(g => g.heading)).toEqual(['Players', 'Teams', 'Seasons']);
    expect(groups[0].items.map(i => [i.label, i.detail, i.link])).toEqual([
      ['Sarge', null, ['/profile', 3]],
      ['Simmons', 'formerly KissAss', ['/profile', 4]],
    ]);
    expect(groups[1].items.map(i => [i.label, i.detail, i.link])).toEqual([['Red Team', 'Season 2', ['/season', 2, 'team', 7]]]);
    expect(groups[2].items.map(i => [i.label, i.detail, i.link])).toEqual([['Season 2', '2018–2019', ['/season', 2]]]);

    expect(toGroups({ players: [], teams: hits.teams, seasons: [] }).map(g => g.kind)).toEqual(['team']);

    // Found by their Discord or display name: that name shown beside the one the site uses
    const zebra = toGroups({ players: [{ userID: 5, name: 'Zebra', pastGamertag: null, matchedName: 'griffo', kind: null, years: null }], teams: [], seasons: [] });
    expect(zebra[0].items[0].detail).toBe('aka griffo');
  });
});

describe('playerDetail', () => {
  const p = (extra: Partial<QuickSearchPlayerDto>): QuickSearchPlayerDto => ({ userID: 1, name: 'Hellmasker1', pastGamertag: null, matchedName: null, kind: null, years: null, ...extra });

  it('tells two players of one name apart: the current gamertag, or the archive player with their years', () => {
    expect(playerDetail(p({ kind: 'Xbox' }))).toBe('Current gamertag');
    expect(playerDetail(p({ kind: 'Archive', years: '2009–2011' }))).toBe('Archive · 2009–2011');
    expect(playerDetail(p({ kind: 'Archive', years: null }))).toBe('Archive');
    expect(playerDetail(p({ kind: null }))).toBeNull();
  });

  it('puts the name the search matched first', () => {
    expect(playerDetail(p({ kind: 'Xbox', pastGamertag: 'OldTag' }))).toBe('formerly OldTag');
    expect(playerDetail(p({ kind: 'Xbox', matchedName: 'griffo' }))).toBe('aka griffo');
  });
});

describe('QuickSearchComponent', () => {
  let fixture: ComponentFixture<QuickSearchComponent>;
  let component: QuickSearchComponent;
  let http: HttpTestingController;
  let dialogRef: jasmine.SpyObj<MatDialogRef<QuickSearchComponent>>;
  let navigate: jasmine.Spy;

  beforeEach(() => {
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    TestBed.configureTestingModule({
      imports: [QuickSearchComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(QuickSearchComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const input = () => el().querySelector<HTMLInputElement>('input[role="combobox"]')!;
  const status = () => el().querySelector('[role="status"]')!.textContent!.trim();
  const options = () => Array.from(el().querySelectorAll<HTMLElement>('[role="option"]'));
  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }
  // Types into the box on a mock clock, then lets the pause pass
  const type = (value: string) => {
    jasmine.clock().install();
    try {
      input().value = value;
      input().dispatchEvent(new Event('input'));
      jasmine.clock().tick(QUICK_SEARCH_DEBOUNCE_MS);
    } finally {
      jasmine.clock().uninstall();
    }
    fixture.detectChanges();
  };
  const key = (key: string, init: KeyboardEventInit = {}) => {
    const event = new KeyboardEvent('keydown', { key, cancelable: true, ...init });
    input().dispatchEvent(event);
    fixture.detectChanges();
    return event;
  };
  const answer = async (q: string, result: QuickSearchDto = hits) => {
    http.expectOne(r => r.url === '/api/search' && r.params.get('q') === q).flush(result);
    await settle();
  };

  it('asks for more until two characters are typed, searching nothing', () => {
    expect(status()).toContain('Type a gamertag');
    type(' s ');
    expect(status()).toContain('Type a gamertag');
    http.expectNone('/api/search');
    expect(input().getAttribute('aria-expanded')).toBe('false');
  });

  it('searches once typing pauses, then lists the hits by group, the first picked', async () => {
    jasmine.clock().install();
    try {
      input().value = 'sa';
      input().dispatchEvent(new Event('input'));
      jasmine.clock().tick(QUICK_SEARCH_DEBOUNCE_MS - 1);
      input().value = 'sarge ';
      input().dispatchEvent(new Event('input'));
      jasmine.clock().tick(QUICK_SEARCH_DEBOUNCE_MS);
    } finally {
      jasmine.clock().uninstall();
    }
    fixture.detectChanges();
    expect(status()).toBe('Searching…');
    await answer('sarge');

    expect(Array.from(el().querySelectorAll('.qs-group')).map(g => g.textContent!.trim())).toEqual(['Players', 'Teams', 'Seasons']);
    expect(options().map(o => o.querySelector('.qs-item__label')!.textContent!.trim())).toEqual(['Sarge', 'Simmons', 'Red Team', 'Season 2']);
    expect(options()[1].querySelector('.qs-item__detail')!.textContent!.trim()).toBe('formerly KissAss');
    expect(input().getAttribute('aria-expanded')).toBe('true');
    expect(input().getAttribute('aria-activedescendant')).toBe(options()[0].id);
    expect(options()[0].getAttribute('aria-selected')).toBe('true');
    expect(status()).toBe('');
  });

  it('moves the pick with the arrow keys and Ctrl+Home/End, round the ends', async () => {
    type('sarge');
    await answer('sarge');

    expect(key('ArrowDown').defaultPrevented).toBeTrue();
    expect(component.active()).toBe(1);
    key('ArrowUp');
    key('ArrowUp');
    expect(component.active()).toBe(3);
    key('ArrowDown');
    expect(component.active()).toBe(0);
    key('End', { ctrlKey: true });
    expect(component.active()).toBe(3);
    key('Home', { ctrlKey: true });
    expect(component.active()).toBe(0);
    // Without Ctrl, Home and End move the caret
    expect(key('End').defaultPrevented).toBeFalse();
    expect(component.active()).toBe(0);
    expect(input().getAttribute('aria-activedescendant')).toBe(options()[0].id);
  });

  it('goes to the picked hit on Enter, closing the dialog', async () => {
    type('sarge');
    await answer('sarge');
    key('ArrowDown');
    key('ArrowDown');

    expect(key('Enter').defaultPrevented).toBeTrue();
    expect(dialogRef.close).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/season', 2, 'team', 7]);
  });

  // A move of the pointer to a spot on the screen, over an element of the list
  const pointer = (target: Element, screenX: number, screenY = 0) => {
    target.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, screenX, screenY }));
    fixture.detectChanges();
  };

  it('picks the hit the pointer moves over and goes to it on a click', async () => {
    type('sarge');
    await answer('sarge');
    // The first move seen only marks where the pointer is
    pointer(options()[2], 10);
    expect(component.active()).toBe(0);
    pointer(options()[3].querySelector('.qs-item__label')!, 12);
    expect(component.active()).toBe(3);
    // Off the hits (a group's heading), the pick stays
    pointer(el().querySelector('.qs-group')!, 14);
    expect(component.active()).toBe(3);

    options()[3].click();
    expect(navigate).toHaveBeenCalledWith(['/season', 2]);
  });

  it('keeps the keyboard pick when the list scrolls under a resting pointer', async () => {
    type('sarge');
    await answer('sarge');
    pointer(options()[0], 10, 10);
    key('ArrowDown');
    key('ArrowDown');
    expect(component.active()).toBe(2);

    // What the browser sends after the scroll: the pointer enters the hit now under it, and a move at the same spot
    options()[3].dispatchEvent(new Event('mouseenter'));
    pointer(options()[3], 10, 10);
    expect(component.active()).toBe(2);
    expect(key('Enter').defaultPrevented).toBeTrue();
    expect(navigate).toHaveBeenCalledOnceWith(['/season', 2, 'team', 7]);
  });
  it('ignores Enter and the arrows with no hits, and says when nothing matches', async () => {
    type('zzz');
    await answer('zzz', { players: [], teams: [], seasons: [] });
    expect(status()).toBe('Nothing matches “zzz”.');
    expect(key('Enter').defaultPrevented).toBeFalse();
    key('ArrowDown');
    expect(component.active()).toBe(0);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('labels each group of options by its heading', async () => {
    type('sarge');
    await answer('sarge');
    const groups = Array.from(el().querySelectorAll<HTMLElement>('[role="listbox"] > [role="group"]'));
    expect(groups.map(g => el().querySelector('#' + g.getAttribute('aria-labelledby'))!.textContent!.trim())).toEqual(['Players', 'Teams', 'Seasons']);
    expect(groups.map(g => g.querySelectorAll('[role="option"]').length)).toEqual([2, 1, 1]);
  });

  it('keeps the last hits listed while the next ones load', async () => {
    type('sa');
    await answer('sa');
    key('ArrowDown');

    type('sarge');
    expect(status()).toBe('Searching…');
    expect(options()).toHaveSize(4);
    expect(component.active()).toBe(1);

    await answer('sarge', { players: [hits.players[0]], teams: [], seasons: [] });
    expect(options()).toHaveSize(1);
    expect(component.active()).toBe(0);
  });

  it('keeps the pick in view as the arrow keys move it', async () => {
    type('sarge');
    await answer('sarge');
    const scroll = spyOn(Element.prototype, 'scrollIntoView');

    key('ArrowDown');
    expect(scroll).toHaveBeenCalledOnceWith({ block: 'nearest' });
    expect(scroll.calls.mostRecent().object).toBe(options()[1]);
  });

  it('on Enter before typing pauses, searches now and goes to the first of its hits', async () => {
    type('sa');
    await answer('sa');
    input().value = 'simmons';
    input().dispatchEvent(new Event('input'));

    // The list is still for "sa": Enter does not go to its pick
    expect(key('Enter').defaultPrevented).toBeTrue();
    expect(navigate).not.toHaveBeenCalled();

    await answer('simmons', { players: [hits.players[1]], teams: [], seasons: [] });
    expect(navigate).toHaveBeenCalledOnceWith(['/profile', 4]);
    expect(dialogRef.close).toHaveBeenCalled();
  });

  it('on Enter while the hits load, goes to nothing when none come', async () => {
    type('zzz');
    expect(key('Enter').defaultPrevented).toBeTrue();
    await answer('zzz', { players: [], teams: [], seasons: [] });
    expect(navigate).not.toHaveBeenCalled();
    // Enter again acts on the list shown
    expect(key('Enter').defaultPrevented).toBeFalse();
  });

  it('on Enter before typing pauses with too little typed, searches nothing', () => {
    input().value = 's';
    input().dispatchEvent(new Event('input'));
    key('Enter');
    http.expectNone('/api/search');
    expect(navigate).not.toHaveBeenCalled();
  });

  const fail = async () => {
    http.expectOne(r => r.url === '/api/search').flush('boom', { status: 500, statusText: 'Server Error' });
    await settle();
  };

  it('says when the search failed, with a button to try again', async () => {
    type('sarge');
    await fail();
    expect(status()).toBe('The search failed. Press Enter to try again.');
    const retry = el().querySelector<HTMLButtonElement>('button.qs-retry')!;
    expect(retry.textContent!.trim()).toBe('Try again');

    retry.focus();
    retry.click();
    fixture.detectChanges();
    expect(document.activeElement).toBe(input());
    expect(status()).toBe('Searching…');
    await answer('sarge');
    expect(el().querySelector('button.qs-retry')).toBeNull();
    expect(options()).toHaveSize(4);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('on Enter after the search failed, searches again and goes to the first hit', async () => {
    type('sarge');
    await fail();

    expect(key('Enter').defaultPrevented).toBeTrue();
    expect(navigate).not.toHaveBeenCalled();
    await answer('sarge');
    expect(navigate).toHaveBeenCalledOnceWith(['/profile', hits.players[0].userID]);
    expect(dialogRef.close).toHaveBeenCalled();
  });

  it('on Enter after the search failed again, goes nowhere', async () => {
    type('sarge');
    await fail();
    key('Enter');
    await fail();
    expect(navigate).not.toHaveBeenCalled();
    expect(status()).toBe('The search failed. Press Enter to try again.');
  });

  it('drops a pending Enter once typing goes on', async () => {
    type('sa');
    // The hits for "sa" are loading: Enter waits for them
    expect(key('Enter').defaultPrevented).toBeTrue();
    // Typing goes on (still mid-word: its pause never comes here), then the hits for "sa" come: no jump
    jasmine.clock().install();
    try {
      input().value = 'sarg';
      input().dispatchEvent(new Event('input'));
    } finally {
      jasmine.clock().uninstall();
    }
    await answer('sa');
    expect(options()).toHaveSize(4);
    expect(navigate).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('closes from its Close button', () => {
    el().querySelector<HTMLButtonElement>('button[aria-label="Close"]')!.click();
    expect(dialogRef.close).toHaveBeenCalled();
  });
});

describe('QuickSearchComponent outside a dialog', () => {
  it('has no Close button and navigates without closing anything', () => {
    TestBed.configureTestingModule({
      imports: [QuickSearchComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(QuickSearchComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button[aria-label="Close"]')).toBeNull();
    fixture.componentInstance.close();
    fixture.componentInstance.go({ id: 'x', kind: 'season', label: 'S', detail: null, link: ['/season', 1] });
    expect(navigate).toHaveBeenCalledWith(['/season', 1]);
  });
});
