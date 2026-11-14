import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { onlyMatch, PlayerSearchComponent, sameIgnoringCase } from './playerSearch.component';
import { SEARCH_DEBOUNCE_MS } from './playerSearch.store';
import type { QuickSearchPlayerDto } from '@api/models';

describe('PlayerSearchComponent', () => {
  let fixture: ComponentFixture<PlayerSearchComponent>;
  let http: HttpTestingController;
  let picked: QuickSearchPlayerDto[];
  let submitted: string[];
  let cleared: number;
  const found: QuickSearchPlayerDto[] = [
    { userID: 1, name: 'Legendary', pastGamertag: null, kind: 'Archive', years: '2009–2011', matchedName: null },
    { userID: 2, name: 'Silva', pastGamertag: 'The Silvaa', kind: 'Xbox', matchedName: null, years: null },
    { userID: 3, name: 'Wall', pastGamertag: null, kind: 'Xbox', matchedName: null, years: null },
  ];

  beforeEach(() => {
    jasmine.clock().install();
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PlayerSearchComponent);
    picked = [];
    submitted = [];
    cleared = 0;
    fixture.componentInstance.picked.subscribe(p => picked.push(p));
    fixture.componentInstance.submitted.subscribe(q => submitted.push(q));
    fixture.componentInstance.cleared.subscribe(() => cleared++);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
    jasmine.clock().uninstall();
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const input = () => el().querySelector<HTMLInputElement>('#playerSearchInput')!;
  const list = () => el().querySelector<HTMLUListElement>('#playerSearchList')!;
  const options = () => Array.from(el().querySelectorAll<HTMLLIElement>('[role="option"]'));
  const type = (text: string) => {
    input().value = text;
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  const key = (name: string) => {
    const event = new KeyboardEvent('keydown', { key: name, cancelable: true });
    input().dispatchEvent(event);
    fixture.detectChanges();
    return event;
  };
  const searched = (text: string): TestRequest => http.expectOne(r => r.url === '/api/leaderboards/players' && r.params.get('query') === text);
  /** Types the text, waits out the pause and answers with the players found. */
  const showResults = (text = 'Le', results = found) => {
    type(text);
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    searched(text).flush(results);
    fixture.detectChanges();
  };

  it('searches once typing pauses, then lists the players found labelled as the toolbar search labels them', () => {
    type('S');
    type('Si');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS - 1);
    http.expectNone('/api/leaderboards/players');
    type(' Sil ');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    searched('Sil').flush(found);
    fixture.detectChanges();

    expect(list().hidden).toBeFalse();
    expect(input().getAttribute('aria-expanded')).toBe('true');
    expect(input().getAttribute('aria-controls')).toBe('playerSearchList');
    expect(options().map(o => o.textContent!.replace(/\s+/g, ' ').trim())).toEqual(['Legendary Archive · 2009–2011', 'Silva formerly The Silvaa', 'Wall Current gamertag']);
  });

  it('never searches fewer than two characters, and closes the list for them', () => {
    showResults();
    type('L');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    http.expectNone('/api/leaderboards/players');
    expect(list().hidden).toBeTrue();
    expect(input().getAttribute('aria-expanded')).toBe('false');
  });

  it('cancels an older search when the text changes, so its late answer is dropped', () => {
    type('ab');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    const older = searched('ab');
    type('abc');
    expect(older.cancelled).toBeTrue();
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    searched('abc').flush([found[0]]);
    fixture.detectChanges();
    expect(options()).toHaveSize(1);
  });

  it('keeps the list closed when nothing is found, or the search fails', () => {
    showResults('zz', []);
    expect(list().hidden).toBeTrue();

    type('Leg');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    searched('Leg').flush('', { status: 500, statusText: 'down' });
    fixture.detectChanges();
    expect(list().hidden).toBeTrue();
    expect(fixture.componentInstance.results()).toEqual([]);
  });

  it('drops the pending search on clear: nothing opens afterwards, and the box empties', () => {
    type('abc');
    el().querySelector<HTMLButtonElement>('.search__clear')!.click();
    fixture.detectChanges();
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS * 2);
    http.expectNone('/api/leaderboards/players');
    expect(cleared).toBe(1);
    expect(fixture.componentInstance.query()).toBe('');
    expect(el().querySelector('.search__clear')).toBeNull();
    expect(list().hidden).toBeTrue();
  });

  it('drops the search in flight on reset, emptying the box without saying it was cleared', () => {
    type('ab');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    const inFlight = searched('ab');
    fixture.componentInstance.reset();
    fixture.detectChanges();
    expect(inFlight.cancelled).toBeTrue();
    expect(cleared).toBe(0);
    expect(input().value).toBe('');
    expect(list().hidden).toBeTrue();
  });

  it('is a plain text box, so the browser adds no clear control of its own that would skip clear()', () => {
    expect(input().type).toBe('text');
    expect(input().getAttribute('role')).toBe('combobox');
  });

  it('drops the pending search on submit, and looks up the trimmed text', () => {
    type('  abc ');
    el().querySelector<HTMLButtonElement>('.hud-btn')!.click();
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS * 2);
    http.expectNone('/api/leaderboards/players');
    expect(submitted).toEqual(['abc']);

    type('   ');
    el().querySelector<HTMLButtonElement>('.hud-btn')!.click();
    expect(submitted).toEqual(['abc']);
  });

  it('picks a suggestion with the mouse, filling the box and closing the list', () => {
    showResults();
    options()[1].dispatchEvent(new Event('mousedown'));
    fixture.detectChanges();
    expect(picked).toEqual([found[1]]);
    expect(input().value).toBe('Silva');
    expect(list().hidden).toBeTrue();
  });

  it('moves through the suggestions with the arrow keys, and picks the one chosen with Enter', () => {
    showResults();
    expect(key('ArrowDown').defaultPrevented).toBeTrue();
    expect(input().getAttribute('aria-activedescendant')).toBe('playerSearchOption0');
    expect(options()[0].getAttribute('aria-selected')).toBe('true');
    key('ArrowUp');
    expect(input().getAttribute('aria-activedescendant')).toBe('playerSearchOption2');
    key('ArrowDown');
    expect(input().getAttribute('aria-activedescendant')).toBe('playerSearchOption0');
    key('ArrowUp');
    key('ArrowUp');
    expect(options()[1].classList).toContain('search__option--active');

    key('Enter');
    expect(picked).toEqual([found[1]]);
    expect(submitted).toEqual([]);
    expect(list().hidden).toBeTrue();
  });

  it('on Enter with no suggestion chosen, picks the one player whose name or past gamertag it is', () => {
    showResults('the silvaa');
    key('Enter');
    expect(picked).toEqual([found[1]]);
    expect(submitted).toEqual([]);
  });

  it('on Enter with several players and none named so, opens the list to choose from instead of guessing', () => {
    const twins: QuickSearchPlayerDto[] = [
      { userID: 7, name: 'Twin', pastGamertag: null, kind: 'Xbox', matchedName: null, years: null },
      { userID: 8, name: 'Twin', pastGamertag: null, kind: 'Archive', years: '2010', matchedName: null },
    ];
    showResults('Twin', twins);
    key('Escape');
    key('Enter');
    expect(picked).toEqual([]);
    expect(submitted).toEqual([]);
    expect(list().hidden).toBeFalse();
    expect(input().getAttribute('aria-activedescendant')).toBe('playerSearchOption0');
  });

  it('looks up the text on Enter when no suggestions are shown for it yet', () => {
    showResults('zz', []);
    key('Enter');
    expect(submitted).toEqual(['zz']);
    type('Legend');
    key('Enter');
    expect(submitted).toEqual(['zz', 'Legend']);
    expect(picked).toEqual([]);
  });

  it('points to one player only: the one named so (ignoring case), else the only one found', () => {
    const p = (userID: number, name: string, extra: Partial<QuickSearchPlayerDto> = {}): QuickSearchPlayerDto => ({ userID, name, pastGamertag: null, matchedName: null, kind: null, years: null, ...extra });
    expect(onlyMatch([p(1, 'Bob'), p(2, 'Bobby')], 'bob')?.userID).toBe(1);
    expect(onlyMatch([p(1, 'Robert', { matchedName: 'Bob' }), p(2, 'Bobby')], 'BOB')?.userID).toBe(1);
    expect(onlyMatch([p(1, 'Bobby')], 'Bob')?.userID).toBe(1);
    expect(onlyMatch([p(1, 'Bob'), p(2, 'Bob')], 'Bob')).toBeNull();
    expect(onlyMatch([p(1, 'Bobby'), p(2, 'Bobcat')], 'Bob')).toBeNull();
    expect(onlyMatch([], 'Bob')).toBeNull();
  });

  it('compares names as the server does (OrdinalIgnoreCase): case only, no accent or ß-to-SS folding', () => {
    expect(sameIgnoringCase('Élan', 'éLAN')).toBeTrue();
    expect(sameIgnoringCase('Elan', 'Élan')).toBeFalse();
    expect(sameIgnoringCase('Straße', 'STRASSE')).toBeFalse();
    expect(sameIgnoringCase('Straße', 'STRAßE')).toBeTrue();
    // 'ß' has no one-character capital in .NET's mapping either: not the capital sharp s
    expect(sameIgnoringCase('Straße', 'STRAẞE')).toBeFalse();
    expect(sameIgnoringCase('Bob', 'Bobby')).toBeFalse();
    // So where the server would point to one player, so does the box: not both of them
    const p = (userID: number, name: string): QuickSearchPlayerDto => ({ userID, name, pastGamertag: null, matchedName: null, kind: null, years: null });
    expect(onlyMatch([p(1, 'Strasse'), p(2, 'Straße')], 'straße')?.userID).toBe(2);
    expect(onlyMatch([p(1, 'Resume'), p(2, 'Résumé')], 'resume')?.userID).toBe(1);
  });

  it('closes the list on Escape and on blur at once, and reopens it with the arrow keys', () => {
    showResults();
    key('ArrowDown');
    key('Escape');
    expect(list().hidden).toBeTrue();
    expect(input().hasAttribute('aria-activedescendant')).toBeFalse();

    key('ArrowDown');
    expect(list().hidden).toBeFalse();
    input().dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(list().hidden).toBeTrue();
  });

  it('ignores the arrow keys with no suggestions, and other keys', () => {
    expect(key('ArrowDown').defaultPrevented).toBeFalse();
    expect(key('a').defaultPrevented).toBeFalse();
    expect(list().hidden).toBeTrue();
  });
});
