import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { StatFlagDto, StatFlagRule } from '@api/models';
import { noMaterialAnimations } from '@shared/testing/noMaterialAnimations';
import { StatFlagsComponent } from './statFlags.component';

const flag: StatFlagDto = {
  statFlagID: 7, rule: 'NegativeStat', matchID: null, recordedGameID: 101, seasonMatchID: 12, gameNumber: 2,
  playerUserID: 5, playerName: 'Sarge', reason: '-1 kills', foundAt: '2026-11-20T12:00:00Z',
  gamePlayedAt: null, gameTeams: null,
};

describe('StatFlagsComponent', () => {
  let component: StatFlagsComponent;
  let fixture: ComponentFixture<StatFlagsComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatFlagsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), noMaterialAnimations],
    }).compileComponents();

    fixture = TestBed.createComponent(StatFlagsComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    // ngOnInit (store.load()) runs on the first change detection, not on creation
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('loads the open flags on init', () => {
    expect(component.loading()).toBeTrue();
    http.expectOne('/api/stat-flags').flush([flag]);
    expect(component.loading()).toBeFalse();
    expect(component.flags()).toEqual([flag]);
  });

  it('renders every flag loaded, with a dismiss button each', async () => {
    http.expectOne('/api/stat-flags').flush([flag]);
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('-1 kills');
    expect(text).toContain('Sarge');
    expect(fixture.nativeElement.querySelectorAll('button')).not.toHaveSize(0);
  });

  it('shows no open flags once the list is empty', async () => {
    http.expectOne('/api/stat-flags').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No open flags.');
  });

  it('dismisses a flag through the store', () => {
    spyOn(console, 'error');
    http.expectOne('/api/stat-flags').flush([flag]);

    component.dismiss(flag);

    http.expectOne({ method: 'POST', url: '/api/stat-flags/7/dismiss' }).flush(null);
    expect(console.error).not.toHaveBeenCalled();
  });

  it('labels every rule in plain words', () => {
    const rules: StatFlagRule[] = [
      'ScoreDoesNotMatchGoals', 'WinnerDoesNotMatchScores', 'TeamSmallerThanFormat',
      'DateOutsideSeason', 'NegativeStat', 'ImpossibleAccuracy', 'UndecodedCustomScore', 'SeriesDoesNotMatchGames',
      'KillsDoNotMatchDeaths', 'DateOutsideMatchWeek', 'StatsExceedGameLength',
    ];
    http.expectOne('/api/stat-flags').flush([]);

    expect(rules.every(rule => component.ruleLabel(rule) !== rule)).toBeTrue();
  });

  it('labels an unknown rule (a future one the client hasn\'t caught up to) with its own name', () => {
    http.expectOne('/api/stat-flags').flush([]);

    expect(component.ruleLabel('SomeFutureRule' as StatFlagRule)).toBe('SomeFutureRule');
  });

  it('links a Halo match outside a series to its own match page', async () => {
    const haloFlag: StatFlagDto = {
      statFlagID: 8, rule: 'UndecodedCustomScore', matchID: '11111111-1111-1111-1111-111111111111', recordedGameID: null,
      seasonMatchID: null, gameNumber: null, playerUserID: null, playerName: null, reason: 'not decoded', foundAt: '2026-11-20T12:00:00Z',
      gamePlayedAt: null, gameTeams: null,
    };
    http.expectOne('/api/stat-flags').flush([haloFlag]);
    await fixture.whenStable();
    fixture.detectChanges();

    const link: HTMLAnchorElement | null = fixture.nativeElement.querySelector('a[href*="/match/11111111-1111-1111-1111-111111111111"]');
    expect(link).not.toBeNull();
  });

  it('shows the date and teams of a recorded game outside any series, with no page to link to', async () => {
    const scrimFlag: StatFlagDto = {
      statFlagID: 9, rule: 'NegativeStat', matchID: null, recordedGameID: 202,
      seasonMatchID: null, gameNumber: null, playerUserID: null, playerName: null, reason: '-1 kills', foundAt: '2026-11-20T12:00:00Z',
      gamePlayedAt: '2026-11-18T12:00:00Z', gameTeams: 'Reds vs Blues',
    };
    http.expectOne('/api/stat-flags').flush([scrimFlag]);
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Reds vs Blues');
    expect(fixture.nativeElement.querySelector('a[href*="/match/"]')).toBeNull();
  });
});
