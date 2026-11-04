import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { PlayerSearchComponent, SEARCH_DEBOUNCE_MS } from './player-search.component';
import type { PlayerSearchResultDto } from '../../api/dtos/playerAllTimeRankDto';

describe('PlayerSearchComponent', () => {
  let fixture: ComponentFixture<PlayerSearchComponent>;
  let http: HttpTestingController;
  let picked: PlayerSearchResultDto[];
  let submitted: string[];
  let cleared: number;
  const found: PlayerSearchResultDto[] = [
    { userID: 1, gamertag: 'Legendary', matchedAlias: null },
    { userID: 2, gamertag: 'Silva', matchedAlias: 'The Silvaa' },
    { userID: 3, gamertag: 'Wall' },
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
  const searched = (text: string): TestRequest => http.expectOne(r => r.url === '/api/Leaderboards/SearchPlayers' && r.params.get('query') === text);
  /** Types the text, waits out the pause and answers with the players found. */
  const showResults = (text = 'Le', results = found) => {
    type(text);
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    searched(text).flush(results);
    fixture.detectChanges();
  };

  it('searches once typing pauses, then lists the players found with their matched alias', () => {
    type('S');
    type('Si');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS - 1);
    http.expectNone('/api/Leaderboards/SearchPlayers');
    type(' Sil ');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    searched('Sil').flush(found);
    fixture.detectChanges();

    expect(list().hidden).toBeFalse();
    expect(input().getAttribute('aria-expanded')).toBe('true');
    expect(input().getAttribute('aria-controls')).toBe('playerSearchList');
    expect(options().map(o => o.textContent!.replace(/\s+/g, ' ').trim())).toEqual(['Legendary', 'Silva (alias: "The Silvaa")', 'Wall']);
  });

  it('never searches fewer than two characters, and closes the list for them', () => {
    showResults();
    type('L');
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS);
    http.expectNone('/api/Leaderboards/SearchPlayers');
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
    expect(options().length).toBe(1);
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
    http.expectNone('/api/Leaderboards/SearchPlayers');
    expect(cleared).toBe(1);
    expect(fixture.componentInstance.query()).toBe('');
    expect(el().querySelector('.search__clear')).toBeNull();
    expect(list().hidden).toBeTrue();
  });

  it('drops the pending search on submit, and looks up the trimmed text', () => {
    type('  abc ');
    el().querySelector<HTMLButtonElement>('.hud-btn')!.click();
    jasmine.clock().tick(SEARCH_DEBOUNCE_MS * 2);
    http.expectNone('/api/Leaderboards/SearchPlayers');
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

  it('looks up the text on Enter when no suggestion is chosen', () => {
    showResults('Leg');
    key('Enter');
    expect(submitted).toEqual(['Leg']);
    expect(picked).toEqual([]);
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
