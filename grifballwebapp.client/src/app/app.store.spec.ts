import { TestBed } from '@angular/core/testing';
import { SeasonService } from '@api/services/season.service';
import { of, throwError } from 'rxjs';
import { AppStore } from './app.store';

describe('AppStore', () => {
  let seasons: jasmine.SpyObj<SeasonService>;
  let store: AppStore;

  beforeEach(() => {
    seasons = jasmine.createSpyObj<SeasonService>('SeasonService', ['getCurrentSeasonId']);
    TestBed.configureTestingModule({ providers: [AppStore, { provide: SeasonService, useValue: seasons }] });
    store = TestBed.inject(AppStore);
  });

  it('gives the current season, and nothing when there is none or it cannot be read', () => {
    const found = jasmine.createSpy('found');
    seasons.getCurrentSeasonId.and.returnValue(of(12));
    store.loadCurrentSeason(found);
    expect(found).toHaveBeenCalledOnceWith(12);

    seasons.getCurrentSeasonId.and.returnValue(of(0));
    store.loadCurrentSeason(found);
    seasons.getCurrentSeasonId.and.returnValue(throwError(() => new Error('down')));
    store.loadCurrentSeason(found);
    expect(found).toHaveBeenCalledTimes(1);
  });
});
