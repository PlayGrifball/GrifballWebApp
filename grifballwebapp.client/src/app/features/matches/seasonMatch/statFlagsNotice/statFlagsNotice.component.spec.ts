import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { StatFlagDto } from '@api/models';
import { noMaterialAnimations } from '@shared/testing/noMaterialAnimations';
import { StatFlagsNoticeComponent } from './statFlagsNotice.component';

const flag: StatFlagDto = {
  statFlagID: 7, rule: 'TeamSmallerThanFormat', matchID: null, recordedGameID: 101, seasonMatchID: 12, gameNumber: 2,
  playerUserID: null, playerName: null, reason: 'Home Team fielded 3 player(s)', foundAt: '2026-11-20T12:00:00Z',
};

describe('StatFlagsNoticeComponent', () => {
  let fixture: ComponentFixture<StatFlagsNoticeComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StatFlagsNoticeComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), noMaterialAnimations],
    }).compileComponents();

    fixture = TestBed.createComponent(StatFlagsNoticeComponent);
    fixture.componentRef.setInput('seasonMatchID', 12);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('renders nothing while there are no open flags', async () => {
    fixture.detectChanges();
    http.expectOne('/api/season-matches/12/stat-flags').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });

  it('shows each open flag, with its game number and a dismiss button', async () => {
    fixture.detectChanges();
    http.expectOne('/api/season-matches/12/stat-flags').flush([flag]);
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Game 2');
    expect(text).toContain('Home Team fielded 3 player(s)');
  });

  it('dismisses a flag, dropping it from what is shown', async () => {
    fixture.detectChanges();
    http.expectOne('/api/season-matches/12/stat-flags').flush([flag]);
    await fixture.whenStable();
    fixture.detectChanges();

    fixture.componentInstance.dismiss(flag);
    http.expectOne({ method: 'POST', url: '/api/stat-flags/7/dismiss' }).flush(null);
    fixture.detectChanges();

    expect(fixture.componentInstance.flags()).toEqual([]);
  });

  it('logs, rather than throws, when loading fails', async () => {
    spyOn(console, 'error');
    fixture.detectChanges();
    http.expectOne('/api/season-matches/12/stat-flags').flush('x', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    expect(console.error).toHaveBeenCalled();
  });

  it('logs, rather than throws, when dismissing fails, leaving the flag shown', async () => {
    spyOn(console, 'error');
    fixture.detectChanges();
    http.expectOne('/api/season-matches/12/stat-flags').flush([flag]);
    await fixture.whenStable();
    fixture.detectChanges();

    fixture.componentInstance.dismiss(flag);
    http.expectOne('/api/stat-flags/7/dismiss').flush('x', { status: 500, statusText: 'Server Error' });

    expect(console.error).toHaveBeenCalled();
    expect(fixture.componentInstance.flags()).toEqual([flag]);
  });
});
