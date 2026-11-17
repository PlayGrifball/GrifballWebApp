import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SeasonAwardsComponent } from './seasonAwards.component';

describe('SeasonAwardsComponent', () => {
  let fixture: ComponentFixture<SeasonAwardsComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    fixture = TestBed.createComponent(SeasonAwardsComponent);
    fixture.componentRef.setInput('seasonID', 12);
    fixture.componentRef.setInput('awards', [
      { name: 'MVP', winners: [{ userID: 7, teamID: null, name: 'yLuxe', teamName: null, value: null }] },
      { name: 'Most Kills (one game)', winners: [{ userID: 8, teamID: 40, name: 'Sisum', teamName: 'WYLASS', value: '134' }] },
      { name: 'Team Most Kills', winners: [{ userID: null, teamID: 41, name: 'Chupathingy', teamName: 'Chupathingy', value: '385' }] },
      { name: 'Top 5 Runners', winners: [
        { userID: 9, teamID: null, name: 'Runner One', teamName: null, value: '#1' },
        { userID: 10, teamID: null, name: 'Runner Two', teamName: null, value: '#2' },
      ] },
      { name: 'Unlinked', winners: [{ userID: null, teamID: null, name: 'Nobody', teamName: null, value: null }] },
    ]);
    fixture.detectChanges();
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('lists each award with its winners in order', () => {
    expect(Array.from(el().querySelectorAll('.award__name')).map(n => n.textContent!.trim()))
      .toEqual(['MVP', 'Most Kills (one game)', 'Team Most Kills', 'Top 5 Runners', 'Unlinked']);
    const runners = el().querySelectorAll('.award')[3].querySelectorAll('.award__winner');
    expect(Array.from(runners).map(r => [r.querySelector('a')!.textContent!.trim(), r.querySelector('.award__value')!.textContent!.trim()]))
      .toEqual([['Runner One', '#1'], ['Runner Two', '#2']]);
  });

  it('links a player to their profile, a team to its page, and a player\'s team after them', () => {
    const hrefs = Array.from(el().querySelectorAll('a')).map(a => a.getAttribute('href'));
    expect(hrefs).toEqual(['/profile/7', '/profile/8', '/season/12/team/40', '/season/12/team/41', '/profile/9', '/profile/10']);
    expect(el().querySelector('.award__team')!.textContent!.trim()).toBe('WYLASS');
  });

  it('shows a winner with neither a player nor a team as text', () => {
    const unlinked = el().querySelectorAll('.award')[4];
    expect(unlinked.querySelector('a')).toBeNull();
    expect(unlinked.querySelector('.award__winner')!.textContent!.trim()).toBe('Nobody');
  });
});
