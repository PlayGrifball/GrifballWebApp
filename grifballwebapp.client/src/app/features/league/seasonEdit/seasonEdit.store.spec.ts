import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { GameVersionOptionDto, LeagueOptionDto } from '@api/models';
import { EventOrganizerService } from '@api/services/event-organizer.service';
import { of, Subject, throwError } from 'rxjs';
import type { SeasonSettings } from '../api/seasonSettings';
import { SeasonSettingsApiService } from '../api/seasonSettingsApi.service';
import { SeasonEditStore } from './seasonEdit.store';

const season = (seasonID: number) => ({ seasonID, seasonName: `Season ${seasonID}` }) as SeasonSettings;

describe('SeasonEditStore', () => {
  let store: SeasonEditStore;
  let eventOrganizer: jasmine.SpyObj<EventOrganizerService>;
  let api: jasmine.SpyObj<SeasonSettingsApiService>;

  beforeEach(() => {
    eventOrganizer = jasmine.createSpyObj<EventOrganizerService>('EventOrganizerService', ['getGameVersions', 'getLeagues']);
    api = jasmine.createSpyObj<SeasonSettingsApiService>('SeasonSettingsApiService', ['getSeason', 'getSeasons', 'upsertSeason']);
    eventOrganizer.getGameVersions.and.returnValue(of([{ gameVersionID: 1 } as GameVersionOptionDto]));
    eventOrganizer.getLeagues.and.returnValue(of([{ leagueID: 1 } as LeagueOptionDto]));
    api.getSeasons.and.returnValue(of([season(3)]));
    api.getSeason.and.callFake(id => of(season(id)));
    TestBed.configureTestingModule({
      providers: [SeasonEditStore, { provide: EventOrganizerService, useValue: eventOrganizer }, { provide: SeasonSettingsApiService, useValue: api }],
    });
    store = TestBed.inject(SeasonEditStore);
  });

  it('loads nothing until it has the season, and nothing for a new one', () => {
    TestBed.tick();
    expect(api.getSeason).not.toHaveBeenCalled();
    store.seasonID.use(signal(0));
    TestBed.tick();
    expect(api.getSeason).not.toHaveBeenCalled();
    expect(store.season()).toBeNull();
  });

  it('loads the season, drops it for another, and drops a late answer for the last one', () => {
    const late = new Subject<SeasonSettings | null>();
    api.getSeason.and.callFake(id => id === 4 ? late : of(season(id)));
    const seasonID = signal(4);
    store.seasonID.use(seasonID);
    TestBed.tick();
    seasonID.set(5);
    TestBed.tick();
    expect(store.season()!.seasonID).toBe(5);
    late.next(season(4));
    expect(store.season()!.seasonID).toBe(5);

    seasonID.set(0);
    TestBed.tick();
    expect(store.season()).toBeNull();
  });

  it("loads the selects' options, and logs those that fail", () => {
    const error = spyOn(console, 'error');
    api.getSeasons.and.returnValue(throwError(() => 'down'));
    store.loadOptions();
    expect(store.gameVersions()).toHaveSize(1);
    expect(store.leagues()).toHaveSize(1);
    expect(store.seasons()).toEqual([]);
    expect(error).toHaveBeenCalledWith('down');

    eventOrganizer.getGameVersions.and.returnValue(throwError(() => 'games down'));
    eventOrganizer.getLeagues.and.returnValue(throwError(() => 'leagues down'));
    api.getSeasons.and.returnValue(of([season(3)]));
    store.loadOptions();
    expect(store.seasons()).toHaveSize(1);
    expect(error).toHaveBeenCalledWith('games down');
    expect(error).toHaveBeenCalledWith('leagues down');
  });

  it('saves the season and gives its ID, and logs a failed save', () => {
    const log = spyOn(console, 'log');
    const saved = jasmine.createSpy('saved');
    api.upsertSeason.and.returnValue(of(12));
    store.save(season(0), saved);
    expect(api.upsertSeason).toHaveBeenCalledWith(season(0));
    expect(saved).toHaveBeenCalledWith(12);

    api.upsertSeason.and.returnValue(throwError(() => 'nope'));
    store.save(season(0), saved);
    expect(saved).toHaveBeenCalledTimes(1);
    expect(log).toHaveBeenCalledWith('nope');
  });
});
