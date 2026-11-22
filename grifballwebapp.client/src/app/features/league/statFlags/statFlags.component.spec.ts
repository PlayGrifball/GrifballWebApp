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
      'ScoreDoesNotMatchGoals', 'WinnerDoesNotMatchScores', 'PlayerOnBothTeamsOrTwice', 'TeamSmallerThanFormat',
      'DateOutsideSeason', 'NegativeStat', 'ImpossibleAccuracy', 'UndecodedCustomScore', 'SeriesDoesNotMatchGames',
    ];
    http.expectOne('/api/stat-flags').flush([]);

    expect(rules.every(rule => component.ruleLabel(rule) !== rule)).toBeTrue();
  });
});
