import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { provideRouter, Router } from '@angular/router';
import type { QuickSearchDto } from '../api/dtos/quickSearchDto';
import { QUICK_SEARCH_DEBOUNCE_MS, QuickSearchComponent, seasonYears, toGroups } from './quickSearch.component';

const hits: QuickSearchDto = {
  players: [
    { userID: 3, name: 'Sarge', pastGamertag: null },
    { userID: 4, name: 'Simmons', pastGamertag: 'KissAss' },
  ],
  teams: [{ teamID: 7, teamName: 'Red Team', seasonID: 2, seasonName: 'Season 2' }],
  seasons: [{ seasonID: 2, seasonName: 'Season 2', start: '2018-12-01T00:00:00Z', end: '2019-02-01T00:00:00Z' }],
};

describe('QuickSearch helpers', () => {
  it('gives a season one year, or two when it crossed a new year', () => {
    expect(seasonYears({ start: '2019-03-01T00:00:00Z', end: '2019-05-01T00:00:00Z' })).toBe('2019');
    expect(seasonYears({ start: '2018-12-01T00:00:00Z', end: '2019-02-01T00:00:00Z' })).toBe('2018–2019');
  });

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
    http.expectOne(r => r.url === '/api/Search/QuickSearch' && r.params.get('q') === q).flush(result);
    await settle();
  };

  it('asks for more until two characters are typed, searching nothing', () => {
    expect(status()).toContain('Type a gamertag');
    type(' s ');
    expect(status()).toContain('Type a gamertag');
    http.expectNone('/api/Search/QuickSearch');
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

  it('picks a hit on hover and goes to it on a click', async () => {
    type('sarge');
    await answer('sarge');
    options()[3].dispatchEvent(new Event('mouseenter'));
    fixture.detectChanges();
    expect(component.active()).toBe(3);

    options()[3].click();
    expect(navigate).toHaveBeenCalledWith(['/season', 2]);
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

  it('says when the search failed', async () => {
    type('sarge');
    http.expectOne(r => r.url === '/api/Search/QuickSearch').flush('boom', { status: 500, statusText: 'Server Error' });
    await settle();
    expect(status()).toBe('The search failed. Try again.');
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
