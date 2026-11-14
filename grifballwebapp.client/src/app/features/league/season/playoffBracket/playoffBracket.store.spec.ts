import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PlayoffBracketStore } from './playoffBracket.store';

const data = (participants: number) => ({ stages: [], matches: [], matchGames: [], participants: Array.from({ length: participants }, (_, id) => ({ id })) });

describe('PlayoffBracketStore', () => {
  let store: PlayoffBracketStore;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [PlayoffBracketStore, provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: MatSnackBar, useValue: snackBar }],
    });
    store = TestBed.inject(PlayoffBracketStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const request = (seasonID: number) => http.expectOne(`/api/seasons/${seasonID}/bracket/viewer-data`);

  it('asks for nothing until it has the season, and knows there is no bracket for none', () => {
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.loaded()).toBeFalse();
    store.seasonID.use(signal(0));
    TestBed.tick();
    http.expectNone(() => true);
    expect(store.loaded()).toBeTrue();
  });

  it("loads the season's bracket, and loads it again on request", () => {
    store.seasonID.use(signal(6));
    TestBed.tick();
    expect(store.loaded()).toBeFalse();
    request(6).flush(data(2));
    expect(store.loaded()).toBeTrue();
    expect(store.viewerData()!.participants).toHaveSize(2);

    store.reload();
    request(6).flush(data(4));
    expect(store.viewerData()!.participants).toHaveSize(4);
  });

  it("drops the last season's bracket, and its late answer, for another", () => {
    const seasonID = signal(6);
    store.seasonID.use(seasonID);
    TestBed.tick();
    const late = request(6);
    seasonID.set(7);
    TestBed.tick();
    request(7).flush(data(0));
    late.flush(data(2));
    expect(store.viewerData()!.participants).toEqual([]);
  });

  it('has none for a season that does not exist, and says when it fails', () => {
    spyOn(console, 'log');
    store.seasonID.use(signal(6));
    TestBed.tick();
    request(6).flush('x', { status: 404, statusText: 'Not Found' });
    expect(store.loaded()).toBeTrue();
    expect(store.viewerData()).toBeNull();
    expect(snackBar.open).not.toHaveBeenCalled();

    store.reload();
    request(6).flush('x', { status: 500, statusText: 'Server Error' });
    expect(snackBar.open).toHaveBeenCalledWith('Failed to get viewer data', 'Close');
  });
});
