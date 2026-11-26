import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { SeasonSummaryDto, SignupResponseDto } from '@api/models';
import { SeasonService } from '@api/services/season.service';
import { SignupsService } from '@api/services/signups.service';
import { of, Subject, throwError } from 'rxjs';
import { SeasonSummaryService } from '../season/seasonSummary';
import { SignupsStore } from './signups.store';

const signup = (personName: string) => ({ personName }) as SignupResponseDto;
const summary = { signupCount: 1 } as SeasonSummaryDto;

describe('SignupsStore', () => {
  let store: SignupsStore;
  let seasons: jasmine.SpyObj<SeasonService>;
  let api: jasmine.SpyObj<SignupsService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<SignupsService>('SignupsService', ['getSignups']);
    seasons = jasmine.createSpyObj<SeasonService>('SeasonService', ['getCurrentSeasonId']);
    api.getSignups.and.callFake(p => of([signup(`Season ${p.seasonID}`)]));
    seasons.getCurrentSeasonId.and.returnValue(of(9));
    TestBed.configureTestingModule({
      providers: [
        SignupsStore,
        { provide: SignupsService, useValue: api },
        { provide: SeasonService, useValue: seasons },
        { provide: SeasonSummaryService, useValue: { get: () => of(summary) } },
      ],
    });
    store = TestBed.inject(SignupsStore);
  });

  const settle = () => TestBed.inject(ApplicationRef).whenStable();

  it('asks for nothing until it has the season', () => {
    TestBed.tick();
    expect(api.getSignups).not.toHaveBeenCalled();
    expect(seasons.getCurrentSeasonId).not.toHaveBeenCalled();
    expect(store.firstLoad()).toBeFalse();
  });

  it("loads the route's season's signups and summary", async () => {
    store.seasonID.use(signal(3));
    await settle();
    expect(store.shownSeasonID()).toBe(3);
    expect(store.signUps().map(s => s.personName)).toEqual(['Season 3']);
    expect(store.summary()).toEqual(summary);
    expect(store.firstLoad()).toBeFalse();
    expect(seasons.getCurrentSeasonId).not.toHaveBeenCalled();
  });

  it('shows the current season when the route names none, waiting (a skeleton) until it knows which', async () => {
    const current = new Subject<number>();
    seasons.getCurrentSeasonId.and.returnValue(current);
    store.seasonID.use(signal(0));
    TestBed.tick();
    expect(store.firstLoad()).toBeTrue();
    expect(api.getSignups).not.toHaveBeenCalled();
    current.next(9);
    current.complete();
    await settle();
    expect(store.shownSeasonID()).toBe(9);
    expect(store.signUps().map(s => s.personName)).toEqual(['Season 9']);
    expect(store.firstLoad()).toBeFalse();
  });

  it("drops the last season's signups for a new one, cancelling their request", async () => {
    const late = new Subject<SignupResponseDto[]>();
    api.getSignups.and.callFake(p => p.seasonID === 3 ? late : of([signup('Simmons')]));
    const seasonID = signal(3);
    store.seasonID.use(seasonID);
    TestBed.tick();
    expect(late.observed).toBeTrue();

    seasonID.set(4);
    TestBed.tick();
    expect(late.observed).toBeFalse();
    await settle();
    expect(store.signUps().map(s => s.personName)).toEqual(['Simmons']);
  });

  it('fails, with no signups, when they cannot be read, and logs it', async () => {
    spyOn(console, 'error');
    api.getSignups.and.returnValue(throwError(() => new Error('down')));
    store.seasonID.use(signal(3));
    await settle();
    expect(store.signUps()).toEqual([]);
    expect(store.failed()).toBeTrue();
    expect(store.firstLoad()).toBeFalse();
    expect(console.error).toHaveBeenCalled();
  });
});
