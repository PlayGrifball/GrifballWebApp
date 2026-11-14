import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { LetterDto, SeasonSummaryDto } from '@api/models';
import { Observable, of, Subject, throwError } from 'rxjs';
import { SeasonSummaryService } from '../seasonSummary';
import { PlayerGradesStore } from './playerGrades.store';

const grade = (gamertag: string) => ({ gamertag }) as LetterDto;
const grades = (...letters: LetterDto[]) => ({ totals: [], perMinutes: [], letters });

describe('PlayerGradesStore', () => {
  let store: PlayerGradesStore;
  let http: HttpTestingController;
  let summary: Observable<Partial<SeasonSummaryDto>>;

  beforeEach(() => {
    summary = of({ isHistorical: false, hasEnded: false, matchCount: 3, bracketMatchCount: 0 });
    TestBed.configureTestingModule({
      providers: [PlayerGradesStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: SeasonSummaryService, useValue: { get: () => summary } }],
    });
    store = TestBed.inject(PlayerGradesStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks for nothing until it has the season', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.summaryDone()).toBeFalse();
  });

  it("loads the season's summary, then its grades, every player's", () => {
    store.seasonID.use(signal(7));
    TestBed.tick();
    expect(store.summaryDone()).toBeTrue();
    expect(store.gradesDone()).toBeFalse();
    http.expectOne({ method: 'GET', url: '/api/seasons/7/grades' }).flush(grades(grade('Player1'), grade('Player2'), grade('Player3')));
    expect(store.gradesDone()).toBeTrue();
    expect(store.gradesFailed()).toBeFalse();
    expect(store.letters().map(l => l.gamertag)).toEqual(['Player1', 'Player2', 'Player3']);
    expect(store.noMatches()).toBeFalse();
  });

  it('does not ask for the grades of an ended season without matches', () => {
    summary = of({ isHistorical: false, hasEnded: true, matchCount: 0, bracketMatchCount: 0 });
    store.seasonID.use(signal(7));
    TestBed.tick();
    http.expectNone('/api/seasons/7/grades');
    expect(store.noMatches()).toBeTrue();
  });

  it('has no matches for an ended season whose matches gave no grades', () => {
    summary = of({ isHistorical: true, hasEnded: true, matchCount: 3, bracketMatchCount: 0 });
    store.seasonID.use(signal(7));
    TestBed.tick();
    expect(store.noMatches()).toBeFalse();
    http.expectOne('/api/seasons/7/grades').flush(grades());
    expect(store.noMatches()).toBeTrue();
  });

  it('asks for the grades without the summary when it fails, and marks failed grades', () => {
    summary = throwError(() => new Error('down'));
    store.seasonID.use(signal(7));
    TestBed.tick();
    expect(store.summaryDone()).toBeTrue();
    expect(store.summary()).toBeNull();
    http.expectOne('/api/seasons/7/grades').flush('down', { status: 500, statusText: 'Server Error' });
    expect(store.gradesDone()).toBeTrue();
    expect(store.gradesFailed()).toBeTrue();
    expect(store.letters()).toEqual([]);
  });

  it("drops the last season's grades, and their late answer, for another", () => {
    const seasonID = signal(7);
    store.seasonID.use(seasonID);
    TestBed.tick();
    const late = http.expectOne('/api/seasons/7/grades');
    seasonID.set(8);
    TestBed.tick();
    expect(store.gradesDone()).toBeFalse();
    http.expectOne('/api/seasons/8/grades').flush(grades(grade('Other')));
    late.flush(grades(grade('Grif')));
    expect(store.letters().map(l => l.gamertag)).toEqual(['Other']);
  });

  it('waits for a summary still on its way', () => {
    const pending = new Subject<Partial<SeasonSummaryDto>>();
    summary = pending;
    store.seasonID.use(signal(7));
    TestBed.tick();
    http.expectNone('/api/seasons/7/grades');
    expect(store.summaryDone()).toBeFalse();
    pending.next({ isHistorical: false, hasEnded: false, matchCount: 1, bracketMatchCount: 0 });
    http.expectOne('/api/seasons/7/grades').flush(grades());
    expect(store.gradesDone()).toBeTrue();
  });
});
